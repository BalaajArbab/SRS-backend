using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Orders;
using SRS_backend.Models;

namespace SRS_backend.Services.Orders;

public sealed class OrderService(IMongoDatabase database) : IOrderService
{
    private const string CollectionName = "orders";
    private const string ProductCollectionName = "products";
    private const string CategoryCollectionName = "categories";
    private const int DeliveryFee = 0;

    private readonly IMongoCollection<Order> _orders = database.GetCollection<Order>(CollectionName);
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(ProductCollectionName);
    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>(CategoryCollectionName);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(order => order.OrderNumber),
                new CreateIndexOptions { Name = "ux_orders_order_number", Unique = true }),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(order => order.IdempotencyKeyHash),
                new CreateIndexOptions { Name = "ux_orders_idempotency_key_hash", Unique = true }),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys
                    .Ascending(order => order.FulfillmentStatus)
                    .Descending(order => order.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_orders_fulfillment_created" }),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys
                    .Ascending(order => order.PaymentStatus)
                    .Descending(order => order.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_orders_payment_created" }),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys
                    .Ascending("customer.email")
                    .Descending(order => order.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_orders_customer_email_created" }),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys
                    .Ascending(order => order.IsViewed)
                    .Descending(order => order.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_orders_viewed_created" }),
            new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Descending(order => order.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_orders_created" })
        };

        await _orders.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var idempotencyKeyHash = HashIdempotencyKey(idempotencyKey);
        var existingOrder = await _orders
            .Find(order => order.IdempotencyKeyHash == idempotencyKeyHash)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingOrder is not null)
        {
            return ToResponse(existingOrder);
        }

        var requestedItems = GetValidatedItems(request.Items);
        var customer = GetValidatedCustomer(request.Customer);
        var shippingAddress = GetValidatedShippingAddress(request.ShippingAddress);
        var productIds = requestedItems.Select(item => item.ProductId).ToArray();
        var products = await _products
            .Find(Builders<Product>.Filter.In(product => product.Id, productIds) &
                  Builders<Product>.Filter.Eq(product => product.IsActive, true))
            .ToListAsync(cancellationToken);

        if (products.Count != requestedItems.Count)
        {
            throw new OrderConflictException("One or more products are not available.");
        }

        var categoriesById = await GetCategoriesByIdAsync(products, cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);
        var orderItems = new List<OrderItem>(requestedItems.Count);
        var subtotal = 0;

        foreach (var requestedItem in requestedItems)
        {
            var product = productsById[requestedItem.ProductId];
            if (!product.CategoryIds.Any(categoryId =>
                    categoriesById.TryGetValue(categoryId, out var category) && category.IsActive) ||
                !categoriesById.TryGetValue(product.CategoryIds[0], out var primaryCategory))
            {
                throw new OrderConflictException("One or more products are not available.");
            }

            var unitPrice = GetCurrentUnitPrice(product);
            var lineTotal = checked(unitPrice * requestedItem.Quantity);
            subtotal = checked(subtotal + lineTotal);
            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Categories =
                [
                    new OrderItemCategory
                    {
                        CategoryId = primaryCategory.Id,
                        Name = primaryCategory.Name,
                        Slug = primaryCategory.Slug
                    }
                ],
                UnitPrice = unitPrice,
                Quantity = requestedItem.Quantity,
                LineTotal = lineTotal
            });
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = ObjectId.GenerateNewId(),
            OrderNumber = CreateOrderNumber(now),
            IdempotencyKeyHash = idempotencyKeyHash,
            Customer = customer,
            ShippingAddress = shippingAddress,
            Items = orderItems,
            Subtotal = subtotal,
            DeliveryFee = DeliveryFee,
            Total = checked(subtotal + DeliveryFee),
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = request.PaymentMethod == OrderPaymentMethod.BankTransfer
                ? OrderPaymentStatus.Pending
                : OrderPaymentStatus.NotApplicable,
            FulfillmentStatus = OrderFulfillmentStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await ReserveStockAsync(orderItems, cancellationToken);

        try
        {
            await _orders.InsertOneAsync(order, cancellationToken: cancellationToken);
            return ToResponse(order);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await RestoreStockAsync(orderItems, CancellationToken.None);

            var duplicateOrder = await _orders
                .Find(candidate => candidate.IdempotencyKeyHash == idempotencyKeyHash)
                .FirstOrDefaultAsync(cancellationToken);
            if (duplicateOrder is not null)
            {
                return ToResponse(duplicateOrder);
            }

            throw new OrderConflictException("An order with the generated order number already exists.");
        }
        catch
        {
            await RestoreStockAsync(orderItems, CancellationToken.None);
            throw;
        }
    }

    public async Task<PagedResponse<OrderResponse>> GetAsync(GetOrdersQuery query, CancellationToken cancellationToken)
    {
        var filter = Builders<Order>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(query.CustomerEmail))
        {
            filter &= Builders<Order>.Filter.Eq("customer.email", query.CustomerEmail.Trim().ToLowerInvariant());
        }

        if (query.PaymentStatus is not null)
        {
            filter &= Builders<Order>.Filter.Eq(order => order.PaymentStatus, query.PaymentStatus.Value);
        }

        if (query.FulfillmentStatus is not null)
        {
            filter &= Builders<Order>.Filter.Eq(order => order.FulfillmentStatus, query.FulfillmentStatus.Value);
        }

        if (query.IsViewed is not null)
        {
            var viewedFilter = query.IsViewed.Value
                ? Builders<Order>.Filter.Eq(order => order.IsViewed, true)
                : Builders<Order>.Filter.Or(
                    Builders<Order>.Filter.Eq(order => order.IsViewed, false),
                    Builders<Order>.Filter.Exists("isViewed", false));

            filter &= viewedFilter;
        }

        var skip = (query.Page - 1) * query.PageSize;
        var ordersTask = _orders
            .Find(filter)
            .Sort(Builders<Order>.Sort.Descending(order => order.CreatedAtUtc))
            .Skip(skip)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        var totalCountTask = _orders.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        await Task.WhenAll(ordersTask, totalCountTask);

        return new PagedResponse<OrderResponse>(
            ordersTask.Result.Select(ToResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCountTask.Result);
    }

    public async Task<OrderResponse> GetByIdAsync(string orderId, CancellationToken cancellationToken)
    {
        var parsedOrderId = ParseOrderId(orderId);
        var order = await _orders
            .FindOneAndUpdateAsync(
                candidate => candidate.Id == parsedOrderId,
                Builders<Order>.Update.Set(candidate => candidate.IsViewed, true),
                new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After },
                cancellationToken)
            ?? throw new OrderNotFoundException("The requested order does not exist.");

        return ToResponse(order);
    }

    public async Task<OrderResponse> UpdateStatusAsync(
        string orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var parsedOrderId = ParseOrderId(orderId);
        var order = await _orders
            .Find(candidate => candidate.Id == parsedOrderId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new OrderNotFoundException("The requested order does not exist.");
        var paymentStatus = request.PaymentStatus
            ?? throw new OrderValidationException("PaymentStatus is required.");
        var fulfillmentStatus = request.FulfillmentStatus
            ?? throw new OrderValidationException("FulfillmentStatus is required.");
        var bankTransferReference = TrimToNull(request.BankTransferReference) ?? order.BankTransferReference;

        ValidateStatusChange(order, paymentStatus, fulfillmentStatus, bankTransferReference);

        var updatedOrder = await _orders.FindOneAndUpdateAsync(
            candidate => candidate.Id == parsedOrderId &&
                         candidate.PaymentStatus == order.PaymentStatus &&
                         candidate.FulfillmentStatus == order.FulfillmentStatus,
            Builders<Order>.Update
                .Set(candidate => candidate.PaymentStatus, paymentStatus)
                .Set(candidate => candidate.FulfillmentStatus, fulfillmentStatus)
                .Set(candidate => candidate.BankTransferReference, bankTransferReference)
                .Set(candidate => candidate.UpdatedAtUtc, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After },
            cancellationToken)
            ?? throw new OrderConflictException("The order changed before the status update could be applied.");

        if (updatedOrder.FulfillmentStatus == OrderFulfillmentStatus.Cancelled &&
            order.FulfillmentStatus != OrderFulfillmentStatus.Cancelled)
        {
            await RestoreStockAsync(updatedOrder.Items, CancellationToken.None);
            updatedOrder = await _orders.FindOneAndUpdateAsync(
                candidate => candidate.Id == parsedOrderId && candidate.StockRestoredAtUtc == null,
                Builders<Order>.Update
                    .Set(candidate => candidate.StockRestoredAtUtc, DateTime.UtcNow)
                    .Set(candidate => candidate.UpdatedAtUtc, DateTime.UtcNow),
                new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After },
                cancellationToken)
                ?? updatedOrder;
        }

        return ToResponse(updatedOrder);
    }

    private async Task<IReadOnlyDictionary<ObjectId, Category>> GetCategoriesByIdAsync(
        IEnumerable<Product> products,
        CancellationToken cancellationToken)
    {
        var categoryIds = products
            .SelectMany(product => product.CategoryIds)
            .Distinct()
            .ToArray();
        var activeCategories = await _categories
            .Find(Builders<Category>.Filter.In(category => category.Id, categoryIds))
            .ToListAsync(cancellationToken);

        return activeCategories.ToDictionary(category => category.Id);
    }

    private async Task ReserveStockAsync(IReadOnlyCollection<OrderItem> orderItems, CancellationToken cancellationToken)
    {
        var reservedItems = new List<OrderItem>(orderItems.Count);

        try
        {
            foreach (var item in orderItems)
            {
                var result = await _products.UpdateOneAsync(
                    product => product.Id == item.ProductId && product.IsActive && product.StockQuantity >= item.Quantity,
                    Builders<Product>.Update.Inc(product => product.StockQuantity, -item.Quantity),
                    cancellationToken: cancellationToken);
                if (result.ModifiedCount == 0)
                {
                    throw new OrderConflictException($"Insufficient stock for '{item.Name}'.");
                }

                reservedItems.Add(item);
            }
        }
        catch
        {
            await RestoreStockAsync(reservedItems, CancellationToken.None);
            throw;
        }
    }

    private async Task RestoreStockAsync(IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken)
    {
        foreach (var item in orderItems)
        {
            await _products.UpdateOneAsync(
                product => product.Id == item.ProductId,
                Builders<Product>.Update.Inc(product => product.StockQuantity, item.Quantity),
                cancellationToken: cancellationToken);
        }
    }

    private static List<RequestedOrderItem> GetValidatedItems(IReadOnlyCollection<CreateOrderItemRequest> items)
    {
        if (items.Count == 0)
        {
            throw new OrderValidationException("At least one order item is required.");
        }

        if (items.Count > 50)
        {
            throw new OrderValidationException("An order can contain at most 50 items.");
        }

        var productIds = new HashSet<ObjectId>();
        var validatedItems = new List<RequestedOrderItem>(items.Count);
        foreach (var item in items)
        {
            if (!ObjectId.TryParse(item.ProductId, out var productId))
            {
                throw new OrderValidationException("Each ProductId must be a valid MongoDB ObjectId.");
            }

            if (item.Quantity is < 1 or > 100)
            {
                throw new OrderValidationException("Each item quantity must be between 1 and 100.");
            }

            if (!productIds.Add(productId))
            {
                throw new OrderValidationException("Each product can appear only once in an order.");
            }

            validatedItems.Add(new RequestedOrderItem(productId, item.Quantity));
        }

        return validatedItems;
    }

    private static OrderCustomer GetValidatedCustomer(OrderCustomerRequest customer)
    {
        var email = RequiredTrimmedValue(customer.Email, "Customer email", 256).ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new OrderValidationException("Customer email must be a valid email address.");
        }

        return new OrderCustomer
        {
            Name = RequiredTrimmedValue(customer.Name, "Customer name", 120),
            Email = email,
            Phone = RequiredTrimmedValue(customer.Phone, "Customer phone", 32)
        };
    }

    private static OrderShippingAddress GetValidatedShippingAddress(OrderShippingAddressRequest shippingAddress) => new()
    {
        AddressLine1 = RequiredTrimmedValue(shippingAddress.AddressLine1, "Shipping address line 1", 200),
        AddressLine2 = OptionalTrimmedValue(shippingAddress.AddressLine2, 200, "Shipping address line 2"),
        City = RequiredTrimmedValue(shippingAddress.City, "Shipping city", 120),
        PostalCode = OptionalTrimmedValue(shippingAddress.PostalCode, 32, "Shipping postal code"),
        Notes = OptionalTrimmedValue(shippingAddress.Notes, 1_000, "Shipping notes")
    };

    private static void ValidateStatusChange(
        Order order,
        OrderPaymentStatus paymentStatus,
        OrderFulfillmentStatus fulfillmentStatus,
        string? bankTransferReference)
    {
        if (order.FulfillmentStatus == OrderFulfillmentStatus.Cancelled)
        {
            throw new OrderConflictException("A cancelled order cannot be updated.");
        }

        if (order.PaymentMethod == OrderPaymentMethod.CashOnDelivery && paymentStatus != OrderPaymentStatus.NotApplicable)
        {
            throw new OrderValidationException("Cash-on-delivery orders must use the NotApplicable payment status.");
        }

        if (order.PaymentMethod == OrderPaymentMethod.CashOnDelivery && bankTransferReference is not null)
        {
            throw new OrderValidationException("Cash-on-delivery orders cannot have a bank transfer reference.");
        }

        if (order.PaymentMethod == OrderPaymentMethod.BankTransfer && paymentStatus == OrderPaymentStatus.NotApplicable)
        {
            throw new OrderValidationException("Bank transfer orders must use Pending, Paid, or Rejected payment status.");
        }

        if (order.PaymentStatus == OrderPaymentStatus.Paid && paymentStatus != OrderPaymentStatus.Paid)
        {
            throw new OrderConflictException("A received payment cannot be changed.");
        }

        if (fulfillmentStatus == OrderFulfillmentStatus.Cancelled)
        {
            if (order.FulfillmentStatus == OrderFulfillmentStatus.Delivered)
            {
                throw new OrderConflictException("A delivered order cannot be cancelled.");
            }

            return;
        }

        if (fulfillmentStatus < order.FulfillmentStatus)
        {
            throw new OrderValidationException("Fulfillment status cannot move backwards.");
        }

        if (order.PaymentMethod == OrderPaymentMethod.BankTransfer &&
            fulfillmentStatus != OrderFulfillmentStatus.Pending &&
            paymentStatus != OrderPaymentStatus.Paid)
        {
            throw new OrderConflictException("A bank transfer payment must be paid before fulfillment can proceed.");
        }
    }

    private static int GetCurrentUnitPrice(Product product) =>
        product.IsOnSale && product.SalePrice is int salePrice && salePrice >= 0 && salePrice < product.Price
            ? salePrice
            : product.Price;

    private static ObjectId ParseOrderId(string orderId)
    {
        if (!ObjectId.TryParse(orderId, out var parsedOrderId))
        {
            throw new OrderValidationException("OrderId must be a valid MongoDB ObjectId.");
        }

        return parsedOrderId;
    }

    private static string HashIdempotencyKey(string idempotencyKey)
    {
        var normalizedKey = RequiredTrimmedValue(idempotencyKey, "Idempotency-Key", 128);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey)));
    }

    private static string CreateOrderNumber(DateTime utcNow) =>
        $"ORD-{utcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";

    private static string RequiredTrimmedValue(string? value, string fieldName, int maximumLength)
    {
        var normalizedValue = TrimToNull(value);
        if (normalizedValue is null)
        {
            throw new OrderValidationException($"{fieldName} is required.");
        }

        return OptionalTrimmedValue(normalizedValue, maximumLength, fieldName)!;
    }

    private static string? OptionalTrimmedValue(string? value, int maximumLength, string fieldName)
    {
        var normalizedValue = TrimToNull(value);
        if (normalizedValue is not null && normalizedValue.Length > maximumLength)
        {
            throw new OrderValidationException($"{fieldName} must be {maximumLength} characters or fewer.");
        }

        return normalizedValue;
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OrderResponse ToResponse(Order order) => new(
        order.Id.ToString(),
        order.OrderNumber,
        new OrderCustomerResponse(order.Customer.Name, order.Customer.Email, order.Customer.Phone),
        new OrderShippingAddressResponse(
            order.ShippingAddress.AddressLine1,
            order.ShippingAddress.AddressLine2,
            order.ShippingAddress.City,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Notes),
        order.Items.Select(item => new OrderItemResponse(
            item.ProductId.ToString(),
            item.Name,
            item.Sku,
            item.Categories.Select(category => new OrderItemCategoryResponse(
                category.CategoryId.ToString(),
                category.Name,
                category.Slug)).ToArray(),
            item.UnitPrice,
            item.Quantity,
            item.LineTotal)).ToArray(),
        order.Subtotal,
        order.DeliveryFee,
        order.Total,
        order.PaymentMethod,
        order.PaymentStatus,
        order.FulfillmentStatus,
        order.IsViewed,
        order.BankTransferReference,
        order.CreatedAtUtc,
        order.UpdatedAtUtc);

    private sealed record RequestedOrderItem(ObjectId ProductId, int Quantity);
}

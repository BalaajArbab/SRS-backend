using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Configuration;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Orders;
using SRS_backend.Models;

namespace SRS_backend.Services.Email;

public sealed class OrderEmailDeliveryService(
    IMongoDatabase database,
    IEmailSender emailSender,
    OrderEmailTemplateFactory templateFactory,
    IOptions<EmailOptions> emailOptions,
    ILogger<OrderEmailDeliveryService> logger) : BackgroundService, IOrderEmailDeliveryService
{
    private const string CollectionName = "orders";
    private static readonly TimeSpan DeliveryLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly IMongoCollection<Order> _orders = database.GetCollection<Order>(CollectionName);
    private readonly IEmailSender _emailSender = emailSender;
    private readonly OrderEmailTemplateFactory _templateFactory = templateFactory;
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly ILogger<OrderEmailDeliveryService> _logger = logger;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var index = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys
                .Ascending("emailNotifications.status")
                .Ascending("emailNotifications.nextAttemptAtUtc"),
            new CreateIndexOptions { Name = "ix_orders_email_notification_delivery" });

        await _orders.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task<PagedResponse<FailedOrderEmailNotificationResponse>> GetFailedAsync(
        GetFailedOrderEmailNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var failedMatch = new BsonDocument("emailNotifications.status", OrderEmailNotificationStatus.Failed.ToString());
        var pipeline = new[]
        {
            new BsonDocument("$match", failedMatch),
            new BsonDocument("$unwind", "$emailNotifications"),
            new BsonDocument("$match", failedMatch),
            new BsonDocument("$sort", new BsonDocument
            {
                ["emailNotifications.nextAttemptAtUtc"] = 1,
                ["_id"] = 1
            })
        };
        var skip = (query.Page - 1) * query.PageSize;
        var jobsPipeline = PipelineDefinition<Order, BsonDocument>.Create(
            pipeline.Concat(
            [
                new BsonDocument("$skip", skip),
                new BsonDocument("$limit", query.PageSize)
            ]));
        var countPipeline = PipelineDefinition<Order, BsonDocument>.Create(
            pipeline.Take(3).Append(new BsonDocument("$count", "totalCount")));

        var jobsTask = _orders.Aggregate(jobsPipeline).ToListAsync(cancellationToken);
        var countTask = _orders.Aggregate(countPipeline).FirstOrDefaultAsync(cancellationToken);
        await Task.WhenAll(jobsTask, countTask);

        var totalCount = countTask.Result is null
            ? 0
            : countTask.Result["totalCount"].ToInt64();
        var items = jobsTask.Result.Select(ToFailedResponse).ToArray();
        return new PagedResponse<FailedOrderEmailNotificationResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task RetryFailedAsync(string orderId, string notificationId, CancellationToken cancellationToken)
    {
        var parsedOrderId = ParseOrderId(orderId);
        var parsedNotificationId = ParseNotificationId(notificationId);
        var claimed = await ClaimFailedAsync(parsedOrderId, parsedNotificationId, cancellationToken);

        try
        {
            await _emailSender.SendAsync(_templateFactory.Create(claimed.Order, claimed.Notification), cancellationToken);
            await MarkSentAsync(claimed, cancellationToken);
            _logger.LogInformation(
                "Manually delivered {NotificationKind} notification for order {OrderId}.",
                claimed.Notification.Kind,
                claimed.Order.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await MarkFailedAsync(claimed, cancellationToken);
            _logger.LogWarning(
                "The manual resend of {NotificationKind} notification for order {OrderId} was not accepted by the email provider.",
                claimed.Notification.Kind,
                claimed.Order.Id);
            throw new OrderEmailDeliveryException("The email could not be sent. The failed notification remains available for another retry.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var deliveredWork = false;
            try
            {
                deliveredWork = await TryDeliverNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                _logger.LogWarning("The order email delivery worker could not query or update the delivery queue.");
            }

            if (!deliveredWork)
            {
                await Task.Delay(TimeSpan.FromSeconds(_emailOptions.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task<bool> TryDeliverNextAsync(CancellationToken cancellationToken)
    {
        var claimed = await ClaimNextAsync(cancellationToken);
        if (claimed is null)
        {
            return false;
        }

        try
        {
            await _emailSender.SendAsync(_templateFactory.Create(claimed.Order, claimed.Notification), cancellationToken);
            await MarkSentAsync(claimed, cancellationToken);
            _logger.LogInformation(
                "Delivered {NotificationKind} notification for order {OrderId}.",
                claimed.Notification.Kind,
                claimed.Order.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await MarkFailedAsync(claimed, cancellationToken);
            _logger.LogWarning(
                "Could not deliver {NotificationKind} notification for order {OrderId}; it will be retried when eligible.",
                claimed.Notification.Kind,
                claimed.Order.Id);
        }

        return true;
    }

    private async Task<ClaimedOrderEmailNotification?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseToken = Guid.NewGuid().ToString("N");
        var dueNotification = new BsonDocument("$elemMatch", new BsonDocument("$or", new BsonArray
        {
            new BsonDocument
            {
                ["status"] = new BsonDocument("$in", new BsonArray
                {
                    OrderEmailNotificationStatus.Pending.ToString()
                }),
                ["nextAttemptAtUtc"] = new BsonDocument("$lte", now)
            },
            new BsonDocument
            {
                ["status"] = OrderEmailNotificationStatus.Sending.ToString(),
                ["leaseExpiresAtUtc"] = new BsonDocument("$lte", now)
            }
        }));
        var filter = new BsonDocument("emailNotifications", dueNotification);
        var update = new BsonDocument
        {
            ["$set"] = new BsonDocument
            {
                ["emailNotifications.$.status"] = OrderEmailNotificationStatus.Sending.ToString(),
                ["emailNotifications.$.leaseToken"] = leaseToken,
                ["emailNotifications.$.leaseExpiresAtUtc"] = now.Add(DeliveryLeaseDuration)
            },
            ["$inc"] = new BsonDocument("emailNotifications.$.attemptCount", 1)
        };
        var options = new FindOneAndUpdateOptions<Order>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = new BsonDocument("emailNotifications.nextAttemptAtUtc", 1)
        };
        var order = await _orders.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        var notification = order?.EmailNotifications.SingleOrDefault(candidate => candidate.LeaseToken == leaseToken);

        return order is null || notification is null
            ? null
            : new ClaimedOrderEmailNotification(order, notification, leaseToken);
    }

    private async Task<ClaimedOrderEmailNotification> ClaimFailedAsync(
        ObjectId orderId,
        string notificationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseToken = Guid.NewGuid().ToString("N");
        var failedNotification = new BsonDocument("$elemMatch", new BsonDocument
        {
            ["id"] = notificationId,
            ["status"] = OrderEmailNotificationStatus.Failed.ToString()
        });
        var filter = new BsonDocument
        {
            ["_id"] = orderId,
            ["emailNotifications"] = failedNotification
        };
        var update = new BsonDocument
        {
            ["$set"] = new BsonDocument
            {
                ["emailNotifications.$.status"] = OrderEmailNotificationStatus.Sending.ToString(),
                ["emailNotifications.$.leaseToken"] = leaseToken,
                ["emailNotifications.$.leaseExpiresAtUtc"] = now.Add(DeliveryLeaseDuration)
            },
            ["$inc"] = new BsonDocument("emailNotifications.$.attemptCount", 1)
        };
        var order = await _orders.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        var notification = order?.EmailNotifications.SingleOrDefault(candidate => candidate.LeaseToken == leaseToken);
        if (order is not null && notification is not null)
        {
            return new ClaimedOrderEmailNotification(order, notification, leaseToken);
        }

        var currentOrder = await _orders
            .Find(candidate => candidate.Id == orderId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new OrderEmailNotificationNotFoundException("The requested order email notification does not exist.");
        var currentNotification = currentOrder.EmailNotifications
            .SingleOrDefault(candidate => candidate.Id == notificationId)
            ?? throw new OrderEmailNotificationNotFoundException("The requested order email notification does not exist.");
        throw new OrderEmailNotificationConflictException(
            $"The order email notification is {currentNotification.Status} and cannot be manually resent.");
    }

    private Task MarkSentAsync(ClaimedOrderEmailNotification claimed, CancellationToken cancellationToken) =>
        UpdateClaimedNotificationAsync(
            claimed,
            OrderEmailNotificationStatus.Sent,
            DateTime.UtcNow,
            cancellationToken);

    private Task MarkFailedAsync(ClaimedOrderEmailNotification claimed, CancellationToken cancellationToken)
    {
        var nextAttemptAtUtc = DateTime.UtcNow.AddSeconds(_emailOptions.RetryDelaySeconds);
        var nextStatus = claimed.Notification.AttemptCount >= _emailOptions.MaximumDeliveryAttempts
            ? OrderEmailNotificationStatus.Failed
            : OrderEmailNotificationStatus.Pending;

        return UpdateClaimedNotificationAsync(claimed, nextStatus, nextAttemptAtUtc, cancellationToken);
    }

    private Task UpdateClaimedNotificationAsync(
        ClaimedOrderEmailNotification claimed,
        OrderEmailNotificationStatus status,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken)
    {
        var filter = new BsonDocument
        {
            ["_id"] = claimed.Order.Id,
            ["emailNotifications"] = new BsonDocument("$elemMatch", new BsonDocument
            {
                ["id"] = claimed.Notification.Id,
                ["leaseToken"] = claimed.LeaseToken,
                ["status"] = OrderEmailNotificationStatus.Sending.ToString()
            })
        };
        var update = new BsonDocument("$set", new BsonDocument
        {
            ["emailNotifications.$.status"] = status.ToString(),
            ["emailNotifications.$.nextAttemptAtUtc"] = nextAttemptAtUtc,
            ["emailNotifications.$.leaseToken"] = BsonNull.Value,
            ["emailNotifications.$.leaseExpiresAtUtc"] = BsonNull.Value,
            ["emailNotifications.$.sentAtUtc"] = status == OrderEmailNotificationStatus.Sent
                ? DateTime.UtcNow
                : BsonNull.Value
        });

        return _orders.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    private static FailedOrderEmailNotificationResponse ToFailedResponse(BsonDocument document)
    {
        var notification = document["emailNotifications"].AsBsonDocument;
        return new FailedOrderEmailNotificationResponse(
            document["_id"].AsObjectId.ToString(),
            document["orderNumber"].AsString,
            notification["id"].AsString,
            Enum.Parse<OrderEmailNotificationKind>(notification["kind"].AsString),
            notification["recipientAddress"].AsString,
            notification["attemptCount"].ToInt32());
    }

    private static ObjectId ParseOrderId(string orderId)
    {
        if (!ObjectId.TryParse(orderId, out var parsedOrderId))
        {
            throw new OrderEmailNotificationNotFoundException("The requested order email notification does not exist.");
        }

        return parsedOrderId;
    }

    private static string ParseNotificationId(string notificationId)
    {
        if (!Guid.TryParseExact(notificationId, "N", out _))
        {
            throw new OrderEmailNotificationNotFoundException("The requested order email notification does not exist.");
        }

        return notificationId;
    }

    private sealed record ClaimedOrderEmailNotification(
        Order Order,
        OrderEmailNotification Notification,
        string LeaseToken);
}

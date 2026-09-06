using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Statistics;
using SRS_backend.Models;

namespace SRS_backend.Services.Statistics;

public sealed class StatisticsService(IMongoDatabase database) : IStatisticsService
{
    private const string CategoryCollectionName = "categories";
    private const string ProductCollectionName = "products";
    private const string OrderCollectionName = "orders";
    private const string PageHitCollectionName = "pageHits";
    private const int MaximumDailyStatisticsDays = 366;
    private static readonly TimeZoneInfo PakistanTimeZone = GetPakistanTimeZone();

    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>(CategoryCollectionName);
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(ProductCollectionName);
    private readonly IMongoCollection<Order> _orders = database.GetCollection<Order>(OrderCollectionName);
    private readonly IMongoCollection<PageHit> _pageHits = database.GetCollection<PageHit>(PageHitCollectionName);

    public async Task<PagedResponse<CategoryInventoryValueResponse>> GetCategoryInventoryValuesAsync(
        GetCategoryInventoryValuesQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;
        var categoriesTask = _categories
            .Find(Builders<Category>.Filter.Empty)
            .SortBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Skip(skip)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        var totalCountTask = _categories.CountDocumentsAsync(
            Builders<Category>.Filter.Empty,
            cancellationToken: cancellationToken);

        await Task.WhenAll(categoriesTask, totalCountTask);

        var categories = categoriesTask.Result;
        if (categories.Count == 0)
        {
            return new PagedResponse<CategoryInventoryValueResponse>([], query.Page, query.PageSize, totalCountTask.Result);
        }

        var categoryIds = categories.Select(category => category.Id).ToArray();
        var inventoryValues = await _products.Aggregate<BsonDocument>(new BsonDocument[]
        {
            new BsonDocument("$match", new BsonDocument("categoryIds", new BsonDocument("$in", ToBsonArray(categoryIds)))),
            new BsonDocument("$project", new BsonDocument
            {
                ["primaryCategoryId"] = new BsonDocument("$arrayElemAt", new BsonArray { "$categoryIds", 0 }),
                ["stockQuantity"] = 1,
                ["price"] = 1
            }),
            new BsonDocument("$match", new BsonDocument("primaryCategoryId", new BsonDocument("$in", ToBsonArray(categoryIds)))),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = "$primaryCategoryId",
                ["inventoryValue"] = new BsonDocument("$sum", new BsonDocument("$multiply", new BsonArray
                {
                    "$stockQuantity",
                    "$price"
                }))
            })
        }).ToListAsync(cancellationToken);

        var inventoryValueByCategoryId = inventoryValues.ToDictionary(
            value => value["_id"].AsObjectId,
            value => value["inventoryValue"].ToInt64());

        return new PagedResponse<CategoryInventoryValueResponse>(
            categories.Select(category => new CategoryInventoryValueResponse(
                category.Id.ToString(),
                category.Name,
                category.Slug,
                category.IsActive,
                inventoryValueByCategoryId.GetValueOrDefault(category.Id))).ToArray(),
            query.Page,
            query.PageSize,
            totalCountTask.Result);
    }

    public async Task<PagedResponse<CategorySalesResponse>> GetCategorySalesAsync(
        GetCategorySalesQuery query,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toExclusiveUtc, _, _) = GetDateRange(fromDate, toDate);
        return await GetCategorySalesAsync(query.Page, query.PageSize, fromUtc, toExclusiveUtc, cancellationToken);
    }

    public async Task<PagedResponse<CategorySalesResponse>> GetAllCategorySalesAsync(
        GetAllCategorySalesQuery query,
        CancellationToken cancellationToken) =>
        await GetCategorySalesAsync(query.Page, query.PageSize, null, null, cancellationToken);

    public async Task<PagedResponse<PageHitStatisticsResponse>> GetPageHitStatisticsAsync(
        GetPageHitStatisticsQuery query,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toExclusiveUtc, _, _) = GetDateRange(fromDate, toDate);
        var match = new BsonDocument("viewedAtUtc", new BsonDocument
        {
            ["$gte"] = fromUtc,
            ["$lt"] = toExclusiveUtc
        });
        if (query.PageType is not null)
        {
            match["pageType"] = query.PageType.Value.ToString();
        }

        var pipeline = new List<BsonDocument>
        {
            new("$match", match),
            new("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument
                {
                    ["pageType"] = "$pageType",
                    ["resourceId"] = "$resourceId"
                },
                ["hitCount"] = new BsonDocument("$sum", 1)
            })
        };
        var countPipeline = new List<BsonDocument>(pipeline)
        {
            new("$count", "totalCount")
        };
        var skip = (query.Page - 1) * query.PageSize;
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            ["hitCount"] = -1,
            ["_id.pageType"] = 1
        }));
        pipeline.Add(new BsonDocument("$skip", skip));
        pipeline.Add(new BsonDocument("$limit", query.PageSize));

        var hitsTask = _pageHits.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        var countTask = _pageHits.Aggregate<BsonDocument>(countPipeline).FirstOrDefaultAsync(cancellationToken);
        await Task.WhenAll(hitsTask, countTask);

        var hitGroups = hitsTask.Result.Select(value => new PageHitGroup(
            Enum.Parse<PageHitType>(value["_id"]["pageType"].AsString),
            GetNullableObjectId(value["_id"].AsBsonDocument, "resourceId"),
            value["hitCount"].ToInt64())).ToArray();
        var categoryIds = hitGroups
            .Where(group => group.PageType == PageHitType.Category && group.ResourceId is not null)
            .Select(group => group.ResourceId!.Value)
            .ToArray();
        var productIds = hitGroups
            .Where(group => group.PageType == PageHitType.Product && group.ResourceId is not null)
            .Select(group => group.ResourceId!.Value)
            .ToArray();
        var categoriesTask = categoryIds.Length == 0
            ? Task.FromResult(new List<Category>())
            : _categories.Find(Builders<Category>.Filter.In(category => category.Id, categoryIds)).ToListAsync(cancellationToken);
        var productsTask = productIds.Length == 0
            ? Task.FromResult(new List<Product>())
            : _products.Find(Builders<Product>.Filter.In(product => product.Id, productIds)).ToListAsync(cancellationToken);
        await Task.WhenAll(categoriesTask, productsTask);

        var categoryById = categoriesTask.Result.ToDictionary(category => category.Id);
        var productById = productsTask.Result.ToDictionary(product => product.Id);
        var totalCount = countTask.Result is null ? 0 : countTask.Result["totalCount"].ToInt64();
        return new PagedResponse<PageHitStatisticsResponse>(
            hitGroups.Select(group => ToPageHitStatisticsResponse(group, categoryById, productById)).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    private async Task<PagedResponse<CategorySalesResponse>> GetCategorySalesAsync(
        int page,
        int pageSize,
        DateTime? fromUtc,
        DateTime? toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var pipeline = BuildCategorySalesPipeline(fromUtc, toExclusiveUtc);
        var countPipeline = new List<BsonDocument>(pipeline)
        {
            new("$count", "totalCount")
        };
        var skip = (page - 1) * pageSize;
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            ["sales"] = -1,
            ["_id.name"] = 1
        }));
        pipeline.Add(new BsonDocument("$skip", skip));
        pipeline.Add(new BsonDocument("$limit", pageSize));

        var salesTask = _orders.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        var countTask = _orders.Aggregate<BsonDocument>(countPipeline).FirstOrDefaultAsync(cancellationToken);
        await Task.WhenAll(salesTask, countTask);

        var totalCount = countTask.Result is null ? 0 : countTask.Result["totalCount"].ToInt64();
        return new PagedResponse<CategorySalesResponse>(
            salesTask.Result.Select(value => new CategorySalesResponse(
                value["_id"]["categoryId"].AsObjectId.ToString(),
                value["_id"]["name"].AsString,
                value["_id"]["slug"].AsString,
                value["sales"].ToInt64())).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<DailyOrderStatisticsResponse>> GetDailyOrderStatisticsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toExclusiveUtc, fromDateUtc, toDateUtc) = GetDailyDateRange(fromDate, toDate);
        var dailyValuesTask = _orders.Aggregate<BsonDocument>(new BsonDocument[]
        {
            new BsonDocument("$match", GetEligibleOrdersMatch(fromUtc, toExclusiveUtc)),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = GetPakistanDayExpression(),
                ["orderCount"] = new BsonDocument("$sum", 1),
                ["sales"] = new BsonDocument("$sum", "$total")
            }),
            new BsonDocument("$sort", new BsonDocument("_id", 1))
        }).ToListAsync(cancellationToken);
        var categorySalesTask = _orders.Aggregate<BsonDocument>(new BsonDocument[]
        {
            new BsonDocument("$match", GetEligibleOrdersMatch(fromUtc, toExclusiveUtc)),
            new BsonDocument("$unwind", "$items"),
            new BsonDocument("$project", new BsonDocument
            {
                ["date"] = GetPakistanDayExpression(),
                ["primaryCategory"] = new BsonDocument("$arrayElemAt", new BsonArray { "$items.categories", 0 }),
                ["lineTotal"] = "$items.lineTotal"
            }),
            new BsonDocument("$match", new BsonDocument("primaryCategory.categoryId", new BsonDocument("$exists", true))),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument
                {
                    ["date"] = "$date",
                    ["categoryId"] = "$primaryCategory.categoryId",
                    ["name"] = "$primaryCategory.name",
                    ["slug"] = "$primaryCategory.slug"
                },
                ["sales"] = new BsonDocument("$sum", "$lineTotal")
            }),
            new BsonDocument("$sort", new BsonDocument("_id.date", 1))
        }).ToListAsync(cancellationToken);

        await Task.WhenAll(dailyValuesTask, categorySalesTask);

        var valuesByDate = dailyValuesTask.Result.ToDictionary(
            value => DateOnly.Parse(value["_id"].AsString),
            value => (OrderCount: value["orderCount"].ToInt64(), Sales: value["sales"].ToInt64()));
        var categorySalesByDate = categorySalesTask.Result
            .GroupBy(value => DateOnly.Parse(value["_id"]["date"].AsString))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DailyCategorySalesResponse>)group
                    .Select(value => new DailyCategorySalesResponse(
                        value["_id"]["categoryId"].AsObjectId.ToString(),
                        value["_id"]["name"].AsString,
                        value["_id"]["slug"].AsString,
                        value["sales"].ToInt64()))
                    .OrderByDescending(value => value.Sales)
                    .ThenBy(value => value.Name)
                    .ToArray());
        var result = new List<DailyOrderStatisticsResponse>();
        for (var date = fromDateUtc; date <= toDateUtc; date = date.AddDays(1))
        {
            var value = valuesByDate.GetValueOrDefault(date);
            result.Add(new DailyOrderStatisticsResponse(
                date,
                value.OrderCount,
                value.Sales,
                categorySalesByDate.GetValueOrDefault(date, [])));
        }

        return result;
    }

    public async Task<IReadOnlyList<DailyFulfillmentStatusStatisticsResponse>> GetDailyFulfillmentStatusStatisticsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toExclusiveUtc, fromDateUtc, toDateUtc) = GetDailyDateRange(fromDate, toDate);
        var values = await _orders.Aggregate<BsonDocument>(new BsonDocument[]
        {
            new BsonDocument("$match", GetOrdersInDateRangeMatch(fromUtc, toExclusiveUtc)),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument
                {
                    ["date"] = GetPakistanDayExpression(),
                    ["fulfillmentStatus"] = "$fulfillmentStatus"
                },
                ["orderCount"] = new BsonDocument("$sum", 1),
                ["sales"] = new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray
                    {
                        "$fulfillmentStatus",
                        OrderFulfillmentStatus.Cancelled.ToString()
                    }),
                    0,
                    "$total"
                }))
            }),
            new BsonDocument("$sort", new BsonDocument("_id.date", 1))
        }).ToListAsync(cancellationToken);

        var fulfillmentSalesByDate = values
            .GroupBy(value => DateOnly.Parse(value["_id"]["date"].AsString))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DailyFulfillmentStatusSalesResponse>)group
                    .Select(value => new DailyFulfillmentStatusSalesResponse(
                        Enum.Parse<OrderFulfillmentStatus>(value["_id"]["fulfillmentStatus"].AsString),
                        value["orderCount"].ToInt64(),
                        value["sales"].ToInt64()))
                    .OrderBy(value => value.FulfillmentStatus)
                    .ToArray());

        var result = new List<DailyFulfillmentStatusStatisticsResponse>();
        for (var date = fromDateUtc; date <= toDateUtc; date = date.AddDays(1))
        {
            var fulfillmentSales = fulfillmentSalesByDate.GetValueOrDefault(date, []);
            result.Add(new DailyFulfillmentStatusStatisticsResponse(
                date,
                fulfillmentSales.Sum(value => value.OrderCount),
                fulfillmentSales.Sum(value => value.Sales),
                fulfillmentSales));
        }

        return result;
    }

    private static List<BsonDocument> BuildCategorySalesPipeline(DateTime? fromUtc, DateTime? toExclusiveUtc) =>
    [
        new BsonDocument("$match", GetEligibleOrdersMatch(fromUtc, toExclusiveUtc)),
        new BsonDocument("$unwind", "$items"),
        new BsonDocument("$project", new BsonDocument
        {
            ["primaryCategory"] = new BsonDocument("$arrayElemAt", new BsonArray { "$items.categories", 0 }),
            ["items"] = 1
        }),
        new BsonDocument("$match", new BsonDocument("primaryCategory.categoryId", new BsonDocument("$exists", true))),
        new BsonDocument("$group", new BsonDocument
        {
            ["_id"] = new BsonDocument
            {
                ["categoryId"] = "$primaryCategory.categoryId",
                ["name"] = "$primaryCategory.name",
                ["slug"] = "$primaryCategory.slug"
            },
            ["sales"] = new BsonDocument("$sum", "$items.lineTotal")
        })
    ];

    private static BsonDocument GetEligibleOrdersMatch(DateTime? fromUtc = null, DateTime? toExclusiveUtc = null)
    {
        var match = GetOrdersInDateRangeMatch(fromUtc, toExclusiveUtc);
        match["fulfillmentStatus"] = new BsonDocument("$ne", OrderFulfillmentStatus.Cancelled.ToString());
        return match;
    }

    private static BsonDocument GetOrdersInDateRangeMatch(DateTime? fromUtc, DateTime? toExclusiveUtc)
    {
        var match = new BsonDocument();
        if (fromUtc is not null && toExclusiveUtc is not null)
        {
            match["createdAtUtc"] = new BsonDocument
            {
                ["$gte"] = fromUtc.Value,
                ["$lt"] = toExclusiveUtc.Value
            };
        }

        return match;
    }

    private static BsonDocument GetPakistanDayExpression() => new("$dateToString", new BsonDocument
    {
        ["format"] = "%Y-%m-%d",
        ["date"] = "$createdAtUtc",
        ["timezone"] = "Asia/Karachi"
    });

    private static (DateTime FromUtc, DateTime ToExclusiveUtc, DateOnly FromDateUtc, DateOnly ToDateUtc) GetDailyDateRange(
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var dateRange = GetDateRange(fromDate, toDate);
        if (dateRange.ToDate.DayNumber - dateRange.FromDate.DayNumber + 1 > MaximumDailyStatisticsDays)
        {
            throw new StatisticsValidationException($"The daily statistics date range cannot exceed {MaximumDailyStatisticsDays} days.");
        }

        return dateRange;
    }

    private static (DateTime FromUtc, DateTime ToExclusiveUtc, DateOnly FromDate, DateOnly ToDate) GetDateRange(
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate is null || toDate is null)
        {
            throw new StatisticsValidationException("FromDate and ToDate are required.");
        }

        if (toDate < fromDate)
        {
            throw new StatisticsValidationException("ToDate must be on or after FromDate.");
        }

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
            fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            PakistanTimeZone);
        var toExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(
            toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            PakistanTimeZone);
        return (fromUtc, toExclusiveUtc, fromDate.Value, toDate.Value);
    }

    private static TimeZoneInfo GetPakistanTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        }
    }

    private static PageHitStatisticsResponse ToPageHitStatisticsResponse(
        PageHitGroup group,
        IReadOnlyDictionary<ObjectId, Category> categoryById,
        IReadOnlyDictionary<ObjectId, Product> productById)
    {
        if (group.PageType == PageHitType.FrontPage)
        {
            return new PageHitStatisticsResponse(group.PageType, null, "Front page", null, group.HitCount);
        }

        if (group.ResourceId is null)
        {
            return new PageHitStatisticsResponse(group.PageType, null, null, null, group.HitCount);
        }

        return group.PageType switch
        {
            PageHitType.Category when categoryById.TryGetValue(group.ResourceId.Value, out var category) =>
                new PageHitStatisticsResponse(group.PageType, group.ResourceId.Value.ToString(), category.Name, category.Slug, group.HitCount),
            PageHitType.Product when productById.TryGetValue(group.ResourceId.Value, out var product) =>
                new PageHitStatisticsResponse(group.PageType, group.ResourceId.Value.ToString(), product.Name, product.Slug, group.HitCount),
            _ => new PageHitStatisticsResponse(group.PageType, group.ResourceId.Value.ToString(), null, null, group.HitCount)
        };
    }

    private static ObjectId? GetNullableObjectId(BsonDocument document, string fieldName) =>
        document.TryGetValue(fieldName, out var value) && value.IsObjectId ? value.AsObjectId : null;

    private static BsonArray ToBsonArray(IEnumerable<ObjectId> values) =>
        new(values.Select(value => (BsonValue)value));

    private sealed record PageHitGroup(PageHitType PageType, ObjectId? ResourceId, long HitCount);
}

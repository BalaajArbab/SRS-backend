using System.Text;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Scalar.AspNetCore;
using SRS_backend.Configuration;
using SRS_backend.OpenApi;
using SRS_backend.Security;
using SRS_backend.Services.Authentication;
using SRS_backend.Services.Categories;
using SRS_backend.Services.Database;
using SRS_backend.Services.Products;
using SRS_backend.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

var adminOptions = builder.Configuration
    .GetRequiredSection(AdminOptions.SectionName)
    .Get<AdminOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{AdminOptions.SectionName}' is required.");
var mongoDbOptions = builder.Configuration
    .GetRequiredSection(MongoDbOptions.SectionName)
    .Get<MongoDbOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{MongoDbOptions.SectionName}' is required.");
var blobStorageOptions = builder.Configuration
    .GetRequiredSection(BlobStorageOptions.SectionName)
    .Get<BlobStorageOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{BlobStorageOptions.SectionName}' is required.");

if (string.IsNullOrWhiteSpace(mongoDbOptions.ConnectionString))
{
    throw new InvalidOperationException("Configuration value 'MongoDb:ConnectionString' is required.");
}

if (string.IsNullOrWhiteSpace(blobStorageOptions.ConnectionString))
{
    throw new InvalidOperationException("Configuration value 'BlobStorage:ConnectionString' is required.");
}

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<AuthorizationSecurityRequirementOperationTransformer>();
    options.AddOperationTransformer<RequiredMultipartFormFieldsOperationTransformer>();
    options.AddOperationTransformer<ResponseExampleOperationTransformer>();
});

builder.Services.AddOptions<AdminOptions>()
    .BindConfiguration(AdminOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(
        options => Encoding.UTF8.GetByteCount(options.JwtSigningKey) >= 32,
        "Admin:JwtSigningKey must be at least 32 bytes long.")
    .ValidateOnStart();

builder.Services.AddOptions<MongoDbOptions>()
    .BindConfiguration(MongoDbOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BlobStorageOptions>()
    .BindConfiguration(BlobStorageOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoDbOptions.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
    serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(mongoDbOptions.DatabaseName));
builder.Services.AddSingleton(_ => new BlobServiceClient(blobStorageOptions.ConnectionString));
builder.Services.AddSingleton(serviceProvider => serviceProvider
    .GetRequiredService<BlobServiceClient>()
    .GetBlobContainerClient(blobStorageOptions.ContainerName));
builder.Services.AddSingleton<IPasswordHasher<AdminOptions>, PasswordHasher<AdminOptions>>();
builder.Services.AddSingleton<IAdminAuthenticationService, AdminAuthenticationService>();
builder.Services.AddSingleton<IImageStorageService, ImageStorageService>();
builder.Services.AddSingleton<ICategoryService, CategoryService>();
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddHostedService<MongoDbInitializationService>();
builder.Services.AddHostedService<BlobStorageInitializationService>();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(adminOptions.JwtSigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = adminOptions.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = adminOptions.JwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Admin, policy =>
    {
        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AuthorizationPolicies.AdminRole);
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static async (context, cancellationToken) =>
    {
        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many login attempts.",
                type: "https://httpstatuses.com/429")
            .ExecuteAsync(context.HttpContext);
    };

    options.AddFixedWindowLimiter(RateLimitPolicies.AdminLogin, limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("SRS Backend API")
                                        .ExpandAllTags()
                                        .AddPreferredSecuritySchemes([BearerSecuritySchemeTransformer.SchemeName])
                                        .EnablePersistentAuthentication());
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

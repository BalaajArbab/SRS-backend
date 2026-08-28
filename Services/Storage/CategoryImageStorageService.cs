using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ImageMagick;
using MongoDB.Bson;
using SRS_backend.Configuration;

namespace SRS_backend.Services.Storage;

public sealed class CategoryImageStorageService(
    BlobContainerClient containerClient,
    IOptions<BlobStorageOptions> options) : ICategoryImageStorageService
{
    private const long MaximumImageSizeInBytes = 5 * 1024 * 1024;

    public async Task<string> UploadAsync(
        ObjectId categoryId,
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (image.Length == 0)
        {
            throw new CategoryImageValidationException("The category image must not be empty.");
        }

        if (image.Length > MaximumImageSizeInBytes)
        {
            throw new CategoryImageValidationException("The category image must be 5 MB or smaller.");
        }

        var contentType = GetValidatedContentType(image);
        var blobName = $"categories/{categoryId}/{Guid.NewGuid():N}.webp";
        var maximumImageDimension = (uint)options.Value.MaximumImageDimension;
        var webpQuality = (uint)options.Value.WebpQuality;

        try
        {
            await using var content = image.OpenReadStream();
            ValidateImageSignature(content, contentType);
            content.Position = 0;

            using var processedImage = new MagickImage(content);
            processedImage.AutoOrient();
            processedImage.Strip();
            processedImage.Resize(new MagickGeometry(
                maximumImageDimension,
                maximumImageDimension)
            {
                Greater = true
            });
            processedImage.Format = MagickFormat.WebP;
            processedImage.Quality = webpQuality;

            await using var processedContent = new MemoryStream();
            processedImage.Write(processedContent);
            cancellationToken.ThrowIfCancellationRequested();

            if (processedContent.Length > MaximumImageSizeInBytes)
            {
                throw new CategoryImageValidationException("The processed category image must be 5 MB or smaller.");
            }

            processedContent.Position = 0;

            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(
                processedContent,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "image/webp" }
                },
                cancellationToken);

            return blobName;
        }
        catch (RequestFailedException exception)
        {
            throw new CategoryImageStorageException("The category image could not be stored.", exception);
        }
        catch (MagickException)
        {
            throw new CategoryImageValidationException("The category image could not be decoded.");
        }
    }

    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await containerClient
                .GetBlobClient(blobName)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw new CategoryImageStorageException("The category image could not be deleted.", exception);
        }
    }

    public string GetPublicUrl(string blobName) => containerClient.GetBlobClient(blobName).Uri.AbsoluteUri;

    public async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var publicAccessType = options.Value.PublicBlobAccess
                ? PublicAccessType.Blob
                : PublicAccessType.None;

            await containerClient.CreateIfNotExistsAsync(publicAccessType, cancellationToken: cancellationToken);
            await containerClient.SetAccessPolicyAsync(publicAccessType, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw new CategoryImageStorageException("The category image container could not be initialized.", exception);
        }
    }

    private static string GetValidatedContentType(IFormFile image) => image.ContentType.ToLowerInvariant() switch
    {
        "image/jpeg" => "image/jpeg",
        "image/png" => "image/png",
        "image/webp" => "image/webp",
        _ => throw new CategoryImageValidationException("Only JPEG, PNG, and WebP category images are supported.")
    };

    private static void ValidateImageSignature(Stream content, string contentType)
    {
        Span<byte> header = stackalloc byte[12];
        var bytesRead = content.Read(header);
        var isValid = contentType switch
        {
            "image/jpeg" => bytesRead >= 3 && header[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            "image/png" => bytesRead >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytesRead >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!isValid)
        {
            throw new CategoryImageValidationException("The category image content does not match its declared file type.");
        }
    }
}

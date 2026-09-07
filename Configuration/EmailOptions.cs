using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public EmailProvider Provider { get; init; }

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string SenderAddress { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string AdminRecipientAddress { get; init; } = string.Empty;

    [Range(1, 60)]
    public int PollIntervalSeconds { get; init; } = 5;

    [Range(1, 10)]
    public int MaximumDeliveryAttempts { get; init; } = 5;

    [Range(1, 3_600)]
    public int RetryDelaySeconds { get; init; } = 60;

    [StringLength(4_000)]
    public string? BankTransferInstructions { get; init; }

    public SmtpEmailOptions Smtp { get; init; } = new();

    public AzureCommunicationServicesEmailOptions AzureCommunicationServices { get; init; } = new();
}

public enum EmailProvider
{
    Smtp,
    AzureCommunicationServices
}

public sealed class SmtpEmailOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65_535)]
    public int Port { get; init; } = 1025;

    public bool UseSsl { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }
}

public sealed class AzureCommunicationServicesEmailOptions
{
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;
}

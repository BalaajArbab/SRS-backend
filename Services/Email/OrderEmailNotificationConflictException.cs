namespace SRS_backend.Services.Email;

public sealed class OrderEmailNotificationConflictException(string message) : Exception(message);

namespace SRS_backend.Services.Email;

public sealed class OrderEmailNotificationNotFoundException(string message) : Exception(message);

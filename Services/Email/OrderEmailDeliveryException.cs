namespace SRS_backend.Services.Email;

public sealed class OrderEmailDeliveryException(string message) : Exception(message);

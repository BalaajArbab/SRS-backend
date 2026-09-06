namespace SRS_backend.Services.Orders;

public sealed class OrderValidationException(string message) : Exception(message);

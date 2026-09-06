namespace SRS_backend.Services.Orders;

public sealed class OrderNotFoundException(string message) : Exception(message);

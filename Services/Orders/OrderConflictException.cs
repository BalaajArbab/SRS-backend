namespace SRS_backend.Services.Orders;

public sealed class OrderConflictException(string message) : Exception(message);

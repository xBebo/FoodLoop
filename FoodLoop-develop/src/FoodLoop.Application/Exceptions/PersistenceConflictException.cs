namespace FoodLoop.Application.Exceptions;
public sealed class PersistenceConflictException(string message, Exception innerException) : Exception(message, innerException);

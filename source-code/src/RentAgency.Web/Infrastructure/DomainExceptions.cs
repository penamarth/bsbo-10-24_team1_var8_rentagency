namespace RentAgency.Web.Infrastructure;

public class DomainException(string message) : Exception(message);

public sealed class ForbiddenException(string message = "Forbidden") : DomainException(message);

public sealed class NotFoundException(string message = "Not found") : DomainException(message);

public sealed class ValidationException(string message) : DomainException(message);

public sealed class ConflictException(string message) : DomainException(message);

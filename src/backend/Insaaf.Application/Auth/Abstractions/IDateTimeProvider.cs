namespace Insaaf.Application.Auth.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

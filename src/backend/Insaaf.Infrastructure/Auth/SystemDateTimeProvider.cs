using Insaaf.Application.Auth.Abstractions;

namespace Insaaf.Infrastructure.Auth;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Insaaf.Application.Tests.Social;

/// <summary>A real notification publisher on the test context.</summary>
internal static class TestNotifications
{
    public static NotificationPublisher Publisher(AppDbContext context, IDateTimeProvider clock) =>
        new(
            new NotificationRepository(context, NullLogger<NotificationRepository>.Instance),
            new ReviewRepository(context),
            clock);
}

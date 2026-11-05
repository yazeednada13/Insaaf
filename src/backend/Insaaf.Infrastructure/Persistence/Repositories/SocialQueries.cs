using Insaaf.Domain.Constants;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

/// <summary>Shared query building blocks, so each rule has one definition.</summary>
internal static class SocialQueries
{
    /// <summary>Reviews anyone may see: public, of an active restaurant.</summary>
    public static IQueryable<Review> PublicReviewsOfActiveRestaurants(this AppDbContext dbContext) =>
        dbContext.Reviews
            .AsNoTracking()
            .Where(ReviewVisibility.IsPublic)
            .Where(review => review.Restaurant.IsActive);

    /// <summary>Admin accounts have no public presence: no profile, search result or list row.</summary>
    public static IQueryable<Guid> AdminUserIds(this AppDbContext dbContext) =>
        dbContext.UserRoles
            .Join(
                dbContext.Roles.Where(role => role.Name == Roles.Admin),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId);
}

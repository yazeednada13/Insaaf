using System.Linq.Expressions;
using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Rules;

/// <summary>
/// Single source of truth for which reviews may appear in public results (lists, counts,
/// averages, filters). Only verified reviews are public (ADR-004), and a review an Admin has hidden
/// is not (Phase 9, ADR-017). Every read query must apply it; likes and comments check it too.
/// </summary>
public static class ReviewVisibility
{
    public static readonly Expression<Func<Review, bool>> IsPublic =
        review => review.IsVerified && review.HiddenAt == null;
}

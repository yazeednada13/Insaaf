using System.Globalization;
using System.Text;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

/// <summary>The opaque cursor for review streams (the feed and a user's reviews).</summary>
public static class FeedCursorCodec
{
    public const int MaxLimit = 50;

    public static string Encode(DateTime createdAt, Guid id)
    {
        var value = $"{createdAt.Ticks.ToString(CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static FeedCursor? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('|');

            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks <= DateTime.MaxValue.Ticks
                && Guid.TryParse(parts[1], out var id))
            {
                return new FeedCursor(new DateTime(ticks, DateTimeKind.Utc), id);
            }
        }
        catch (FormatException)
        {
            // Falls through to the validation error below.
        }

        throw new SocialException("validation_error", "Cursor is not valid.");
    }

    public static void ValidateLimit(int limit)
    {
        if (limit < 1 || limit > MaxLimit)
        {
            throw new SocialException("validation_error", $"Limit must be between 1 and {MaxLimit}.");
        }
    }
}

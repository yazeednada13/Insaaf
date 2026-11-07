using System.Globalization;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Admin;

/// <summary>The checked form of a create or update body; categories are still slugs here.</summary>
public sealed record ValidatedRestaurantInput(
    string Name,
    string Description,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? Phone,
    string? ImageUrl,
    string? TimeZoneId,
    IReadOnlyList<string> CategorySlugs,
    IReadOnlyList<OpeningHoursProjection> OpeningHours);

/// <summary>
/// Validates an Admin restaurant body (Phase 9 plan §5.1). Lengths match the database columns.
/// </summary>
public static class RestaurantInputValidator
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxCityLength = 100;
    public const int MaxAddressLength = 500;
    public const int MaxPhoneLength = 32;
    public const int MaxImageUrlLength = 2048;
    public const int MaxTimeZoneIdLength = 64;
    public const int MaxCategories = 10;
    public const int MaxOpeningPeriods = 50;

    private const int MinutesPerDay = 24 * 60;
    private const int MinutesPerWeek = 7 * MinutesPerDay;

    public static (ValidatedRestaurantInput? Input, IReadOnlyList<string> Errors) Validate(UpsertRestaurantRequest? request)
    {
        if (request is null)
        {
            return (null, ["A request body is required."]);
        }

        var errors = new List<string>();

        var name = Required(request.Name, "Name", MaxNameLength, errors);
        var description = Optional(request.Description, "Description", MaxDescriptionLength, errors) ?? string.Empty;
        var city = Required(request.City, "City", MaxCityLength, errors);
        var address = Optional(request.Address, "Address", MaxAddressLength, errors);
        var phone = Optional(request.Phone, "Phone", MaxPhoneLength, errors);
        var imageUrl = Optional(request.ImageUrl, "Image URL", MaxImageUrlLength, errors);
        var timeZoneId = Optional(request.TimeZoneId, "Time zone", MaxTimeZoneIdLength, errors);

        if (request.Latitude is not { } latitude || double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            errors.Add("Latitude is required and must be between -90 and 90.");
        }

        if (request.Longitude is not { } longitude || double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            errors.Add("Longitude is required and must be between -180 and 180.");
        }

        if (request.PriceLevel is not { } priceLevel || !Enum.IsDefined(typeof(PriceLevel), priceLevel))
        {
            errors.Add("Price level is required and must be between 1 and 4.");
        }

        if (imageUrl is not null && !IsHttpUrl(imageUrl))
        {
            errors.Add("Image URL must be an absolute http or https URL.");
        }

        // null clears the time zone; any string, empty included, must be a real IANA id.
        if (request.TimeZoneId is not null && (timeZoneId is null || !IsIanaTimeZone(timeZoneId)))
        {
            errors.Add("Time zone must be a valid IANA time zone id, such as Asia/Amman.");
        }

        var slugs = ValidateCategorySlugs(request.CategorySlugs, errors);
        var hours = ValidateOpeningHours(request.OpeningHours, errors);

        if (hours is { Count: > 0 } && request.TimeZoneId is null)
        {
            errors.Add("Time zone is required when opening hours are given.");
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ValidatedRestaurantInput(
            name!,
            description,
            city!,
            address,
            request.Latitude!.Value,
            request.Longitude!.Value,
            (PriceLevel)request.PriceLevel!.Value,
            phone,
            imageUrl,
            timeZoneId,
            slugs!,
            hours!), errors);
    }

    /// <summary>
    /// IANA ids only: the id must convert to a Windows id (so it is an IANA name, not a Windows name
    /// such as "Arab Standard Time") and the server must be able to load it.
    /// </summary>
    public static bool IsIanaTimeZone(string id) =>
        !string.IsNullOrWhiteSpace(id)
        && TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _)
        && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    /// <summary>
    /// True when any two periods overlap anywhere in the week. Each period is placed on a 7-day circle
    /// of minutes, so an overnight period is compared with the next day's periods, and Saturday night
    /// with Sunday. Periods that only touch (one closes when the next opens) don't overlap.
    /// </summary>
    public static bool HasOverlap(IReadOnlyList<OpeningHoursProjection> periods)
    {
        var intervals = periods.Select(ToWeekInterval).ToList();

        for (var i = 0; i < intervals.Count; i++)
        {
            for (var j = i + 1; j < intervals.Count; j++)
            {
                if (Overlaps(intervals[i], intervals[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (int Start, int End) ToWeekInterval(OpeningHoursProjection period)
    {
        var opens = period.OpensAt.Hour * 60 + period.OpensAt.Minute;
        var closes = period.ClosesAt.Hour * 60 + period.ClosesAt.Minute;
        var length = closes > opens ? closes - opens : closes - opens + MinutesPerDay;
        var start = (int)period.DayOfWeek * MinutesPerDay + opens;
        return (start, start + length);
    }

    // Intervals are shorter than a day, so comparing with the other interval shifted one week either
    // way covers the wrap from Saturday to Sunday.
    private static bool Overlaps((int Start, int End) a, (int Start, int End) b)
    {
        foreach (var shift in new[] { -MinutesPerWeek, 0, MinutesPerWeek })
        {
            if (a.Start < b.End + shift && b.Start + shift < a.End)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string>? ValidateCategorySlugs(IReadOnlyList<string>? slugs, List<string> errors)
    {
        if (slugs is null)
        {
            errors.Add("Category slugs are required.");
            return null;
        }

        if (slugs.Count == 0 || slugs.Count > MaxCategories)
        {
            errors.Add($"Between 1 and {MaxCategories} category slugs are required.");
            return null;
        }

        if (slugs.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Category slugs must not be empty.");
            return null;
        }

        var normalized = slugs.Select(slug => slug.Trim().ToLowerInvariant()).ToList();
        if (normalized.Distinct().Count() != normalized.Count)
        {
            errors.Add("Category slugs must not repeat.");
            return null;
        }

        return normalized;
    }

    private static List<OpeningHoursProjection>? ValidateOpeningHours(
        IReadOnlyList<OpeningHoursInput>? input,
        List<string> errors)
    {
        if (input is null)
        {
            errors.Add("Opening hours are required; send an empty list when they are unknown.");
            return null;
        }

        if (input.Count > MaxOpeningPeriods)
        {
            errors.Add($"At most {MaxOpeningPeriods} opening periods are allowed.");
            return null;
        }

        var periods = new List<OpeningHoursProjection>();
        for (var index = 0; index < input.Count; index++)
        {
            var item = input[index];
            if (item is null
                || item.DayOfWeek is not { } day
                || day is < 0 or > 6
                || !TryParseTime(item.OpensAt, out var opensAt)
                || !TryParseTime(item.ClosesAt, out var closesAt))
            {
                errors.Add($"Opening period {index + 1} needs dayOfWeek 0–6 and opensAt/closesAt as HH:mm.");
                continue;
            }

            if (opensAt == closesAt)
            {
                errors.Add($"Opening period {index + 1} must not open and close at the same time.");
                continue;
            }

            periods.Add(new OpeningHoursProjection((DayOfWeek)day, opensAt, closesAt));
        }

        if (periods.Count == input.Count && HasOverlap(periods))
        {
            errors.Add("Opening periods must not overlap, including overnight periods that run into the next day.");
        }

        return periods.Count == input.Count ? periods : null;
    }

    private static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? Required(string? value, string field, int maxLength, List<string> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            errors.Add($"{field} is required and must be at most {maxLength} characters.");
            return null;
        }

        return trimmed;
    }

    private static string? Optional(string? value, string field, int maxLength, List<string> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors.Add($"{field} must be at most {maxLength} characters.");
        }

        return trimmed;
    }
}

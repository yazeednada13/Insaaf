using Insaaf.Application.Verification.Dtos;

namespace Insaaf.Application.Verification.Validation;

public static class VerifiedReviewValidator
{
    /// <summary>Figma write-review counter (/500); the column allows 2000.</summary>
    public const int MaxTextLength = 500;

    public static IReadOnlyList<string> Validate(CreateVerifiedReviewRequest request)
    {
        var errors = new List<string>();

        AddRatingError(errors, "Rating", request.Rating);
        AddRatingError(errors, "Food rating", request.FoodRating);
        AddRatingError(errors, "Service rating", request.ServiceRating);
        AddRatingError(errors, "Value rating", request.ValueRating);
        AddRatingError(errors, "Atmosphere rating", request.AtmosphereRating);

        if (request.Text is not null && request.Text.Trim().Length > MaxTextLength)
        {
            errors.Add($"Text must be at most {MaxTextLength} characters.");
        }

        return errors;
    }

    private static void AddRatingError(List<string> errors, string name, int value)
    {
        if (value is < 1 or > 5)
        {
            errors.Add($"{name} must be between 1 and 5.");
        }
    }
}

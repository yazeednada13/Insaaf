using Insaaf.Application.Social.Dtos;

namespace Insaaf.Application.Social.Validation;

public static class ReviewCommentValidator
{
    public const int MaxTextLength = 500;
    public const int MaxPageSize = 50;

    public static IReadOnlyList<string> Validate(CreateReviewCommentRequest request)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return ["Comment text is required."];
        }

        return text.Length > MaxTextLength
            ? [$"Comment text must be at most {MaxTextLength} characters."]
            : [];
    }

    public static IReadOnlyList<string> Validate(ReviewCommentsQuery query)
    {
        var errors = new List<string>();

        if (query.Page < 1)
        {
            errors.Add("Page must be at least 1.");
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            errors.Add($"Page size must be between 1 and {MaxPageSize}.");
        }

        return errors;
    }
}

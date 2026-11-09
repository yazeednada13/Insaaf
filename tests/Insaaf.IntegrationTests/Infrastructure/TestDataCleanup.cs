using Microsoft.Data.SqlClient;

namespace Insaaf.IntegrationTests.Infrastructure;

/// <summary>
/// Removes everything one run created, in foreign-key order, in one transaction (the same order as
/// docs/scripts/cleanup-test-data.ps1). Only this run's tagged accounts and restaurants are selected.
/// Fails loudly if anything tagged is left afterwards.
/// </summary>
internal static class TestDataCleanup
{
    private const string Selection = """
        DECLARE @Users TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Users SELECT Id FROM AspNetUsers WHERE Email LIKE @EmailPattern;

        DECLARE @Restaurants TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Restaurants SELECT Id FROM Restaurants WHERE Name LIKE @RestaurantPattern;

        DECLARE @Reviews TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Reviews SELECT Id FROM Reviews
        WHERE UserId IN (SELECT Id FROM @Users) OR RestaurantId IN (SELECT Id FROM @Restaurants);

        DECLARE @Tokens TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Tokens SELECT Id FROM VisitTokens
        WHERE RestaurantId IN (SELECT Id FROM @Restaurants)
           OR RedeemedByUserId IN (SELECT Id FROM @Users)
           OR IssuedByUserId IN (SELECT Id FROM @Users);

        DECLARE @Photos TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Photos SELECT Id FROM ReviewPhotos
        WHERE UploadedByUserId IN (SELECT Id FROM @Users)
           OR ReviewId IN (SELECT Id FROM @Reviews)
           OR VisitTokenId IN (SELECT Id FROM @Tokens);
        """;

    private const string Delete = """
        DELETE FROM Notifications WHERE RecipientId IN (SELECT Id FROM @Users) OR ActorId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews);
        DELETE FROM ReviewPhotos WHERE Id IN (SELECT Id FROM @Photos);
        DELETE FROM ReviewCommentLikes WHERE UserId IN (SELECT Id FROM @Users) OR CommentId IN (SELECT Id FROM ReviewComments WHERE UserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews));
        DELETE FROM SavedReviews WHERE UserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews);
        DELETE FROM SavedRestaurants WHERE UserId IN (SELECT Id FROM @Users) OR RestaurantId IN (SELECT Id FROM @Restaurants);
        DELETE FROM ReviewReports WHERE ReporterUserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews);
        DELETE FROM ReviewReplies WHERE ReviewId IN (SELECT Id FROM @Reviews);
        DELETE FROM UserFoodPreferences WHERE UserId IN (SELECT Id FROM @Users);
        DELETE FROM ReviewLikes WHERE UserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews);
        DELETE FROM ReviewComments WHERE UserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews);
        DELETE FROM UserFollows WHERE FollowerId IN (SELECT Id FROM @Users) OR FolloweeId IN (SELECT Id FROM @Users);
        DELETE FROM Reviews WHERE Id IN (SELECT Id FROM @Reviews);
        DELETE FROM VisitTokens WHERE Id IN (SELECT Id FROM @Tokens);
        DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM @Users);
        DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM @Users);
        DELETE FROM AspNetUserClaims WHERE UserId IN (SELECT Id FROM @Users);
        DELETE FROM AspNetUserLogins WHERE UserId IN (SELECT Id FROM @Users);
        DELETE FROM AspNetUserTokens WHERE UserId IN (SELECT Id FROM @Users);
        DELETE FROM RestaurantOpeningHours WHERE RestaurantId IN (SELECT Id FROM @Restaurants);
        DELETE FROM RestaurantCategories WHERE RestaurantId IN (SELECT Id FROM @Restaurants);
        DELETE FROM Restaurants WHERE Id IN (SELECT Id FROM @Restaurants);
        DELETE FROM AspNetUsers WHERE Id IN (SELECT Id FROM @Users);
        """;

    private const string Leftovers = """
        SELECT (SELECT COUNT(*) FROM AspNetUsers WHERE Email LIKE @EmailPattern)
             + (SELECT COUNT(*) FROM Restaurants WHERE Name LIKE @RestaurantPattern);
        """;

    public static async Task RemoveRunAsync(string connectionString, string emailPrefix, string restaurantPrefix)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync())
        {
            await using var command = new SqlCommand(Selection + Delete, connection, transaction) { CommandTimeout = 120 };
            AddPatterns(command, emailPrefix, restaurantPrefix);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        await using var check = new SqlCommand(Leftovers, connection);
        AddPatterns(check, emailPrefix, restaurantPrefix);
        var left = (int)(await check.ExecuteScalarAsync())!;
        if (left != 0)
        {
            throw new InvalidOperationException($"Integration-test cleanup left {left} tagged row(s) behind.");
        }
    }

    private static void AddPatterns(SqlCommand command, string emailPrefix, string restaurantPrefix)
    {
        command.Parameters.AddWithValue("@EmailPattern", emailPrefix + "%@example.com");
        command.Parameters.AddWithValue("@RestaurantPattern", restaurantPrefix + "%");
    }
}

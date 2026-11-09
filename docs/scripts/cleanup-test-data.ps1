# Insaaf — remove development test data (Phase 10, task 10.9).
#
# Removes test accounts (smoke/probe/limit emails @example.com), test restaurants (named "Smoke…" or
# described "…Phase 9A…"), and everything that depends on them: reviews, photos and their files,
# likes, comments, follows, notifications, visit tokens, refresh tokens and role links.
#
# SAFE BY DEFAULT: without -Apply it only COUNTS what would be removed. Nothing changes.
# With -Apply, all database deletions run in ONE transaction (all or nothing), then the photo folders of
# the removed photos are deleted. Any folder that can't be deleted is removed later by the API's hourly
# cleanup sweep (folders with no row).
#
# Never touched: the Admin account(s), the -KeepEmail accounts, seeded restaurants (unless their reviews
# belong to a test account), categories and the schema.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File docs\scripts\cleanup-test-data.ps1
#   powershell -ExecutionPolicy Bypass -File docs\scripts\cleanup-test-data.ps1 -Apply
param(
    [string]$ConnectionString = 'Server=localhost;Database=Insaaf;Integrated Security=True;TrustServerCertificate=True',
    [string]$PhotoRoot = (Join-Path $env:LOCALAPPDATA 'Insaaf\review-photos'),
    [string[]]$KeepEmail = @(),
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'

# The selection. Test accounts only ever used these @example.com patterns.
$selection = @"
DECLARE @Users TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Users
SELECT u.Id FROM AspNetUsers u
WHERE (u.Email LIKE 'smoke%@example.com' OR u.Email LIKE 'social-%@example.com' OR u.Email LIKE 'limit-%@example.com')
  AND u.Id NOT IN (SELECT ur.UserId FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = 'Admin')
  AND u.Email NOT IN (SELECT value FROM STRING_SPLIT(@Keep, ','));

DECLARE @Restaurants TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Restaurants
SELECT Id FROM Restaurants WHERE Name LIKE N'Smoke%' OR Description LIKE N'%Phase 9A%';

DECLARE @Reviews TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Reviews
SELECT Id FROM Reviews WHERE UserId IN (SELECT Id FROM @Users) OR RestaurantId IN (SELECT Id FROM @Restaurants);

DECLARE @Tokens TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Tokens
SELECT Id FROM VisitTokens
WHERE RestaurantId IN (SELECT Id FROM @Restaurants)
   OR RedeemedByUserId IN (SELECT Id FROM @Users)
   OR IssuedByUserId IN (SELECT Id FROM @Users)
   OR Id IN (SELECT VisitTokenId FROM Reviews WHERE Id IN (SELECT Id FROM @Reviews) AND VisitTokenId IS NOT NULL);

DECLARE @Photos TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @Photos
SELECT Id FROM ReviewPhotos
WHERE UploadedByUserId IN (SELECT Id FROM @Users)
   OR ReviewId IN (SELECT Id FROM @Reviews)
   OR VisitTokenId IN (SELECT Id FROM @Tokens);
"@

$counts = @"
SELECT 'accounts' AS item, COUNT(*) AS n FROM @Users
UNION ALL SELECT 'restaurants', COUNT(*) FROM @Restaurants
UNION ALL SELECT 'reviews', COUNT(*) FROM @Reviews
UNION ALL SELECT 'reviews on real restaurants (by test accounts)', COUNT(*) FROM Reviews WHERE Id IN (SELECT Id FROM @Reviews) AND RestaurantId NOT IN (SELECT Id FROM @Restaurants)
UNION ALL SELECT 'photos', COUNT(*) FROM @Photos
UNION ALL SELECT 'visit tokens', COUNT(*) FROM @Tokens
UNION ALL SELECT 'likes', COUNT(*) FROM ReviewLikes WHERE UserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews)
UNION ALL SELECT 'comments', COUNT(*) FROM ReviewComments WHERE UserId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews)
UNION ALL SELECT 'follows', COUNT(*) FROM UserFollows WHERE FollowerId IN (SELECT Id FROM @Users) OR FolloweeId IN (SELECT Id FROM @Users)
UNION ALL SELECT 'notifications', COUNT(*) FROM Notifications WHERE RecipientId IN (SELECT Id FROM @Users) OR ActorId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews)
UNION ALL SELECT 'refresh tokens', COUNT(*) FROM RefreshTokens WHERE UserId IN (SELECT Id FROM @Users)
UNION ALL SELECT 'accounts kept (all others)', COUNT(*) FROM AspNetUsers WHERE Id NOT IN (SELECT Id FROM @Users)
UNION ALL SELECT 'restaurants kept', COUNT(*) FROM Restaurants WHERE Id NOT IN (SELECT Id FROM @Restaurants);
"@

$delete = @"
DELETE FROM Notifications WHERE RecipientId IN (SELECT Id FROM @Users) OR ActorId IN (SELECT Id FROM @Users) OR ReviewId IN (SELECT Id FROM @Reviews);
DELETE FROM ReviewPhotos WHERE Id IN (SELECT Id FROM @Photos);
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
"@

$connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$connection.Open()
try {
    $keep = ($KeepEmail -join ',')

    $command = $connection.CreateCommand()
    $command.CommandText = $selection + $counts
    [void]$command.Parameters.AddWithValue('@Keep', $keep)
    $reader = $command.ExecuteReader()
    Write-Output ''
    Write-Output ($(if ($Apply) { 'WILL REMOVE:' } else { 'DRY RUN - would remove (nothing changes):' }))
    while ($reader.Read()) { Write-Output ('  {0,-50} {1}' -f $reader.GetString(0), $reader.GetInt32(1)) }
    $reader.Close()

    if (-not $Apply) {
        Write-Output ''
        Write-Output 'Run again with -Apply to remove them.'
        return
    }

    $transaction = $connection.BeginTransaction()
    try {
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandTimeout = 120
        $command.CommandText = $selection + "SELECT Id FROM @Photos;"
        [void]$command.Parameters.AddWithValue('@Keep', $keep)
        $photoIds = New-Object System.Collections.Generic.List[guid]
        $reader = $command.ExecuteReader()
        while ($reader.Read()) { $photoIds.Add($reader.GetGuid(0)) }
        $reader.Close()

        $command.CommandText = $selection + $delete
        [void]$command.ExecuteNonQuery()
        $transaction.Commit()
        Write-Output 'Database: removed (one transaction, committed).'
    }
    catch {
        $transaction.Rollback()
        throw "Database: nothing removed (rolled back): $($_.Exception.Message)"
    }

    $removed = 0; $failed = 0
    foreach ($id in $photoIds) {
        $folder = Join-Path $PhotoRoot $id.ToString('N')
        if (Test-Path $folder) {
            try { Remove-Item -Recurse -Force $folder; $removed++ } catch { $failed++ }
        }
    }
    Write-Output "Photo folders: $removed removed, $failed left for the API's hourly sweep."
}
finally {
    $connection.Close()
}

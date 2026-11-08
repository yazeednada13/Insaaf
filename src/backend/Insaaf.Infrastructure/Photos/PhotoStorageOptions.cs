namespace Insaaf.Infrastructure.Photos;

/// <summary>
/// <c>Storage:ReviewPhotos</c> (environment variable form: <c>Storage__ReviewPhotos__RootPath</c>).
/// </summary>
public sealed class PhotoStorageOptions
{
    public const string SectionName = "Storage:ReviewPhotos";

    /// <summary>
    /// Folder for photo files. When empty, a per-user OS data folder is used, computed at runtime, so no
    /// machine path is written in code or configuration. It must be outside the API's content root.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>Minutes between cleanup runs; 0 disables the hosted cleanup (tests).</summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    public string ResolveRoot()
    {
        if (!string.IsNullOrWhiteSpace(RootPath))
        {
            return Path.GetFullPath(RootPath);
        }

        var dataFolder = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.GetFullPath(Path.Combine(dataFolder, "Insaaf", "review-photos"));
    }

    /// <summary>
    /// Refuses a storage root inside the content root, which is inside the repository. Photos must never
    /// be stored in the source tree; .gitignore isn't relied on.
    /// </summary>
    public static void EnsureOutsideContentRoot(string root, string contentRoot)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var normalizedContent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (normalizedRoot.StartsWith(normalizedContent, comparison))
        {
            throw new InvalidOperationException(
                "Storage:ReviewPhotos:RootPath must be outside the API content root. Configure a folder outside the repository.");
        }
    }
}

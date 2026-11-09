namespace Insaaf.Domain.Enums;

/// <summary>
/// What matters most to a user in a restaurant (Taste Setup step 2, Phase 11). Stored as flags; no
/// ranking rule uses them yet (Figma and the spec define none).
/// </summary>
[Flags]
public enum TastePriority
{
    None = 0,
    FoodQuality = 1,
    Service = 2,
    Price = 4,
    Atmosphere = 8,
    Location = 16,
}

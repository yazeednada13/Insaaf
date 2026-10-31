using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Entities;

public sealed class Restaurant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public PriceLevel PriceLevel { get; set; }

    public string? Phone { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>IANA time zone of the restaurant (e.g. "Asia/Amman"); opening status is unknown when null.</summary>
    public string? TimeZoneId { get; set; }

    public ICollection<RestaurantCategory> RestaurantCategories { get; set; } = [];

    public ICollection<RestaurantOpeningHours> OpeningHours { get; set; } = [];
}

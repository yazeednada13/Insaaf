namespace Insaaf.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public ICollection<RestaurantCategory> RestaurantCategories { get; set; } = [];
}

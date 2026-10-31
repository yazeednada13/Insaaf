namespace Insaaf.Domain.Entities;

public sealed class RestaurantCategory
{
    public Guid RestaurantId { get; set; }

    public Restaurant Restaurant { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;
}

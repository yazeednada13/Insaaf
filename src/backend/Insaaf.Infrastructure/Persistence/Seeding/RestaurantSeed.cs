using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Insaaf.Infrastructure.Persistence.Seeding;

public static class RestaurantSeed
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RestaurantSeed");

        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await context.Restaurants.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = await EnsureCategoriesAsync(context, cancellationToken);
        var now = DateTime.UtcNow;

        var restaurants = new[]
        {
            Create(
                "مطعم السفير",
                "وجبات لبنانية وشامية أصيلة، مشاوي فاخرة ومقبلات يومية طازجة.",
                "جبل عمّان",
                31.9516,
                35.9239,
                PriceLevel.Moderate,
                "+962 6 555 0303",
                "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "lebanese",
                "shami"),
            Create(
                "Burger Makers",
                "أفضل برجر في عمّان، نستخدم لحوم طازجة يومية وخبز مخبوز في المحل.",
                "الدوار السابع",
                31.9754,
                35.8439,
                PriceLevel.Moderate,
                "+962 6 555 0101",
                "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "burger"),
            Create(
                "سوشي هيكاري",
                "تجربة يابانية أصيلة في قلب عمّان. سمك طازج يومي وأجواء هادئة.",
                "عبدون",
                31.9533,
                35.8872,
                PriceLevel.Upscale,
                "+962 6 555 0202",
                "https://images.unsplash.com/photo-1579871494447-9811cf80d66c?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "sushi"),
            Create(
                "Tonino Italiano",
                "مطعم إيطالي راقٍ بوصفات أصيلة، باستا يدوية، وبيئة دافئة.",
                "الرابية",
                31.9862,
                35.8684,
                PriceLevel.Upscale,
                "+962 6 555 0404",
                "https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "italian",
                "pizza"),
            Create(
                "The Coffee Corner",
                "مقهى حديث بأجواء مريحة، قهوة مختصة وحلويات يومية طازجة.",
                "وسط البلد",
                31.9510,
                35.9320,
                PriceLevel.Budget,
                "+962 6 555 0505",
                "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "coffee",
                "desserts"),
            Create(
                "بيتزا أوفين",
                "بيتزا فرن حطب بعجينة رقيقة ومكونات طازجة في عمّان.",
                "الصويفية",
                31.9589,
                35.8601,
                PriceLevel.Moderate,
                "+962 6 555 0606",
                "https://images.unsplash.com/photo-1513104890138-7c749659a591?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "pizza",
                "italian"),
            Create(
                "دجاج الزيتون",
                "دجاج مشوي ومقليات على الطريقة الأردنية بحصص عائلية.",
                "خلدا",
                31.9921,
                35.8376,
                PriceLevel.Budget,
                "+962 6 555 0707",
                "https://images.unsplash.com/photo-1598103442097-8b57074a1c0d?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "chicken",
                "jordanian"),
            Create(
                "حلويات زمرد",
                "كنافة وبقلاوة وحلويات شرقية يومية في جبل الحسين.",
                "جبل الحسين",
                31.9738,
                35.9184,
                PriceLevel.Budget,
                "+962 6 555 0808",
                "https://images.unsplash.com/photo-1578985545062-69928b1d9587?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "desserts"),
            Create(
                "قصر المشاوي",
                "مشاوي أردنية فاخرة ومقبلات شامية في أجواء تراثية.",
                "الوحدات",
                31.9284,
                35.9391,
                PriceLevel.Premium,
                "+962 6 555 0909",
                "https://images.unsplash.com/photo-1555939594-58d7cb561ad1?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "jordanian",
                "shami"),
            Create(
                "كافيه الرووف",
                "قهوة مختصة وإطلالة على عمّان، مناسب للعمل واللقاءات.",
                "الشميساني",
                31.9747,
                35.8996,
                PriceLevel.Moderate,
                "+962 6 555 1010",
                "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "coffee"),
            Create(
                "ماسة اللبناني",
                "مطبخ لبناني راقٍ مع قائمة تذوق ومأكولات موسمية.",
                "الدوار الرابع",
                31.9624,
                35.8778,
                PriceLevel.Premium,
                "+962 6 555 1111",
                "https://images.unsplash.com/photo-1544025162-d76690232da9?w=600&h=400&fit=crop&auto=format",
                true,
                now,
                categories,
                "lebanese"),
            Create(
                "مطعم مغلق للتجارب",
                "مطعم غير ظاهر في الاكتشاف — بيانات اختبار داخلية.",
                "ماركا",
                31.9800,
                35.9910,
                PriceLevel.Budget,
                null,
                null,
                false,
                now,
                categories,
                "jordanian"),
        };

        context.Restaurants.AddRange(restaurants);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} Amman restaurants.", restaurants.Length);
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var definitions = new (string Slug, string Name)[]
        {
            ("burger", "برجر"),
            ("pizza", "بيتزا"),
            ("sushi", "سوشي"),
            ("coffee", "قهوة"),
            ("chicken", "دجاج"),
            ("desserts", "حلويات"),
            ("italian", "إيطالي"),
            ("lebanese", "لبناني"),
            ("shami", "شامي"),
            ("jordanian", "أردني"),
        };

        var existing = await context.Categories.ToListAsync(cancellationToken);
        var bySlug = existing.ToDictionary(category => category.Slug, StringComparer.OrdinalIgnoreCase);

        foreach (var (slug, name) in definitions)
        {
            if (bySlug.ContainsKey(slug))
            {
                continue;
            }

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
            };
            context.Categories.Add(category);
            bySlug[slug] = category;
        }

        await context.SaveChangesAsync(cancellationToken);
        return bySlug;
    }

    private static Restaurant Create(
        string name,
        string description,
        string address,
        double latitude,
        double longitude,
        PriceLevel priceLevel,
        string? phone,
        string? imageUrl,
        bool isActive,
        DateTime createdAt,
        IReadOnlyDictionary<string, Category> categories,
        params string[] slugs)
    {
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            City = "عمّان",
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            PriceLevel = priceLevel,
            Phone = phone,
            ImageUrl = imageUrl,
            IsActive = isActive,
            CreatedAt = createdAt,
        };

        foreach (var slug in slugs)
        {
            restaurant.RestaurantCategories.Add(new RestaurantCategory
            {
                Restaurant = restaurant,
                Category = categories[slug],
            });
        }

        return restaurant;
    }
}

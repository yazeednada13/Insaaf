using System.Globalization;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Admin;

public interface IRestaurantAdminService
{
    Task<AdminRestaurantListResultDto> ListAsync(AdminRestaurantListQuery query, CancellationToken cancellationToken = default);

    Task<AdminRestaurantDto> CreateAsync(Guid actorId, UpsertRestaurantRequest? request, CancellationToken cancellationToken = default);

    Task<AdminRestaurantDto> UpdateAsync(
        Guid actorId,
        Guid restaurantId,
        UpsertRestaurantRequest? request,
        CancellationToken cancellationToken = default);

    Task<AdminRestaurantDto> SetStatusAsync(
        Guid actorId,
        Guid restaurantId,
        SetRestaurantStatusRequest? request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Restaurant catalog management for Admin (Phase 9, task 9.1). The role is checked by the controller;
/// this service receives the acting Admin's id as <c>actorId</c> and never reads claims itself.
/// There is no hard delete: deactivating hides the restaurant and its reviews from every public read.
/// </summary>
public sealed class RestaurantAdminService : IRestaurantAdminService
{
    public const int MaxPageSize = 50;

    private readonly IRestaurantRepository _restaurants;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLog _audit;

    public RestaurantAdminService(IRestaurantRepository restaurants, IDateTimeProvider clock, IAdminAuditLog audit)
    {
        _restaurants = restaurants;
        _clock = clock;
        _audit = audit;
    }

    public async Task<AdminRestaurantListResultDto> ListAsync(
        AdminRestaurantListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            throw new AdminException("validation_error", $"Page must be at least 1 and page size between 1 and {MaxPageSize}.");
        }

        var page = await _restaurants.ListForAdminAsync(query.IncludeInactive, query.Page, query.PageSize, cancellationToken);
        return new AdminRestaurantListResultDto(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<AdminRestaurantDto> CreateAsync(
        Guid actorId,
        UpsertRestaurantRequest? request,
        CancellationToken cancellationToken = default)
    {
        var model = await ToWriteModelAsync(actorId, "restaurant.create", Guid.Empty, request, cancellationToken);
        var id = await _restaurants.CreateAsync(model, _clock.UtcNow, cancellationToken);

        Record("restaurant.create", actorId, id, AdminAuditResult.Succeeded);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<AdminRestaurantDto> UpdateAsync(
        Guid actorId,
        Guid restaurantId,
        UpsertRestaurantRequest? request,
        CancellationToken cancellationToken = default)
    {
        var model = await ToWriteModelAsync(actorId, "restaurant.update", restaurantId, request, cancellationToken);
        if (!await _restaurants.UpdateAsync(restaurantId, model, cancellationToken))
        {
            Record("restaurant.update", actorId, restaurantId, AdminAuditResult.NotFound);
            throw AdminException.NotFound("Restaurant");
        }

        Record("restaurant.update", actorId, restaurantId, AdminAuditResult.Succeeded);
        return await GetAsync(restaurantId, cancellationToken);
    }

    public async Task<AdminRestaurantDto> SetStatusAsync(
        Guid actorId,
        Guid restaurantId,
        SetRestaurantStatusRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.IsActive is not { } isActive)
        {
            Record("restaurant.status", actorId, restaurantId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", "isActive is required.");
        }

        var changed = await _restaurants.SetActiveAsync(restaurantId, isActive, cancellationToken);
        if (changed is null)
        {
            Record("restaurant.status", actorId, restaurantId, AdminAuditResult.NotFound);
            throw AdminException.NotFound("Restaurant");
        }

        Record("restaurant.status", actorId, restaurantId, changed.Value ? AdminAuditResult.Succeeded : AdminAuditResult.NoChange);
        return await GetAsync(restaurantId, cancellationToken);
    }

    private async Task<RestaurantWriteModel> ToWriteModelAsync(
        Guid actorId,
        string action,
        Guid targetId,
        UpsertRestaurantRequest? request,
        CancellationToken cancellationToken)
    {
        var (input, errors) = RestaurantInputValidator.Validate(request);
        if (input is null)
        {
            Record(action, actorId, targetId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", string.Join(' ', errors));
        }

        var categories = await _restaurants.GetCategoriesBySlugsAsync(input.CategorySlugs, cancellationToken);
        var unknown = input.CategorySlugs
            .Except(categories.Select(category => category.Slug.ToLowerInvariant()))
            .ToList();
        if (unknown.Count > 0)
        {
            Record(action, actorId, targetId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", $"Unknown category slugs: {string.Join(", ", unknown)}.");
        }

        return new RestaurantWriteModel(
            input.Name,
            input.Description,
            input.City,
            input.Address,
            input.Latitude,
            input.Longitude,
            input.PriceLevel,
            input.Phone,
            input.ImageUrl,
            input.TimeZoneId,
            categories.Select(category => category.Id).ToList(),
            input.OpeningHours);
    }

    private async Task<AdminRestaurantDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(await _restaurants.GetForAdminAsync(id, cancellationToken) ?? throw AdminException.NotFound("Restaurant"));

    private void Record(string action, Guid actorId, Guid targetId, string result) =>
        _audit.Record(new AdminAuditEntry(action, actorId, "restaurant", targetId, _clock.UtcNow, result));

    private static AdminRestaurantDto ToDto(AdminRestaurantProjection item) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.City,
            item.Address,
            item.Latitude,
            item.Longitude,
            (int)item.PriceLevel,
            item.Phone,
            item.ImageUrl,
            item.TimeZoneId,
            item.IsActive,
            item.IsPartner,
            item.CreatedAt,
            item.Categories.Select(category => new CategoryDto(category.Id, category.Name, category.Slug)).ToList(),
            item.OpeningHours
                .OrderBy(hours => hours.DayOfWeek)
                .ThenBy(hours => hours.OpensAt)
                .Select(hours => new OpeningHoursDto((int)hours.DayOfWeek, hours.OpensAt.ToString("HH:mm", CultureInfo.InvariantCulture), hours.ClosesAt.ToString("HH:mm", CultureInfo.InvariantCulture)))
                .ToList(),
            item.PublicReviewCount);
}

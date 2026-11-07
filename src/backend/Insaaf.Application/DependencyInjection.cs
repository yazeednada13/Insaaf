using Insaaf.Application.Auth;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Admin;
using Insaaf.Application.Reviews;
using Insaaf.Application.Social;
using Insaaf.Application.Verification;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReviewLikeService, ReviewLikeService>();
        services.AddScoped<IReviewCommentService, ReviewCommentService>();
        services.AddScoped<IUserFollowService, UserFollowService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<IUserTasteService, UserTasteService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IVisitTokenIssuer, VisitTokenIssuer>();
        services.AddScoped<IVisitTokenLifecycleService, VisitTokenLifecycleService>();
        services.AddScoped<IRestaurantPartnershipService, RestaurantPartnershipService>();
        services.AddScoped<IRestaurantAdminService, RestaurantAdminService>();
        services.AddScoped<IReviewModerationService, ReviewModerationService>();
        services.AddScoped<IUserModerationService, UserModerationService>();
        return services;
    }
}

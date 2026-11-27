using Microsoft.Extensions.DependencyInjection;
using RentAgency.Web.Features.Housing.Application;

namespace RentAgency.Web.Features.Housing;

public static class HousingServiceCollectionExtensions
{
    public static IServiceCollection AddHousing(this IServiceCollection services)
    {
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IListingLookup>(serviceProvider => serviceProvider.GetRequiredService<IListingService>());
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }
}

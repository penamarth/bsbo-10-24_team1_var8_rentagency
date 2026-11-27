using Microsoft.Extensions.DependencyInjection;
using RentAgency.Web.Features.Rentals.Application;

namespace RentAgency.Web.Features.Rentals;

public static class RentalsServiceCollectionExtensions
{
    public static IServiceCollection AddRentals(this IServiceCollection services)
    {
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IRentalService, RentalService>();
        return services;
    }
}

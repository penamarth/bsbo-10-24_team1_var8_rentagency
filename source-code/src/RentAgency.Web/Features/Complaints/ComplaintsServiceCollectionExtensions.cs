using Microsoft.Extensions.DependencyInjection;
using RentAgency.Web.Features.Complaints.Application;

namespace RentAgency.Web.Features.Complaints;

public static class ComplaintsServiceCollectionExtensions
{
    public static IServiceCollection AddComplaints(this IServiceCollection services)
    {
        services.AddScoped<IComplaintService, ComplaintService>();
        return services;
    }
}

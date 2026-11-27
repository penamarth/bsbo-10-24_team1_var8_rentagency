using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints.Domain;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Domain;
using HousingRoot = RentAgency.Web.Features.Housing.Domain.Housing;

namespace RentAgency.Web.Data;

public class RentAgencyDbContext(DbContextOptions<RentAgencyDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<HousingRoot> Housings => Set<HousingRoot>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();

    public DbSet<RentalRequest> RentalRequests => Set<RentalRequest>();

    public DbSet<RentalAgreement> RentalAgreements => Set<RentalAgreement>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Complaint> Complaints => Set<Complaint>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(RentAgencyDbContext).Assembly);
    }
}

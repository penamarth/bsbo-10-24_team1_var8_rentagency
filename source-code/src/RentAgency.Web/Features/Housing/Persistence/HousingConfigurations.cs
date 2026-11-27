using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentAgency.Web.Features.Housing.Domain;
using HousingRoot = RentAgency.Web.Features.Housing.Domain.Housing;

namespace RentAgency.Web.Features.Housing.Persistence;

public sealed class HousingConfiguration : IEntityTypeConfiguration<HousingRoot>
{
    public void Configure(EntityTypeBuilder<HousingRoot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Address).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.RentalTerms).IsRequired().HasMaxLength(2000);
        builder.HasIndex(x => x.OwnerId);
    }
}

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Housing)
            .WithMany(h => h.Listings)
            .HasForeignKey(x => x.HousingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Photos)
            .WithOne()
            .HasForeignKey(p => p.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Decisions)
            .WithOne()
            .HasForeignKey(d => d.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistory)
            .WithOne()
            .HasForeignKey(s => s.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ListingPhotoConfiguration : IEntityTypeConfiguration<ListingPhoto>
{
    public void Configure(EntityTypeBuilder<ListingPhoto> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
    }
}

public sealed class ModerationDecisionConfiguration : IEntityTypeConfiguration<ModerationDecision>
{
    public void Configure(EntityTypeBuilder<ModerationDecision> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
    }
}

public sealed class ListingStatusChangeConfiguration : IEntityTypeConfiguration<ListingStatusChange>
{
    public void Configure(EntityTypeBuilder<ListingStatusChange> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    }
}

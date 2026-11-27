using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Web.Features.Rentals.Persistence;

public sealed class RentalRequestConfiguration : IEntityTypeConfiguration<RentalRequest>
{
    public void Configure(EntityTypeBuilder<RentalRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.InitialMessage).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ListingId, x.TenantId })
            .IsUnique()
            .HasFilter("\"Status\" IN ('New', 'InDiscussion')");

        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(m => m.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Agreement)
            .WithOne(a => a.Request)
            .HasForeignKey<RentalAgreement>(a => a.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(4000);
    }
}

public sealed class RentalAgreementConfiguration : IEntityTypeConfiguration<RentalAgreement>
{
    public void Configure(EntityTypeBuilder<RentalAgreement> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TermsVersion).IsConcurrencyToken();
        builder.HasIndex(x => x.RequestId).IsUnique();

        builder.OwnsOne(x => x.Terms, terms =>
        {
            terms.Property(t => t.Start).IsRequired();
            terms.Property(t => t.End).IsRequired();
            terms.Property(t => t.MonthlyPrice).HasPrecision(18, 2);
            terms.Property(t => t.Deposit).HasPrecision(18, 2);
        });

        builder.HasMany(x => x.Confirmations)
            .WithOne()
            .HasForeignKey(c => c.AgreementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Reviews)
            .WithOne()
            .HasForeignKey(r => r.AgreementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConfirmationConfiguration : IEntityTypeConfiguration<Confirmation>
{
    public void Configure(EntityTypeBuilder<Confirmation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(4000);
        builder.HasIndex(x => new { x.AgreementId, x.AuthorId }).IsUnique();
    }
}

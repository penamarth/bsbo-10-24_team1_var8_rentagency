using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentAgency.Web.Features.Complaints.Domain;

namespace RentAgency.Web.Features.Complaints.Persistence;

public sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => x.ReporterId);

        builder.HasMany(x => x.Handlings)
            .WithOne()
            .HasForeignKey(h => h.ComplaintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Evidence)
            .WithOne()
            .HasForeignKey(e => e.ComplaintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ComplaintHandlingConfiguration : IEntityTypeConfiguration<ComplaintHandling>
{
    public void Configure(EntityTypeBuilder<ComplaintHandling> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedDetails).HasMaxLength(2000);
        builder.Property(x => x.Decision).HasMaxLength(2000);
        builder.Property(x => x.Rationale).HasMaxLength(2000);
    }
}

public sealed class ComplaintEvidenceConfiguration : IEntityTypeConfiguration<ComplaintEvidence>
{
    public void Configure(EntityTypeBuilder<ComplaintEvidence> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
    }
}

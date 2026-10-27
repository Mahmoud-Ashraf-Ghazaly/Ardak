using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Identity;
using Ardak.Model.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ardak.Infrastructure.Data;

public class ArdakDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>, IAppDbContext
{
    public ArdakDbContext(DbContextOptions<ArdakDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyCustomer> PropertyCustomers => Set<PropertyCustomer>();
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<ProcedureStatusHistory> ProcedureStatusHistories => Set<ProcedureStatusHistory>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FullName).IsRequired().HasMaxLength(150);
            entity.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Email).HasMaxLength(200);
            entity.Property(c => c.Address).HasMaxLength(500);
            entity.Property(c => c.NationalIdOrTaxNumber).HasMaxLength(50);
            entity.Property(c => c.InternalNotes).HasMaxLength(2000);
            entity.Property(c => c.IdentityUserId).HasMaxLength(450);

            entity.HasIndex(c => c.PhoneNumber);
            entity.HasIndex(c => c.Email);
            entity.HasIndex(c => c.IdentityUserId).IsUnique(false);

            entity.HasMany(c => c.Properties)
                  .WithOne(p => p.Customer)
                  .HasForeignKey(p => p.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Property>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ReferenceNumber).IsRequired().HasMaxLength(50);
            entity.HasIndex(p => p.ReferenceNumber).IsUnique();

            entity.Property(p => p.Title).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Governorate).IsRequired().HasMaxLength(100);
            entity.Property(p => p.City).IsRequired().HasMaxLength(100);
            entity.Property(p => p.DistrictOrArea).IsRequired().HasMaxLength(100);
            entity.Property(p => p.AddressDetails).HasMaxLength(500);
            entity.Property(p => p.AreaMeasurement).HasPrecision(18, 2);
            entity.Property(p => p.AreaUnit).HasMaxLength(20);
            entity.Property(p => p.OwnershipType).HasMaxLength(100);
            entity.Property(p => p.DeedOrTitleNumber).HasMaxLength(100);
            entity.Property(p => p.InternalNotes).HasMaxLength(4000);

            entity.HasIndex(p => p.City);
            entity.HasIndex(p => p.DistrictOrArea);
            entity.HasIndex(p => p.Type);
            entity.HasIndex(p => p.Status);

            entity.HasOne(p => p.Customer)
                  .WithMany(c => c.Properties)
                  .HasForeignKey(p => p.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PropertyCustomer>(entity =>
        {
            entity.HasKey(pc => new { pc.PropertyId, pc.CustomerId });

            entity.Property(pc => pc.OwnershipRole).IsRequired().HasMaxLength(50);
            entity.Property(pc => pc.OwnershipPercentage).HasPrecision(5, 2);

            entity.HasOne(pc => pc.Property)
                  .WithMany(p => p.CustomerAssociations)
                  .HasForeignKey(pc => pc.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Customer)
                  .WithMany(c => c.PropertyAssociations)
                  .HasForeignKey(pc => pc.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Procedure>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TypeLabel).HasMaxLength(100);
            entity.Property(p => p.ApplicationNumber).HasMaxLength(100);
            entity.Property(p => p.ResponsibleAuthority).HasMaxLength(200);
            entity.Property(p => p.NextActionDescription).HasMaxLength(500);
            entity.Property(p => p.MissingDocuments).HasMaxLength(2000);
            entity.Property(p => p.InternalNotes).HasMaxLength(4000);
            entity.Property(p => p.CustomerUpdateNotes).HasMaxLength(2000);
            entity.Property(p => p.TotalFee).HasPrecision(18, 2);
            entity.Property(p => p.AmountPaid).HasPrecision(18, 2);
            entity.Property(p => p.BalanceDue).HasPrecision(18, 2);

            entity.HasIndex(p => p.PropertyId);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.Type);
            entity.HasIndex(p => p.NextActionDeadline);
            entity.HasIndex(p => p.IsCustomerVisible);

            entity.HasOne(p => p.Property)
                  .WithMany(pr => pr.Procedures)
                  .HasForeignKey(p => p.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProcedureStatusHistory>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.ChangeNote).HasMaxLength(1000);
            entity.Property(h => h.ChangedByUserId).HasMaxLength(450);

            entity.HasIndex(h => h.ProcedureId);
            entity.HasIndex(h => h.ChangedAtUtc);

            entity.HasOne(h => h.Procedure)
                  .WithMany(p => p.StatusHistory)
                  .HasForeignKey(h => h.ProcedureId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(200);
            entity.Property(d => d.TypeLabel).HasMaxLength(100);
            entity.Property(d => d.Description).HasMaxLength(1000);
            entity.Property(d => d.ReferenceNumber).HasMaxLength(100);
            entity.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(d => d.StorageKey).IsRequired().HasMaxLength(200);
            entity.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(d => d.FileExtension).IsRequired().HasMaxLength(20);
            entity.Property(d => d.ChecksumSha256).HasMaxLength(64);
            entity.Property(d => d.InternalNotes).HasMaxLength(4000);
            entity.Property(d => d.UploadedByUserId).HasMaxLength(450);
            entity.Property(d => d.ModifiedByUserId).HasMaxLength(450);
            entity.Property(d => d.ArchivedByUserId).HasMaxLength(450);

            entity.HasIndex(d => d.CustomerId);
            entity.HasIndex(d => d.PropertyId);
            entity.HasIndex(d => d.ProcedureId);
            entity.HasIndex(d => d.Type);
            entity.HasIndex(d => d.ConfidentialityLevel);
            entity.HasIndex(d => d.IsCustomerVisible);
            entity.HasIndex(d => d.IsArchived);
            entity.HasIndex(d => d.StorageKey).IsUnique();

            entity.HasOne(d => d.Customer)
                  .WithMany(c => c.Documents)
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Property)
                  .WithMany(p => p.Documents)
                  .HasForeignKey(d => d.PropertyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Procedure)
                  .WithMany(pr => pr.Documents)
                  .HasForeignKey(d => d.ProcedureId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            entity.HasIndex(u => u.CustomerId);
        });
    }
}

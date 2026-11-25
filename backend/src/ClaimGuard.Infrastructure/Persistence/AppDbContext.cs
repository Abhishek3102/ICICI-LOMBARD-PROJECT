using ClaimGuard.Application.Interfaces;
using ClaimGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClaimGuard.Infrastructure.Persistence;

/// <summary>
/// EF Core context over Supabase PostgreSQL. The schema is created by supabase/schema.sql
/// (run in the Supabase SQL Editor); this context maps to it with snake_case naming.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimAuditLog> AuditLogs => Set<ClaimAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Role).HasConversion(UpperEnumConverter<UserRole>()).HasMaxLength(30);
            e.Property(x => x.PartnerGstin).HasMaxLength(20);
            e.Property(x => x.PasswordHash);
        });

        modelBuilder.Entity<Policy>(e =>
        {
            e.ToTable("policies");
            e.HasKey(x => x.Id);
            e.Property(x => x.PolicyNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.PolicyNumber).IsUnique();
            e.Property(x => x.PolicyType).HasConversion(UpperEnumConverter<PolicyType>()).HasMaxLength(20);
            e.Property(x => x.SumInsured).HasColumnType("numeric(12,2)");
            e.Property(x => x.RemainingBalance).HasColumnType("numeric(12,2)");
            e.Property(x => x.Status).HasConversion(UpperEnumConverter<PolicyStatus>()).HasMaxLength(20);
            e.Property(x => x.ValidFrom).HasColumnType("date");
            e.Property(x => x.ValidTo).HasColumnType("date");
            e.HasOne(x => x.Holder)
                .WithMany(u => u.Policies)
                .HasForeignKey(x => x.HolderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Claim>(e =>
        {
            e.ToTable("claims");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClaimNumber).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.ClaimNumber).IsUnique();
            e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.Property(x => x.ClaimedAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.ApprovedAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.DocumentUrl).IsRequired();
            e.Property(x => x.Status).HasConversion(UpperEnumConverter<ClaimStatus>()).HasMaxLength(30);
            e.Property(x => x.AiRiskBand).HasConversion(UpperEnumConverter<RiskBand>()).HasMaxLength(15);
            e.Property(x => x.AiReasoning).HasColumnType("jsonb");
            e.HasIndex(x => new { x.Status, x.AiRiskBand }).HasDatabaseName("idx_claims_status_risk");
            e.HasIndex(x => x.PolicyId).HasDatabaseName("idx_claims_policy_id");
            e.HasOne(x => x.Policy)
                .WithMany()
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SubmittedByUser)
                .WithMany()
                .HasForeignKey(x => x.SubmittedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClaimAuditLog>(e =>
        {
            e.ToTable("claim_audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.ActorType).HasConversion(UpperEnumConverter<ActorType>()).HasMaxLength(20);
            e.Property(x => x.PreviousStatus).HasMaxLength(30);
            e.Property(x => x.NewStatus).HasMaxLength(30).IsRequired();
            e.Property(x => x.Remarks).IsRequired();
            e.Property(x => x.KafkaOffset).HasMaxLength(100);
            e.HasIndex(x => x.ClaimId).HasDatabaseName("idx_audit_claim_id");
            e.HasOne(x => x.Claim)
                .WithMany()
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>
    /// The blueprint schema stores enum values as UPPERCASE VARCHAR with CHECK constraints
    /// (e.g. 'SUBMITTED', 'LOW'). EF's default enum-to-string conversion would emit
    /// 'Submitted', violating those constraints, so every enum uses this converter.
    /// </summary>
    private static ValueConverter<TEnum, string> UpperEnumConverter<TEnum>() where TEnum : struct, Enum =>
        new(
            v => v.ToString().ToUpperInvariant(),
            v => Enum.Parse<TEnum>(v, true));
}

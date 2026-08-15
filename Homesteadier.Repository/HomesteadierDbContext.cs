using System;
using System.Collections.Generic;
using HomeSteadier.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository;

public partial class HomesteadierDbContext : DbContext
{
    public HomesteadierDbContext(DbContextOptions<HomesteadierDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CropType> CropTypes { get; set; }

    public virtual DbSet<GardenBed> GardenBeds { get; set; }

    public virtual DbSet<GardenBedCrop> GardenBedCrops { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CropType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("crop_types_pkey");

            entity.ToTable("crop_types");

            entity.HasIndex(e => e.Name, "ix_crop_types_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Family)
                .HasMaxLength(100)
                .HasColumnName("family");
            entity.Property(e => e.Genus)
                .HasMaxLength(100)
                .HasColumnName("genus");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.SpacingInches).HasColumnName("spacing_inches");
            entity.Property(e => e.SunlightRequirementHours)
                .HasPrecision(6, 2)
                .HasColumnName("sunlight_requirement_hours");
        });

        modelBuilder.Entity<GardenBed>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("garden_bed_pkey");

            entity.ToTable("garden_bed");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Length)
                .HasPrecision(6, 2)
                .HasColumnName("length");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.SunlightHours)
                .HasPrecision(6, 2)
                .HasColumnName("sunlight_hours");
            entity.Property(e => e.Width)
                .HasPrecision(6, 2)
                .HasColumnName("width");
        });

        modelBuilder.Entity<GardenBedCrop>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("garden_bed_crops_pkey");

            entity.ToTable("garden_bed_crops");

            entity.HasIndex(e => e.CropTypeId, "ix_garden_bed_crops_crop_type_id");

            entity.HasIndex(e => e.GardenBedId, "ix_garden_bed_crops_garden_bed_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CropTypeId).HasColumnName("crop_type_id");
            entity.Property(e => e.GardenBedId).HasColumnName("garden_bed_id");

            entity.HasOne(d => d.CropType).WithMany(p => p.GardenBedCrops)
                .HasForeignKey(d => d.CropTypeId)
                .HasConstraintName("garden_bed_crops_crop_type_id_fkey");

            entity.HasOne(d => d.GardenBed).WithMany(p => p.GardenBedCrops)
                .HasForeignKey(d => d.GardenBedId)
                .HasConstraintName("garden_bed_crops_garden_bed_id_fkey");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("password_reset_tokens_pkey");

            entity.ToTable("password_reset_tokens");

            entity.HasIndex(e => e.TokenHash, "ix_password_reset_tokens_token_hash").IsUnique();

            entity.HasIndex(e => e.UserId, "ix_password_reset_tokens_user_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("password_reset_tokens_user_id_fkey");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");

            entity.ToTable("refresh_tokens");

            entity.HasIndex(e => e.TokenHash, "ix_refresh_tokens_token_hash").IsUnique();

            entity.HasIndex(e => e.UserId, "ix_refresh_tokens_user_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.ReplacedByHash)
                .HasMaxLength(128)
                .HasColumnName("replaced_by_hash");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("refresh_tokens_user_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

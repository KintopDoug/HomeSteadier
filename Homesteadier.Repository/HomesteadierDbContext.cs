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

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "ix_users_email");

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

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");

            entity.ToTable("refresh_tokens");

            entity.HasIndex(e => e.TokenHash, "ix_refresh_tokens_token_hash").IsUnique();

            entity.HasIndex(e => e.UserId, "ix_refresh_tokens_user_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.RevokedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedByHash)
                .HasMaxLength(128)
                .HasColumnName("replaced_by_hash");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("refresh_tokens_user_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

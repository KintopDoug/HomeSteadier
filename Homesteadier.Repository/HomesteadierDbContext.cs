using HomeSteadier.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Homesteadier.Repository;

public class HomesteadierDbContext : DbContext
{
    public HomesteadierDbContext(DbContextOptions<HomesteadierDbContext> options)
        : base(options)
    {
    }


    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Email)
                .HasColumnName("email")
                .IsRequired();

            entity.Property(e => e.Password)
                .HasColumnName("password")
                .IsRequired();

            entity.Property(e => e.FirstName)
                .HasColumnName("first_name")
                .IsRequired();

            entity.Property(e => e.LastName)
                .HasColumnName("last_name")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

        });
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Econosys.Api.Models;

namespace Econosys.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Cost> Costs => Set<Cost>();
        public DbSet<Inventory> Inventories => Set<Inventory>();

        private static string ToDbString(string value) => value;
        private static string FromDbString(string? value) => value ?? string.Empty;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure IdentityUser properties
            builder.Entity<ApplicationUser>(entity => {
                entity.ToTable("AspNetUsers");
            });

            builder.Entity<IdentityRole>(entity => {
                entity.ToTable("AspNetRoles");
            });

            builder.Entity<IdentityUserRole<string>>(entity => {
                entity.ToTable("AspNetUserRoles");
            });

            builder.Entity<IdentityUserClaim<string>>(entity => {
                entity.ToTable("AspNetUserClaims");
            });

            builder.Entity<IdentityUserLogin<string>>(entity => {
                entity.ToTable("AspNetUserLogins");
            });

            builder.Entity<IdentityRoleClaim<string>>(entity => {
                entity.ToTable("AspNetRoleClaims");
            });

            builder.Entity<IdentityUserToken<string>>(entity => {
                entity.ToTable("AspNetUserTokens");
            });

            builder.Entity<Cost>(entity =>
            {
                entity.ToTable("Cost", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.Property(e => e.Name)
                    .IsRequired(false)
                    .HasConversion(
                        value => ToDbString(value),
                        value => FromDbString(value));

                entity.Property(e => e.CostTypeText)
                    .IsRequired(false)
                    .HasConversion(
                        value => ToDbString(value),
                        value => FromDbString(value));
            });

            builder.Entity<Inventory>(entity =>
            {
                entity.ToTable("Inventory", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });
        }
    }

    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

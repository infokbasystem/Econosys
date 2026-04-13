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
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<Currency> Currencies => Set<Currency>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
        public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<StockTaking> StockTakings => Set<StockTaking>();
        public DbSet<StockTakingItem> StockTakingItems => Set<StockTakingItem>();
        public DbSet<DeliveryToCustomer> DeliveryToCustomers => Set<DeliveryToCustomer>();
        public DbSet<DeliveryToStock> DeliveryToStocks => Set<DeliveryToStock>();
        public DbSet<DeliveryFromStock> DeliveryFromStocks => Set<DeliveryFromStock>();

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

            builder.Entity<Unit>(entity =>
            {
                entity.ToTable("Unit", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Currency>(entity =>
            {
                entity.ToTable("Currency", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Supplier>(entity =>
            {
                entity.ToTable("Supplier", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<SupplierOrder>(entity =>
            {
                entity.ToTable("SupplierOrder", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.SupplierOrders)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Supplier)
                    .WithMany(x => x.SupplierOrders)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PurchaseCurrency)
                    .WithMany(x => x.PurchaseSupplierOrders)
                    .HasForeignKey(x => x.PurchaseCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Inventory)
                    .WithMany(x => x.SupplierOrders)
                    .HasForeignKey(x => x.InventoryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Unit)
                    .WithMany(x => x.SupplierOrders)
                    .HasForeignKey(x => x.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CustomerOrder>(entity =>
            {
                entity.ToTable("CustomerOrder", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.CustomerOrders)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SalesCurrency)
                    .WithMany(x => x.SalesCustomerOrders)
                    .HasForeignKey(x => x.SalesCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupplierPricePerEurPalletCurrency)
                    .WithMany(x => x.SupplierPricePerEurPalletCustomerOrders)
                    .HasForeignKey(x => x.SupplierPricePerEurPalletCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Unit)
                    .WithMany(x => x.CustomerOrders)
                    .HasForeignKey(x => x.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany(x => x.CustomerOrders)
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customer", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Currency)
                    .WithMany(x => x.Customers)
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<StockTakingItem>(entity =>
            {
                entity.ToTable("StockTakingItem", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.StockTaking)
                    .WithMany()
                    .HasForeignKey(x => x.StockTakingId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<DeliveryToCustomer>(entity =>
            {
                entity.ToTable("DeliveryToCustomer", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ParentDelivery)
                    .WithMany(x => x.ChildDeliveries)
                    .HasForeignKey(x => x.ParentDeliveryId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<DeliveryToStock>(entity =>
            {
                entity.ToTable("DeliveryToStock", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Inventory)
                    .WithMany()
                    .HasForeignKey(x => x.InventoryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ParentDelivery)
                    .WithMany(x => x.ChildDeliveries)
                    .HasForeignKey(x => x.ParentDeliveryId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<DeliveryFromStock>(entity =>
            {
                entity.ToTable("DeliveryFromStock", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Inventory)
                    .WithMany()
                    .HasForeignKey(x => x.InventoryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.DeliveryToStock)
                    .WithMany()
                    .HasForeignKey(x => x.DeliveryToStockId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ParentDelivery)
                    .WithMany(x => x.ChildDeliveries)
                    .HasForeignKey(x => x.ParentDeliveryId)
                    .OnDelete(DeleteBehavior.NoAction);
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

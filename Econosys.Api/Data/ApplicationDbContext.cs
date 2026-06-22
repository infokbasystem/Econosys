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
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<AccountType> AccountTypes => Set<AccountType>();
        public DbSet<PriceType> PriceTypes => Set<PriceType>();
        public DbSet<Language> Languages => Set<Language>();
        public DbSet<TranslationItem> TranslationItems => Set<TranslationItem>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<SupplierOrder> SupplierOrders => Set<SupplierOrder>();
        public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<StockTaking> StockTakings => Set<StockTaking>();
        public DbSet<StockTakingItem> StockTakingItems => Set<StockTakingItem>();
        public DbSet<DeliveryToCustomer> DeliveryToCustomers => Set<DeliveryToCustomer>();
        public DbSet<DeliveryToStock> DeliveryToStocks => Set<DeliveryToStock>();
        public DbSet<DeliveryFromStock> DeliveryFromStocks => Set<DeliveryFromStock>();
        public DbSet<OrderCost> OrderCosts => Set<OrderCost>();
        public DbSet<CustomerOrderEditionAdjustment> CustomerOrderEditionAdjustments => Set<CustomerOrderEditionAdjustment>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceRow> InvoiceRows => Set<InvoiceRow>();
        public DbSet<InvoiceAccountRow> InvoiceAccountRows => Set<InvoiceAccountRow>();
        public DbSet<LegacyUser> LegacyUsers => Set<LegacyUser>();
        public DbSet<PackagingFeeCategory> PackagingFeeCategories => Set<PackagingFeeCategory>();
        public DbSet<Material> Materials => Set<Material>();
        public DbSet<Construction> Constructions => Set<Construction>();
        public DbSet<Varnish> Varnishes => Set<Varnish>();
        public DbSet<TermOfDelivery> TermsOfDelivery => Set<TermOfDelivery>();
        public DbSet<TermOfPayment> TermsOfPayment => Set<TermOfPayment>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<PalletType> PalletTypes => Set<PalletType>();
        public DbSet<PalletFormat> PalletFormats => Set<PalletFormat>();
        public DbSet<PalletFormatPrice> PalletFormatPrices => Set<PalletFormatPrice>();
        public DbSet<Calculation> Calculations => Set<Calculation>();
        public DbSet<Inquiry> Inquiries => Set<Inquiry>();
        public DbSet<InquiryRecipient> InquiryRecipients => Set<InquiryRecipient>();
        public DbSet<CalculationRow> CalculationRows => Set<CalculationRow>();
        public DbSet<CalculationRowCost> CalculationRowCosts => Set<CalculationRowCost>();
        public DbSet<Deviation> Deviations => Set<Deviation>();
        public DbSet<DeviationCost> DeviationCosts => Set<DeviationCost>();
        public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
        public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
        public DbSet<TransportOrder> TransportOrders => Set<TransportOrder>();
        public DbSet<CallOff> CallOffs => Set<CallOff>();
        public DbSet<CallOffDelivery> CallOffDeliveries => Set<CallOffDelivery>();
        public DbSet<Shipper> Shippers => Set<Shipper>();
        public DbSet<CustomerDeliveryAddress> CustomerDeliveryAddresses => Set<CustomerDeliveryAddress>();
        public DbSet<CustomerContactPerson> CustomerContactPersons => Set<CustomerContactPerson>();
        public DbSet<SupplierContactPerson> SupplierContactPersons => Set<SupplierContactPerson>();
        public DbSet<DocumentFileRelation> DocumentFileRelations => Set<DocumentFileRelation>();
        public DbSet<ImprovementProposition> ImprovementPropositions => Set<ImprovementProposition>();
        public DbSet<CompanyInfo> CompanyInfos => Set<CompanyInfo>();
        public DbSet<Setting> Settings => Set<Setting>();
        public DbSet<MailSetting> MailSettings => Set<MailSetting>();
        public DbSet<MailText> MailTexts => Set<MailText>();
        public DbSet<CalculationVariable> CalculationVariables => Set<CalculationVariable>();
        public DbSet<BudgetMonthDistribution> BudgetMonthDistributions => Set<BudgetMonthDistribution>();
        public DbSet<Quotation> Quotations => Set<Quotation>();
        public DbSet<QuotationRow> QuotationRows => Set<QuotationRow>();
        public DbSet<InquiryRow> InquiryRows => Set<InquiryRow>();

        private static string ToDbString(string value) => value;
        private static string FromDbString(string? value) => value ?? string.Empty;
        private static ImprovementPropositionAreaCode ParseImprovementPropositionAreaCode(string? value)
            => Enum.TryParse<ImprovementPropositionAreaCode>(value, true, out var parsed)
                ? parsed
                : ImprovementPropositionAreaCode.SALES;
        private static ImprovementPropositionStatusCode ParseImprovementPropositionStatus(string? value)
            => Enum.TryParse<ImprovementPropositionStatusCode>(value, true, out var parsed)
                ? parsed
                : ImprovementPropositionStatusCode.NEW;

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

            builder.Entity<Account>(entity =>
            {
                entity.ToTable("Account", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.AccountType)
                    .WithMany(x => x.Accounts)
                    .HasForeignKey(x => x.AccountTypeId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<AccountType>(entity =>
            {
                entity.ToTable("AccountType", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<PriceType>(entity =>
            {
                entity.ToTable("PriceType", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Account)
                    .WithMany(x => x.PriceTypes)
                    .HasForeignKey(x => x.AccountId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Language>(entity =>
            {
                entity.ToTable("Lang", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<TranslationItem>(entity =>
            {
                entity.ToTable("TranslationItem", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Supplier>(entity =>
            {
                entity.ToTable("Supplier", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Language)
                    .WithMany(x => x.Suppliers)
                    .HasForeignKey(x => x.LanguageId)
                    .OnDelete(DeleteBehavior.NoAction);
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

                entity.HasOne(x => x.ResponsibleUser)
                    .WithMany(x => x.ResponsibleForCustomerOrders)
                    .HasForeignKey(x => x.ResponsibleUserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customer", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Currency)
                    .WithMany(x => x.Customers)
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Language)
                    .WithMany(x => x.Customers)
                    .HasForeignKey(x => x.LanguageId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ResponsibleUser)
                    .WithMany(x => x.ResponsibleForCustomers)
                    .HasForeignKey(x => x.ResponsibleUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupportEmployee)
                    .WithMany(x => x.SupportingCustomers)
                    .HasForeignKey(x => x.SupportEmployeeId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<StockTaking>(entity =>
            {
                entity.ToTable("StockTaking", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<StockTakingItem>(entity =>
            {
                entity.ToTable("StockTakingItem", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.StockTaking)
                    .WithMany()
                    .HasForeignKey(x => x.StockTakingId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany(x => x.StockTakingItems)
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<DeliveryToCustomer>(entity =>
            {
                entity.ToTable("DeliveryToCustomer", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany(x => x.DeliveryToCustomers)
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany(x => x.DeliveryToCustomers)
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
                    .WithMany(x => x.DeliveryToStocks)
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
                    .WithMany(x => x.DeliveryFromStocks)
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

            builder.Entity<OrderCost>(entity =>
            {
                entity.ToTable("OrderCost", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany(x => x.OrderCosts)
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany(x => x.OrderCosts)
                    .HasForeignKey(x => x.CustomerOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CalculationRow)
                    .WithMany(x => x.OrderCosts)
                    .HasForeignKey(x => x.CalculationRowId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Cost)
                    .WithMany(x => x.OrderCosts)
                    .HasForeignKey(x => x.CostId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Supplier)
                    .WithMany(x => x.OrderCosts)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.InPriceCurrency)
                    .WithMany(x => x.InPriceOrderCosts)
                    .HasForeignKey(x => x.InPriceCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedOrderCosts)
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.EditedByUser)
                    .WithMany(x => x.EditedOrderCosts)
                    .HasForeignKey(x => x.EditedById)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CustomerOrderEditionAdjustment>(entity =>
            {
                entity.ToTable("CustomerOrderEditionAdjustment", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany(x => x.CustomerOrderEditionAdjustments)
                    .HasForeignKey(x => x.CustomerOrderId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Invoice>(entity =>
            {
                entity.ToTable("Invoice", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedInvoices)
                    .HasForeignKey(x => x.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.EditedByUser)
                    .WithMany(x => x.EditedInvoices)
                    .HasForeignKey(x => x.EditedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.Invoices)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SalesCurrency)
                    .WithMany(x => x.SalesInvoices)
                    .HasForeignKey(x => x.SalesCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Unit)
                    .WithMany(x => x.Invoices)
                    .HasForeignKey(x => x.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Language)
                    .WithMany(x => x.Invoices)
                    .HasForeignKey(x => x.LanguageId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<InvoiceRow>(entity =>
            {
                entity.ToTable("InvoiceRow", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Invoice)
                    .WithMany(x => x.InvoiceRows)
                    .HasForeignKey(x => x.InvoiceId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.DeliveryToCustomer)
                    .WithMany(x => x.InvoiceRows)
                    .HasForeignKey(x => x.DeliveryToCustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.DeliveryFromStock)
                    .WithMany(x => x.InvoiceRows)
                    .HasForeignKey(x => x.DeliveryFromStockId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PriceType)
                    .WithMany(x => x.InvoiceRows)
                    .HasForeignKey(x => x.PriceTypeId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Unit)
                    .WithMany(x => x.InvoiceRows)
                    .HasForeignKey(x => x.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.OrderCost)
                    .WithMany(x => x.InvoiceRows)
                    .HasForeignKey(x => x.OrderCostId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<InvoiceAccountRow>(entity =>
            {
                entity.ToTable("InvoiceAccountRow", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Invoice)
                    .WithMany(x => x.InvoiceAccountRows)
                    .HasForeignKey(x => x.InvoiceId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<LegacyUser>(entity =>
            {
                entity.ToTable("User", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<PackagingFeeCategory>(entity =>
            {
                entity.ToTable("PackagingFeeCagetory", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Material>(entity =>
            {
                entity.ToTable("Material", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.PackagingFeeCategory)
                    .WithMany(x => x.Materials)
                    .HasForeignKey(x => x.PackagingFeeCategoryId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Construction>(entity =>
            {
                entity.ToTable("Construction", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Varnish>(entity =>
            {
                entity.ToTable("Varnish", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<TermOfDelivery>(entity =>
            {
                entity.ToTable("TermsOfDelivery", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<TermOfPayment>(entity =>
            {
                entity.ToTable("TermsOfPayment", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Product>(entity =>
            {
                entity.ToTable("Product", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Material)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.MaterialId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Construction)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.ConstructionId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Varnish)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.VarnishId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<PalletType>(entity =>
            {
                entity.ToTable("PalletType", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<PalletFormat>(entity =>
            {
                entity.ToTable("PalletFormat", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.PalletType)
                    .WithMany(x => x.PalletFormats)
                    .HasForeignKey(x => x.PalletTypeId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<PalletFormatPrice>(entity =>
            {
                entity.ToTable("PalletFormatPrice", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.PalletFormatPrices)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Supplier)
                    .WithMany(x => x.PalletFormatPrices)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PalletFormat)
                    .WithMany(x => x.PalletFormatPrices)
                    .HasForeignKey(x => x.PalletFormatId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Calculation>(entity =>
            {
                entity.ToTable("Calculation", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.Calculations)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Supplier)
                    .WithMany(x => x.Calculations)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.Calculations)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Unit)
                    .WithMany(x => x.Calculations)
                    .HasForeignKey(x => x.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PalletFormat)
                    .WithMany(x => x.Calculations)
                    .HasForeignKey(x => x.PalletFormatId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PurchaseCurrency)
                    .WithMany(x => x.PurchaseCalculations)
                    .HasForeignKey(x => x.PurchaseCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SalesCurrency)
                    .WithMany(x => x.SalesCalculations)
                    .HasForeignKey(x => x.SalesCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Inquiry>(entity =>
            {
                entity.ToTable("Inquiry", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Calculation)
                    .WithMany()
                    .HasForeignKey(x => x.CalculationId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SelectedInquiryRecipient)
                    .WithMany()
                    .HasForeignKey(x => x.SelectedInquiryRecipientId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerDeliveryAddress)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerDeliveryAddressId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedInquiries)
                    .HasForeignKey(x => x.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.EditedByUser)
                    .WithMany(x => x.EditedInquiries)
                    .HasForeignKey(x => x.EditedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<InquiryRow>(entity =>
            {
                entity.ToTable("InquiryRow", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Inquiry)
                    .WithMany(x => x.InquiryRows)
                    .HasForeignKey(x => x.InquiryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CalculationRow)
                    .WithMany()
                    .HasForeignKey(x => x.CalculationRowId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<InquiryRecipient>(entity =>
            {
                entity.ToTable("InquiryRecipient", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Inquiry)
                    .WithMany(x => x.InquiryRecipients)
                    .HasForeignKey(x => x.InquiryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Supplier)
                    .WithMany(x => x.InquiryRecipients)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Language)
                    .WithMany(x => x.InquiryRecipients)
                    .HasForeignKey(x => x.LanguageId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Quotation>(entity =>
            {
                entity.ToTable("Quotation", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Calculation)
                    .WithMany(x => x.Quotations)
                    .HasForeignKey(x => x.CalculationId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Inquiry)
                    .WithMany(x => x.Quotations)
                    .HasForeignKey(x => x.InquiryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.Quotations)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SalesCurrency)
                    .WithMany(x => x.SalesQuotations)
                    .HasForeignKey(x => x.SalesCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Unit)
                    .WithMany(x => x.Quotations)
                    .HasForeignKey(x => x.UnitId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedQuotations)
                    .HasForeignKey(x => x.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.EditedByUser)
                    .WithMany(x => x.EditedQuotations)
                    .HasForeignKey(x => x.EditedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PurchaseCurrency)
                    .WithMany(x => x.PurchaseQuotations)
                    .HasForeignKey(x => x.PurchaseCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerDeliveryAddress)
                    .WithMany(x => x.Quotations)
                    .HasForeignKey(x => x.CustomerDeliveryAddressId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.PalletFormat)
                    .WithMany(x => x.Quotations)
                    .HasForeignKey(x => x.PalletFormatId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<QuotationRow>(entity =>
            {
                entity.ToTable("QuotationRow", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Quotation)
                    .WithMany(x => x.QuotationRows)
                    .HasForeignKey(x => x.QuotationId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CalculationRow)
                    .WithMany(x => x.QuotationRows)
                    .HasForeignKey(x => x.CalculationRowId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CalculationRow>(entity =>
            {
                entity.ToTable("CalculationRow", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Calculation)
                    .WithMany(x => x.CalculationRows)
                    .HasForeignKey(x => x.CalculationId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(x => x.CustomerOrders)
                    .WithOne(x => x.SelectedCalculationRow)
                    .HasForeignKey(x => x.SelectedCalculationRowId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(x => x.SupplierOrders)
                    .WithOne(x => x.SelectedCalculationRow)
                    .HasForeignKey(x => x.SelectedCalculationRowId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CalculationRowCost>(entity =>
            {
                entity.ToTable("CalculationRowCost", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CalculationRow)
                    .WithMany(x => x.CalculationRowCosts)
                    .HasForeignKey(x => x.CalculationRowId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Cost)
                    .WithMany(x => x.CalculationRowCosts)
                    .HasForeignKey(x => x.CostId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.InPriceCurrency)
                    .WithMany(x => x.InPriceCalculationRowCosts)
                    .HasForeignKey(x => x.InPriceCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Deviation>(entity =>
            {
                entity.ToTable("Deviation", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany(x => x.Deviations)
                    .HasForeignKey(x => x.CustomerOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedDeviations)
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.EditedByUser)
                    .WithMany(x => x.EditedDeviations)
                    .HasForeignKey(x => x.EditedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.ResponsibleUser)
                    .WithMany(x => x.ResponsibleForDeviations)
                    .HasForeignKey(x => x.ResponsibleUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Supplier)
                    .WithMany(x => x.Deviations)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.Deviations)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.FreightCostCurrency)
                    .WithMany(x => x.FreightCostDeviations)
                    .HasForeignKey(x => x.FreightCostCurrencyId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<DeviationCost>(entity =>
            {
                entity.ToTable("DeviationCost", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Deviation)
                    .WithMany(x => x.DeviationCosts)
                    .HasForeignKey(x => x.DeviationId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<ImprovementProposition>(entity =>
            {
                entity.ToTable("ImprovementProposition");

                entity.Property(x => x.AreaCode)
                    .HasConversion(
                        value => value.ToString(),
                        value => ParseImprovementPropositionAreaCode(value))
                    .HasMaxLength(30)
                    .IsUnicode(false)
                    .HasColumnType("varchar(30)");

                entity.Property(x => x.StatusCode)
                    .HasConversion(
                        value => value.ToString(),
                        value => ParseImprovementPropositionStatus(value))
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnType("varchar(20)");
            });

            builder.Entity<CompanyInfo>(entity =>
            {
                entity.ToTable("CompanyInfo", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Setting>(entity =>
            {
                entity.ToTable("Settings", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CompanyInfo)
                    .WithMany(x => x.Settings)
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<MailSetting>(entity =>
            {
                entity.ToTable("MailSetting", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<MailText>(entity =>
            {
                entity.ToTable("MailText", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<DocumentType>(entity =>
            {
                entity.ToTable("DocumentType", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<TransportOrder>(entity =>
            {
                entity.ToTable("TransportOrder", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<Shipper>(entity =>
            {
                entity.ToTable("Shipper", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<CustomerDeliveryAddress>(entity =>
            {
                entity.ToTable("CustomerDeliveryAddress", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.DeliveryAddresses)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CustomerContactPerson>(entity =>
            {
                entity.ToTable("CustomerContactPerson", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Customer)
                    .WithMany(x => x.CustomerContactPersons)
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

                    builder.Entity<SupplierContactPerson>(entity =>
                    {
                    entity.ToTable("SupplierContactPerson", tableBuilder => tableBuilder.ExcludeFromMigrations());

                    entity.HasOne(x => x.Supplier)
                        .WithMany(x => x.SupplierContactPersons)
                        .HasForeignKey(x => x.SupplierId)
                        .OnDelete(DeleteBehavior.NoAction);
                    });

            builder.Entity<CallOff>(entity =>
            {
                entity.ToTable("CallOff", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.Shipper)
                    .WithMany(x => x.CallOffs)
                    .HasForeignKey(x => x.ShipperId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedCallOffs)
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerDeliveryAddress)
                    .WithMany(x => x.CallOffs)
                    .HasForeignKey(x => x.CustomerDeliveryAddressId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CallOffDelivery>(entity =>
            {
                entity.ToTable("CallOffDelivery", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.CallOff)
                    .WithMany(x => x.CallOffDeliveries)
                    .HasForeignKey(x => x.CallOffId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.DeliveryFromStock)
                    .WithMany(x => x.CallOffDeliveries)
                    .HasForeignKey(x => x.DeliveryFromStockId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<DocumentFile>(entity =>
            {
                entity.ToTable("DocumentFile", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.DocumentType)
                    .WithMany(x => x.DocumentFiles)
                    .HasForeignKey(x => x.DocumentTypeId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany(x => x.DocumentFiles)
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.TransportOrder)
                    .WithMany(x => x.DocumentFiles)
                    .HasForeignKey(x => x.TransportOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany(x => x.CreatedDocumentFiles)
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CallOff)
                    .WithMany(x => x.DocumentFiles)
                    .HasForeignKey(x => x.CallOffId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Deviation)
                    .WithMany(x => x.DocumentFiles)
                    .HasForeignKey(x => x.DeviationId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.DocumentFiles)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CalculationVariable>(entity =>
            {
                entity.ToTable("CalculationVariable", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<BudgetMonthDistribution>(entity =>
            {
                entity.ToTable("BudgetMonthDistribution", tableBuilder => tableBuilder.ExcludeFromMigrations());
            });

            builder.Entity<DocumentFileRelation>(entity =>
            {
                entity.ToTable("DocumentFileRelation", tableBuilder => tableBuilder.ExcludeFromMigrations());

                entity.HasOne(x => x.DocumentFile)
                    .WithMany(x => x.DocumentFileRelations)
                    .HasForeignKey(x => x.DocumentFileId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.SupplierOrder)
                    .WithMany(x => x.DocumentFileRelations)
                    .HasForeignKey(x => x.SupplierOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.CustomerOrder)
                    .WithMany(x => x.DocumentFileRelations)
                    .HasForeignKey(x => x.CustomerOrderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(x => x.Deviation)
                    .WithMany(x => x.DocumentFileRelations)
                    .HasForeignKey(x => x.DeviationId)
                    .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }

    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; } = SwedishTime.Now;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

using Econosys.Api.DTOs;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Econosys.Api.Migrations;
using Econosys.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Econosys.Api.Tests;

public class CalculationDetailTests
{
    [Fact]
    public void ArchiveMigrationOnlyAddsTheRequiredFlagWithAnUnarchivedDefault()
    {
        var migration = new AddCalculationIsArchived();
        var add = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("Calculation", add.Table);
        Assert.Equal("IsArchived", add.Name);
        Assert.Equal("bit", add.ColumnType);
        Assert.False(add.IsNullable);
        Assert.Equal(false, add.DefaultValue);
        var drop = Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal(add.Table, drop.Table);
        Assert.Equal(add.Name, drop.Name);
    }

    [Fact]
    public void ArchiveFlagMapsToTheRequiredDatabaseColumn()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=localhost;Database=ModelMappingOnly;Integrated Security=true")
                .Options);
        var property = context.Model.FindEntityType(typeof(Calculation))!
            .FindProperty(nameof(Calculation.IsArchived))!;

        Assert.Equal("IsArchived", property.GetColumnName());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void NewCalculationsAreNotArchived()
    {
        var calculation = new Calculation();

        Assert.False(calculation.IsArchived);
        Assert.False(CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()).IsArchived);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveStatusIsSavedAndReloadedWithoutChangingRowArchiveStatus(bool isArchived)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var calculation = SavedCalculation;
        calculation.IsArchived = !isArchived;
        calculation.CalculationRows.Add(new CalculationRow { Id = 10, Archived = true });
        context.Calculations.Add(calculation);
        await context.SaveChangesAsync();

        CalculationUpdateMapper.Apply(calculation, new UpdateCalculationRequest
        {
            IsArchived = isArchived,
        }, editedBy: 8, new DateTime(2026, 10, 21, 12, 0, 0));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.Calculations.Include(item => item.CalculationRows)
            .SingleAsync(item => item.Id == calculation.Id);
        var details = CalculationDetailMapper.Map(reloaded, new Dictionary<int, string?>());
        Assert.Equal(isArchived, reloaded.IsArchived);
        Assert.Equal(isArchived, details.IsArchived);
        Assert.False(details.Rows.Single(row => row.Id == 9).Archived);
        Assert.True(details.Rows.Single(row => row.Id == 10).Archived);
    }

    [Fact]
    public void CalculationAndRowsMapTheExistingRequiredCompanyColumn()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=localhost;Database=ModelMappingOnly;Integrated Security=true")
                .Options);

        foreach (var entityType in new[] { typeof(Calculation), typeof(CalculationRow) })
        {
            var entity = context.Model.FindEntityType(entityType)!;
            var property = entity.FindProperty(nameof(Calculation.CompanyId));

            Assert.NotNull(property);
            Assert.False(property.IsNullable);
            Assert.Equal("CompanyId", property.GetColumnName());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void NewCalculationRowsInheritTheCalculationCompany(int companyId)
    {
        var calculation = new Calculation { CompanyId = companyId };
        var row = new CalculationRow();

        CalculationUpdateMapper.ApplyRow(
            row, new UpdateCalculationRowRequest { Edition = 1000 },
            calculation, unitMultiplier: 1000, salesDecimals: 2);

        Assert.Equal(companyId, row.CompanyId);
    }

    private static Calculation SavedCalculation => new()
    {
        Id = 5,
        SupplierFactoryId = 17,
        CustomerDeliveryAddressId = 23,
        LockOutPrice = true,
        CurrencyRate = 2,
        PurchaseCurrencyRate = 10,
        SalesCurrencyRate = 5,
        NrOfCalcDecimalsSales = 2,
        Unit = new Unit { Multiplicator = 1000 },
        CalculationRows = [new CalculationRow
        {
            Id = 9, Edition = 2000, PurchasePrice = 10, Freight = 1,
            Other = 2, Storage = 3, PackagingCost = 4, SalesPrice = 40, PercentAddition = 77,
        }],
    };

    [Fact]
    public void LoadingDetailsPreservesSavedSalesAndDoesNotMutateEntity()
    {
        var calculation = SavedCalculation;
        var result = CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>());
        var row = Assert.Single(result.Rows);
        Assert.Equal(17, result.SupplierFactoryId);
        Assert.Equal(23, result.CustomerDeliveryAddressId);
        Assert.Equal(40d, row.SalesPrice);
        Assert.Equal(77d, row.PercentAddition);
        Assert.Equal(30m, row.TotalCost);
        Assert.Equal(50m, row.Contribution);
        Assert.Equal(100m, row.TotalContribution);
        Assert.Equal(40d, calculation.CalculationRows.Single().SalesPrice);
    }

    [Fact]
    public void ArchivedRowsUseSavedExchangeRatesInsteadOfCurrentHeaderRates()
    {
        var calculation = SavedCalculation;
        var savedRow = calculation.CalculationRows.Single();
        savedRow.Archived = true;
        savedRow.ArchivedPurchaseCurrencyRate = 6;
        savedRow.ArchivedSalesCurrencyRate = 2;
        var row = Assert.Single(CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()).Rows);
        Assert.Equal(30m, row.ConvertedPurchasePrice);
        Assert.Equal(40m, row.TotalCost);
        Assert.Equal(0m, row.Contribution);
        Assert.True(row.Archived);
    }

    [Fact]
    public void MissingUnitDoesNotInventTotalContribution()
    {
        var calculation = SavedCalculation;
        calculation.Unit = null;
        var row = Assert.Single(CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()).Rows);
        Assert.Null(row.TotalContribution);
    }

    [Fact]
    public void ProductVarnishInformationIsIncludedInCalculationDetails()
    {
        var calculation = SavedCalculation;
        calculation.Product = new Product
        {
            Varnish = new Varnish { Name = "Övrig" },
            VarnishOther = "Mattlack",
        };

        var product = CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()).Product!;

        Assert.Equal("Övrig", product.Varnish);
        Assert.Equal("Mattlack", product.VarnishOther);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 2)]
    public void AuditDatesPreserveSwedishWallClockAndResolveUserNames(int month, int offsetHours)
    {
        var calculation = SavedCalculation;
        calculation.CreatedAt = new DateTime(2026, month, 9, 12, 0, 0);
        calculation.CreatedBy = 12;
        var dto = CalculationDetailMapper.Map(calculation, new Dictionary<int, string?> { [12] = "Test" });
        Assert.Equal(TimeSpan.FromHours(offsetHours), dto.CreatedAt!.Value.Offset);
        Assert.Equal(12, dto.CreatedAt.Value.Hour);
        Assert.Equal("Test", dto.CreatedByName);
    }

    [Fact]
    public void MissingRatesDoNotProduceMisleadingComputedPrices()
    {
        var calculation = SavedCalculation;
        calculation.CurrencyRate = null;
        var row = Assert.Single(CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()).Rows);
        Assert.Null(row.TotalCost);
        Assert.Null(row.Contribution);
    }

    [Fact]
    public void CalculationHeaderUpdateDoesNotModifyRowsOrApprovalState()
    {
        var calculation = new Calculation
        {
            CustomerId = 3,
            SupplierId = 5,
            EditedBy = 4,
            EditedAt = new DateTime(2025, 1, 1),
            FreightApprovedBy = "Existing approver",
            FreightApprovedDateTime = new DateTime(2025, 2, 1),
            CalculationRows = [new CalculationRow { Id = 9, SalesPrice = 125, Archived = true }],
        };
        var editedAt = new DateTime(2026, 10, 9, 12, 30, 0);

        CalculationUpdateMapper.Apply(calculation, new UpdateCalculationRequest
        {
            CustomerId = 7,
            SupplierId = 9,
            SupplierFactoryId = 17,
            CustomerDeliveryAddressId = 23,
            UnitId = 2,
            PurchaseCurrencyId = 3,
            SalesCurrencyId = 4,
            CalculationFollowUp = "Updated note",
            FreightPerPallet = 12.5,
        }, editedBy: 8, editedAt);

        var row = Assert.Single(calculation.CalculationRows);
        Assert.Equal(7, calculation.CustomerId);
        Assert.Equal(9, calculation.SupplierId);
        Assert.Equal(17, calculation.SupplierFactoryId);
        Assert.Equal(23, calculation.CustomerDeliveryAddressId);
        Assert.Equal(2, calculation.UnitId);
        Assert.Equal(3, calculation.PurchaseCurrencyId);
        Assert.Equal(4, calculation.SalesCurrencyId);
        Assert.Equal("Updated note", calculation.CalculationFollowUp);
        Assert.Equal(12.5, calculation.FreightPerPallet);
        Assert.Equal(8, calculation.EditedBy);
        Assert.Equal(editedAt, calculation.EditedAt);
        Assert.Equal(125, row.SalesPrice);
        Assert.True(row.Archived);
        Assert.Equal("Existing approver", calculation.FreightApprovedBy);
        Assert.Equal(new DateTime(2025, 2, 1), calculation.FreightApprovedDateTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(15)]
    public void ApprovalFlagsStampOnlyRequestedSectionsAndRoundTripInDetails(int flags)
    {
        var previousTime = new DateTime(2025, 2, 1);
        var approvedAt = new DateTime(2026, 10, 16, 12, 30, 0);
        var calculation = new Calculation
        {
            PackagingApprovedBy = "Previous",
            PackagingApprovedDateTime = previousTime,
            FreightApprovedBy = "Previous",
            FreightApprovedDateTime = previousTime,
            OtherCostApprovedBy = "Previous",
            OtherCostApprovedDateTime = previousTime,
            StorageApprovedBy = "Previous",
            StorageApprovedDateTime = previousTime,
        };
        CalculationUpdateMapper.ApplyApprovals(calculation, new UpdateCalculationRequest
        {
            ApprovePackaging = (flags & 1) != 0,
            ApproveFreight = (flags & 2) != 0,
            ApproveOtherCosts = (flags & 4) != 0,
            ApproveStorage = (flags & 8) != 0,
        }, "Current user", approvedAt);

        var approvals = CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()).Approvals;
        var components = new[] { "packaging", "freight", "other", "storage" };
        for (var index = 0; index < components.Length; index++)
        {
            var changed = (flags & (1 << index)) != 0;
            var approval = Assert.Single(approvals, item => item.Component == components[index]);
            Assert.Equal(changed ? "Current user" : "Previous", approval.ApprovedBy);
            Assert.Equal(changed ? approvedAt : previousTime, approval.ApprovedAt!.Value.DateTime);
        }
    }

    [Fact]
    public void CalculationRowUpdateRecalculatesSalesPriceWhenLocked()
    {
        var calculation = new Calculation
        {
            CurrencyRate = 2,
            SalesCurrencyRate = 5,
            LockOutPrice = true,
        };
        var row = new CalculationRow();

        CalculationUpdateMapper.ApplyRow(row, new UpdateCalculationRowRequest
        {
            Edition = 1000,
            PurchasePrice = 10,
            Freight = 1,
            Other = 2,
            Storage = 3,
            PackagingCost = 4,
            PercentAddition = 20,
            SalesPrice = 999,
            NrOfPallets = 2.5,
            NrOfVehicles = 1,
            PalletCalcFactor = 1.25m,
            FreightDomesticPerPallet = 3,
            FreightInternationalPerPallet = 4,
            FreightPerVehicle = 500m,
            FreightUnloading = 25,
            FreightTotal = 575,
            FreightPerUnit = 57.5m,
            PreCalcAdditionPercent = 10m,
            CalcPalletPriceUsed = true,
            CalcFtlPriceUsed = true,
            CustomerDeliveryAddressId = 77,
            StorageNrOfPallets = 4,
            StorageMonths = 2.5,
            Info = "Print on blue stock",
        }, calculation, unitMultiplier: 1000, salesDecimals: 2);

        Assert.Equal(30d, row.PurchasePrice * calculation.CurrencyRate + row.Freight + row.Other + row.Storage + (double)(row.PackagingCost ?? 0));
        Assert.Equal(36d, row.SalesPrice);
        Assert.Equal(20d, row.PercentAddition);
        Assert.Equal(2.5, row.NrOfPalletsPopup);
        Assert.Equal(1d, row.NrOfVehiclesPopup);
        Assert.Equal(1.25m, row.PalletCalcFactor);
        Assert.Equal(3d, row.FreightCostPerPalletDomesticPopup);
        Assert.Equal(4d, row.FreightCostPerPalletInternationalPopup);
        Assert.Equal(500m, row.FreightCostPerFtlInternationalPopup);
        Assert.Equal(25d, row.FreightUnloadingPopup);
        Assert.Equal(575d, row.FreightPopup);
        Assert.Equal(57.5m, row.FreightTotalPopupPerUnit);
        Assert.Equal(10m, row.PreCalcAdditionPercent);
        Assert.True(row.CalcPalletPriceUsed);
        Assert.True(row.CalcFtlPriceUsed);
        Assert.Equal(77, row.CustomerDeliveryAddressId);
        Assert.Equal(4, row.StorageNrOfPallets);
        Assert.Equal(2.5, row.NrOfStorageMonths);
        Assert.Equal("Print on blue stock", row.Info);
    }
}
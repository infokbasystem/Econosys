using Econosys.Api.Data;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Econosys.Api.Controllers;
using Econosys.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace Econosys.Api.Tests;

public class DocumentProductSnapshotTests
{
    [Fact]
    public async Task CustomerOrderCreationUsesCurrentCustomerLanguageProduct()
    {
        await using var db = CreateContext();
        await Seed(db);
        var controller = new CustomerOrdersController(db, NullLogger<CustomerOrdersController>.Instance, new TestUser());
        var result = await controller.Create(new CreateCustomerOrderRequest
        {
            CalculationId = 10, CustomerId = 1, Product = "Outdated", Material = "Outdated"
        });
        var saved = Assert.IsType<CustomerOrderDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("Product", saved.Product);
        Assert.Equal("English material", saved.Material);
        Assert.Equal("English varnish, extra", saved.Varnish);
        Assert.Equal("External", saved.ProductMessage);
    }

    [Theory]
    [InlineData("inquiries", "French material", "French varnish")]
    [InlineData("quotations", "English material", "English varnish")]
    [InlineData("customerorders", "English material", "English varnish")]
    [InlineData("supplierorders", "German material", "German varnish")]
    public async Task ComparisonAndRefreshUseTargetLanguageAndPreserveUnrelatedFields(
        string type, string material, string varnish)
    {
        await using var db = CreateContext();
        await Seed(db);
        var service = new DocumentProductSnapshotService(db);

        var comparison = await service.GetAsync(type, 1, 1, default);
        Assert.NotNull(comparison);
        Assert.Equal("4 + legacy varnish", comparison.Saved.Color);
        Assert.Null(comparison.Saved.Varnish);
        Assert.Equal(material, comparison.Current.Material);
        Assert.Equal(varnish + ", extra", comparison.Current.Varnish);
        Assert.Equal("4", comparison.Current.Color);
        Assert.Equal("Offset, Flexo", comparison.Current.PrintMethod);
        Assert.Equal("Internal", comparison.Current.ProductNoteInternal);
        Assert.Equal("External", comparison.Current.ProductNoteExternal);

        await Assert.ThrowsAsync<DocumentProductConflictException>(
            () => service.RefreshAsync(type, 1, 1, 9, comparison.Edited!.Value.AddSeconds(-1), default));
        var refreshed = await service.RefreshAsync(type, 1, 1, 9, comparison.Edited, default);
        Assert.Equal(refreshed!.Current, refreshed.Saved);
        db.ChangeTracker.Clear();
        var reloaded = await service.GetAsync(type, 1, 1, default);
        Assert.Equal(refreshed.Current, reloaded!.Saved);
        Assert.Equal("Keep order message", (await db.SupplierOrders.SingleAsync()).Message);
        Assert.Null(await service.GetAsync(type, 1, 2, default));
    }

    [Fact]
    public async Task MissingProductIsReportedInsteadOfReplacingSavedInformation()
    {
        await using var db = CreateContext();
        db.Inquiries.Add(new Inquiry { Id = 1, CompanyId = 1, Product = "Saved" });
        await db.SaveChangesAsync();
        var service = new DocumentProductSnapshotService(db);
        await Assert.ThrowsAsync<DocumentProductSourceException>(() => service.GetAsync("inquiries", 1, 1, default));
        Assert.Equal("Saved", (await db.Inquiries.SingleAsync()).Product);
    }

    [Fact]
    public async Task InquiryFallsBackToSupplierLanguageWhenRecipientLanguageHasNoCode()
    {
        await using var db = CreateContext();
        await Seed(db);
        var recipientLanguage = await db.Languages.SingleAsync(x => x.Id == 3);
        recipientLanguage.LanguageCode = " ";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var comparison = await new DocumentProductSnapshotService(db).GetAsync("inquiries", 1, 1, default);
        Assert.Equal("German material", comparison!.Current.Material);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task Seed(ApplicationDbContext db)
    {
        var edited = new DateTime(2026, 1, 1);
        var customer = new Customer { Id = 1, CompanyId = 1, Language = new Language { Id = 1, LanguageCode = "en" } };
        var supplier = new Supplier { Id = 2, Language = new Language { Id = 2, LanguageCode = "de" } };
        var recipient = new InquiryRecipient
        {
            Id = 3, CompanyId = 1, Supplier = supplier, Language = new Language { Id = 3, LanguageCode = "fr" }
        };
        db.Calculations.Add(new Calculation
        {
            Id = 10, CompanyId = 1, Customer = customer, Supplier = supplier,
            Product = new Product
            {
                Id = 4, CompanyId = 1, Name = "Product", NrOfColors = "4", VarnishOther = "extra",
                Material = new Material { Id = 5, Name = "Material", TranslationCode = 100 },
                Construction = new Construction { Id = 6, Name = "Construction" },
                Varnish = new Varnish { Id = 8, Name = "Varnish", TranslationCode = 101 },
                InternalInfo = new ProductInternalInfo { IsOffset = true, IsFlexo = true },
                NoteInternal = "Internal", NoteExternal = "External",
            },
        });
        db.Inquiries.Add(new Inquiry { Id = 1, CompanyId = 1, CalculationId = 10, SelectedInquiryRecipient = recipient, Color = "4 + legacy varnish", EditedAt = edited });
        db.Quotations.Add(new Quotation { Id = 1, CompanyId = 1, CalculationId = 10, Customer = customer, Color = "4 + legacy varnish", EditedAt = edited });
        db.CustomerOrders.Add(new CustomerOrder { Id = 1, CompanyId = 1, CalculationId = 10, Customer = customer, Color = "4 + legacy varnish", Edited = edited });
        db.SupplierOrders.Add(new SupplierOrder { Id = 1, CompanyId = 1, CalculationId = 10, Supplier = supplier, Color = "4 + legacy varnish", Edited = edited, Message = "Keep order message" });
        foreach (var (language, material, varnish) in new[]
        {
            ("en", "English material", "English varnish"),
            ("de", "German material", "German varnish"),
            ("fr", "French material", "French varnish"),
        })
        {
            db.TranslationItems.Add(new TranslationItem { LangCode = language, TranslationCode = 100, Translation = material });
            db.TranslationItems.Add(new TranslationItem { LangCode = language, TranslationCode = 101, Translation = varnish });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private sealed class TestUser : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 9, CompanyId = 1 });
    }
}

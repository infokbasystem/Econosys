using Econosys.Api.Controllers;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Econosys.Api.Tests;

public class OrderHistoryTests
{
    [Fact]
    public void ReproducesLegacyStockAndProductionTimeline()
    {
        var result = OrderHistoryBuilder.Build(5140, true, false,
            [
                new() { Id = 2, DeliveryDate = Day(20), NrOfItems = 300, DeliveryStatus = 2 },
                new() { Id = 1, DeliveryDate = Day(19), NrOfItems = 4840, DeliveryStatus = 2 },
            ],
            [],
            [
                new() { Id = 10, DeliveryDate = Day(25), NrOfItems = 300, DeliveryStatus = 2 },
                new() { Id = 11, DeliveryDate = Day(26), NrOfItems = 440, DeliveryStatus = 2 },
                new() { Id = 12, DeliveryDate = Day(27), NrOfItems = 880, DeliveryStatus = 2 },
            ],
            [new() { Id = 4, NrOfItems = 5140, StockTaking = new() { StockTakingDate = Day(24) } }],
            [], [], []);

        Assert.Equal(
            new[] { "Produced", "DeliveryToStock", "DeliveryToStock", "StockTaking", "DeliveryFromStock", "DeliveryFromStock", "DeliveryFromStock" },
            result.Rows.Select(x => x.Type));
        Assert.Equal(new double[] { 5140, 300, 0, 0, 0, 0, 0 }, result.Rows.Select(x => x.ProductionBalance));
        Assert.Equal(new double[] { 0, 4840, 5140, 5140, 4840, 4400, 3520 }, result.Rows.Select(x => x.StockBalance));
        Assert.Null(result.Rows[0].Date);
    }

    [Fact]
    public void PlannedDirectDeliveriesAndNegativeAdjustmentsKeepLegacyBalanceRules()
    {
        var result = OrderHistoryBuilder.Build(1000, true, false,
            [
                new() { Id = 1, NrOfItems = 600, DeliveryDate = Day(1), DeliveryStatus = 1 },
                new() { Id = 2, NrOfItems = -50, DeliveryDate = Day(2), IsAdjustment = true },
            ],
            [new() { Id = 1, NrOfItems = 100, DeliveryDate = Day(3), DeliveryStatus = 1 }],
            [new() { Id = 1, NrOfItems = 200, DeliveryDate = Day(4), DeliveryStatus = 1 }],
            [new() { Id = 1, NrOfItems = 320, StockTaking = new() { StockTakingDate = Day(5) } }],
            [], [], []);

        Assert.Equal(new double[] { 1000, 400, 450, 350, 350, 350 }, result.Rows.Select(x => x.ProductionBalance));
        Assert.Equal(new double[] { 0, 600, 550, 550, 350, 320 }, result.Rows.Select(x => x.StockBalance));
        Assert.Equal("Plan.", result.Rows[1].Status);
        Assert.Equal("Justering -", result.Rows[2].TypeName);
    }

    [Fact]
    public void InvoiceAndTransportLinksMatchDeliveryTypeNotJustId()
    {
        var result = OrderHistoryBuilder.Build(null, true, false,
            [new() { Id = 1 }],
            [new() { Id = 1 }],
            [new() { Id = 1 }],
            [],
            [
                new() { DeliveryToStockId = 1, TransportOrderId = null },
                new() { DeliveryToStockId = 1, TransportOrderId = 10, TransportOrder = new() { TransportOrderNr = 100 } },
                new() { DeliveryToCustomerId = 1, TransportOrderId = 20, TransportOrder = new() { TransportOrderNr = 200 } },
            ],
            [
                new() { DeliveryFromStockId = 1, CallOffId = null },
                new() { DeliveryFromStockId = 1, CallOffId = 30 },
            ],
            [
                new() { Id = 40, InvoiceNumber = 400, DeliveryToCustomerId = 1 },
                new() { Id = 50, InvoiceNumber = 500, InvoiceRows = [new() { DeliveryFromStockId = 1 }, new() { DeliveryFromStockId = 1 }] },
                new() { Id = 60, InvoiceNumber = 600, DeliveryFromStockId = 999 },
            ]);

        var inbound = Assert.Single(result.Rows, x => x.Type == "DeliveryToStock");
        var direct = Assert.Single(result.Rows, x => x.Type == "DeliveryToCustomer");
        var outbound = Assert.Single(result.Rows, x => x.Type == "DeliveryFromStock");
        Assert.Empty(inbound.Invoices);
        Assert.Equal(10, inbound.TransportOrderId);
        Assert.Equal(100, inbound.TransportOrderNr);
        Assert.Equal(40, Assert.Single(direct.Invoices).Id);
        Assert.Equal(20, direct.TransportOrderId);
        Assert.Equal(50, Assert.Single(outbound.Invoices).Id);
        Assert.Equal(30, outbound.CallOffId);
    }

    [Fact]
    public void CustomerCreditQuantityOnlyIncludesItsDeliveriesAndDoesNotChangeBalances()
    {
        var result = OrderHistoryBuilder.Build(100, false, true, [],
            [new() { Id = 1, NrOfItems = 20, DeliveryDate = Day(1), DeliveryStatus = 1 }],
            [], [], [], [],
            [new()
            {
                Id = 4, InvoiceNumber = 400, InvoiceTypeCode = InvoiceTypeCode.Credit, InvoiceDate = Day(2),
                InvoiceRows = [
                    new() { DeliveryToCustomerId = 1, NrOf = 5 },
                    new() { DeliveryToCustomerId = 999, NrOf = 100 },
                    new() { NrOf = 500 },
                ],
            }]);

        Assert.False(result.ShowStockBalance);
        var direct = Assert.Single(result.Rows, x => x.Type == "DeliveryToCustomer");
        Assert.Equal("Ej levererad", direct.Status);
        Assert.Empty(direct.Invoices);
        var credit = Assert.Single(result.Rows, x => x.Type == "Invoice");
        Assert.Equal(5d, credit.Quantity);
        Assert.Equal(80d, credit.ProductionBalance);
        Assert.Equal(0d, credit.StockBalance);
        Assert.Equal(400, Assert.Single(credit.Invoices).Number);
    }

    [Fact]
    public void HeaderLinkedInvoicesAndCreditsDoNotRequireInvoiceRows()
    {
        var result = OrderHistoryBuilder.Build(100, true, true, [],
            [new() { Id = 1, NrOfItems = 20, DeliveryDate = Day(1) }],
            [], [], [], [],
            [
                new() { Id = 4, InvoiceNumber = 400, DeliveryToCustomerId = 1 },
                new()
                {
                    Id = 5, InvoiceNumber = 500, DeliveryToCustomerId = 1,
                    InvoiceTypeCode = InvoiceTypeCode.Credit, InvoiceDate = Day(2),
                },
            ]);

        var delivery = Assert.Single(result.Rows, x => x.Type == "DeliveryToCustomer");
        Assert.Equal(400, Assert.Single(delivery.Invoices).Number);
        var credit = Assert.Single(result.Rows, x => x.Type == "Invoice");
        Assert.Equal(500, Assert.Single(credit.Invoices).Number);
        Assert.Equal(0d, credit.Quantity);
        Assert.Equal(80d, credit.ProductionBalance);
    }

    [Fact]
    public void FactoryDeliveryAndStocktakingReferencesArePreserved()
    {
        var result = OrderHistoryBuilder.Build(null, true, false, [], [],
            [new() { Id = 3, Inventory = new() { NoInventoryValue = true }, CustomerOrder = new() { CustomerName = "Kund" } }],
            [new() { Id = 5, StockTakingId = 9, StockTaking = new() { StockTakingDate = Day(2) }, NrOfItems = 0 }],
            [], [], []);
        Assert.Equal("Fabrikslev.", result.Rows[0].TypeName);
        Assert.Equal("Kund", result.Rows[0].To);
        Assert.Equal(9, result.Rows[1].StockTakingId);
        Assert.Equal(0d, result.Rows[1].StockBalance);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 2)]
    public void LegacyDatesHaveExplicitSwedishOffsets(int month, int offsetHours)
    {
        var result = OrderHistoryBuilder.Build(null, false, false,
            [new() { DeliveryDate = new DateTime(2026, month, 1) }],
            [], [], [], [], [], []);
        var date = Assert.Single(result.Rows).Date!.Value;
        Assert.Equal(TimeSpan.FromHours(offsetHours), date.Offset);
        Assert.Equal(1, date.Day);
        Assert.Equal(0, date.Hour);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndpointsForwardScopeAndCancellationAndDistinguishMissingFromEmpty(bool customerOrder)
    {
        using var cancellation = new CancellationTokenSource();
        var service = new HistoryServiceStub();
        var controller = new OrderHistoryController(service);

        Task<ActionResult<OrderHistoryResponse>> Get() => customerOrder
            ? controller.GetCustomerOrder(7, cancellation.Token)
            : controller.GetSupplierOrder(7, cancellation.Token);

        Assert.IsType<NotFoundResult>((await Get()).Result);
        Assert.Equal((7, customerOrder, cancellation.Token), service.Request);
        service.Response = new(false, []);
        var ok = Assert.IsType<OkObjectResult>((await Get()).Result);
        Assert.Same(service.Response, ok.Value);
    }

    private static DateTime Day(int day) => new(2026, 5, day);

    private sealed class HistoryServiceStub : IOrderHistoryService
    {
        public OrderHistoryResponse? Response { get; set; }
        public (int, bool, CancellationToken) Request { get; private set; }
        public Task<OrderHistoryResponse?> GetAsync(int orderId, bool customerOrder, CancellationToken cancellationToken)
        {
            Request = (orderId, customerOrder, cancellationToken);
            return Task.FromResult(Response);
        }
    }
}

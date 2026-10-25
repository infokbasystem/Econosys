using System.Linq.Expressions;
using System.Text.Json;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CalculationsController(
    ApplicationDbContext dbContext,
    ILegacyUserResolutionService legacyUserResolutionService,
    CalculationFreightPriceService calculationFreightPriceService) : ControllerBase
{
    private static readonly TimeZoneInfo SwedenTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    [HttpGet("{id:int}/aggregate")]
    [ProducesResponseType(typeof(CalculationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CalculationDetailDto>> GetAggregate(int id, CancellationToken cancellationToken)
    {
        var calculation = await BuildAggregateQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (calculation is null)
        {
            return NotFound();
        }

        return Ok(await MapAggregateAsync(calculation, cancellationToken));
    }

    [HttpGet("from-product/{productId:int}/draft")]
    [ProducesResponseType(typeof(CalculationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculationDetailDto>> GetDraftFromProduct(
        int productId,
        CancellationToken cancellationToken)
    {
        var legacyUser = await legacyUserResolutionService.ResolveCurrentUserAsync(User);
        if (legacyUser is null)
        {
            return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
        }

        var product = await dbContext.Products.AsNoTracking()
            .Include(item => item.Material)
            .Include(item => item.Construction)
            .Include(item => item.Varnish)
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        var variables = await dbContext.CalculationVariables
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (variables is null)
        {
            return BadRequest(new { message = "Calculation variables have not been configured." });
        }

        var currency = await dbContext.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IsDefault, cancellationToken);
        var unit = await dbContext.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IsDefault, cancellationToken);
        var currencyRate = currency is null
            ? 1d
            : currency.RateToSek.GetValueOrDefault() == 0
                ? 0d
                : 1d;
        var calculation = new Calculation
        {
            CompanyId = legacyUser.CompanyId,
            ProductId = productId,
            Product = product,
            UnitId = unit?.Id,
            Unit = unit,
            PurchaseCurrencyId = currency?.Id,
            PurchaseCurrency = currency,
            PurchaseCurrencyRate = currency?.RateToSek,
            SalesCurrencyId = currency?.Id,
            SalesCurrency = currency,
            SalesCurrencyRate = currency?.RateToSek,
            CurrencyRate = currencyRate,
            FreightPerPallet = variables.FreightPerPallet,
            FreightPerVehicle = variables.FreightPerVehicle,
            Interest = variables.Interest,
            LoadingPerDelivery = variables.LoadingPerDelivery,
            StoragePerM2 = variables.StoragePerM2,
            AutomaticLoadingInstruction = true,
            LockOutPrice = true,
        };

        return Ok(CalculationDetailMapper.Map(calculation, new Dictionary<int, string?>()));
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<CalculationHistoryRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CalculationHistoryRowDto>>> GetHistory(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Calculations.AnyAsync(item => item.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var customerOrders = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.QuotationId.HasValue
                && dbContext.Quotations.Any(quotation => quotation.Id == order.QuotationId.Value
                    && quotation.CalculationId == id))
            .OrderByDescending(order => order.Id)
            .Select(order => new
            {
                order.Id,
                order.CustomerOrderNr,
                order.Created,
                order.DeliveryDate,
                order.DeliveryDateWeekMode,
                order.TimeOfDelivery,
                order.Edition,
                order.SalesPrice,
                order.SalesCurrencyRate,
                SalesCurrencyName = order.SalesCurrency != null ? order.SalesCurrency.Name : null,
                UnitMultiplier = order.Unit != null ? order.Unit.Multiplicator : (short?)1,
                SupplierOrderId = order.SupplierOrder != null ? order.SupplierOrder.Id : (int?)null,
                SupplierOrderNr = order.SupplierOrder != null ? order.SupplierOrder.SupplierOrderNr : null,
                SelectedCalculationRowId = order.SupplierOrder != null ? order.SupplierOrder.SelectedCalculationRowId : null,
                SupplierOrderCreated = order.SupplierOrder != null ? order.SupplierOrder.Created : null,
                SupplierOrderDeliveryDate = order.SupplierOrder != null ? order.SupplierOrder.DeliveryDate : null,
                SupplierOrderDeliveryDateWeekMode = order.SupplierOrder != null && order.SupplierOrder.DeliveryDateWeekMode,
                SupplierOrderTimeOfDelivery = order.SupplierOrder != null ? order.SupplierOrder.TimeOfDelivery : null,
                SupplierPurchasePrice = order.SupplierOrder != null ? order.SupplierOrder.PurchasePrice : null,
                SupplierPurchaseCurrencyRate = order.SupplierOrder != null ? order.SupplierOrder.PurchaseCurrencyRate : null,
                SupplierPurchaseCurrencyName = order.SupplierOrder != null && order.SupplierOrder.PurchaseCurrency != null
                    ? order.SupplierOrder.PurchaseCurrency.Name
                    : null,
                CalculationRowPurchasePrice = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.PurchasePrice : null,
                CalculationRowSalesPrice = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.SalesPrice : null,
                CalculationRowFreightCost = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.Freight : null,
                CalculationRowOtherCost = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.Other : null,
                CalculationRowStorageCost = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.Storage : null,
                CalculationRowPercentAddition = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.PercentAddition : null,
                CalculationRowPackagingCost = order.SupplierOrder != null && order.SupplierOrder.SelectedCalculationRow != null ? order.SupplierOrder.SelectedCalculationRow.PackagingCost : null,
            })
            .ToListAsync(cancellationToken);

        var customerHistory = customerOrders.Select(order =>
        {
            var purchaseRate = order.SupplierPurchaseCurrencyRate ?? 0;
            var salesRate = order.SalesCurrencyRate ?? 0;
            var salesRateForCostTotal = order.SalesCurrencyRate ?? 1;
            var purchasePrice = order.SupplierPurchasePrice ?? 0;
            var freight = order.CalculationRowFreightCost ?? 0;
            var other = order.CalculationRowOtherCost ?? 0;
            var storage = order.CalculationRowStorageCost ?? 0;
            var packaging = (double)(order.CalculationRowPackagingCost ?? 0);
            var multiplier = order.UnitMultiplier.GetValueOrDefault(1);
            if (multiplier <= 0) multiplier = 1;
            var edition = order.Edition ?? 0;
            var purchaseCostTotal = salesRateForCostTotal > 0
                ? purchasePrice * purchaseRate / salesRateForCostTotal + freight + other + storage + packaging
                : (double?)null;
            var tbTotalSek = salesRate > 0
                ? (order.SalesPrice.GetValueOrDefault() * salesRate
                    - purchasePrice * purchaseRate
                    - freight * salesRate
                    - other * salesRate
                    - storage * salesRate
                    - packaging * salesRate)
                    * edition / multiplier
                : (double?)null;
            var infoParts = new List<string>();
            if (order.SupplierPurchasePrice != order.CalculationRowPurchasePrice)
            {
                infoParts.Add($"Inköpspris arkiverad kalkylrad / beställning:{Environment.NewLine}{order.CalculationRowPurchasePrice.GetValueOrDefault()} / {order.SupplierPurchasePrice.GetValueOrDefault()}");
            }
            if (order.SalesPrice != order.CalculationRowSalesPrice)
            {
                infoParts.Add($"Försäljningspris arkiverad kalkylrad / beställning:{Environment.NewLine}{order.CalculationRowSalesPrice.GetValueOrDefault()} / {order.SalesPrice.GetValueOrDefault()}");
            }

            return new CalculationHistoryRowDto(
                order.Created?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                order.SupplierOrderId,
                order.SupplierOrderNr,
                order.Id,
                order.CustomerOrderNr,
                FormatHistoryDeliveryTime(order.SupplierOrderDeliveryDate, order.SupplierOrderDeliveryDateWeekMode, order.SupplierOrderTimeOfDelivery),
                order.Edition,
                order.SupplierPurchasePrice,
                order.SupplierPurchaseCurrencyName,
                order.SupplierPurchaseCurrencyRate,
                order.CalculationRowFreightCost,
                order.CalculationRowOtherCost,
                order.CalculationRowStorageCost,
                order.CalculationRowPackagingCost,
                purchaseCostTotal,
                order.CalculationRowPercentAddition,
                order.SalesPrice,
                order.SalesCurrencyName,
                order.SalesCurrencyRate,
                tbTotalSek,
                infoParts.Count == 0 ? null : string.Join($"{Environment.NewLine}{Environment.NewLine}", infoParts),
                order.SelectedCalculationRowId);
        }).ToArray();

        var supplierOrders = await dbContext.SupplierOrders
            .AsNoTracking()
            .Where(order => order.InquiryId.HasValue
                && dbContext.Inquiries.Any(inquiry => inquiry.Id == order.InquiryId.Value
                    && inquiry.CalculationId == id)
                && !dbContext.CustomerOrders.Any(customerOrder => customerOrder.SupplierOrderId == order.Id))
            .OrderByDescending(order => order.Id)
            .Select(order => new
            {
                order.Id,
                order.SupplierOrderNr,
                order.SelectedCalculationRowId,
                order.Created,
                order.DeliveryDate,
                order.DeliveryDateWeekMode,
                order.TimeOfDelivery,
                order.Edition,
                order.PurchasePrice,
                order.PurchaseCurrencyRate,
                PurchaseCurrencyName = order.PurchaseCurrency != null ? order.PurchaseCurrency.Name : null,
            })
            .ToListAsync(cancellationToken);

        var supplierHistory = supplierOrders.Select(order => new CalculationHistoryRowDto(
            order.Created?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            order.Id,
            order.SupplierOrderNr,
            null,
            null,
            FormatHistoryDeliveryTime(order.DeliveryDate, order.DeliveryDateWeekMode, order.TimeOfDelivery),
            order.Edition,
            order.PurchasePrice,
            order.PurchaseCurrencyName,
            order.PurchaseCurrencyRate,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            order.SelectedCalculationRowId)).ToArray();

        return Ok(supplierHistory.Concat(customerHistory).ToArray());
    }

    private static string? FormatHistoryDeliveryTime(DateTime? deliveryDate, bool weekMode, string? freeText)
    {
        if (!deliveryDate.HasValue) return freeText;
        return weekMode
            ? $"v. {System.Globalization.ISOWeek.GetWeekOfYear(deliveryDate.Value)}"
            : deliveryDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    [HttpPut("{id:int}/aggregate")]
    [ProducesResponseType(typeof(CalculationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculationDetailDto>> UpdateAggregate(
        int id,
        [FromBody] UpdateCalculationRequest request,
        CancellationToken cancellationToken)
        => await SaveAggregateAsync(id, null, request, cancellationToken);

    [HttpPost("from-product/{productId:int}")]
    [ProducesResponseType(typeof(CalculationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculationDetailDto>> CreateFromProduct(
        int productId,
        [FromBody] UpdateCalculationRequest request,
        CancellationToken cancellationToken)
        => await SaveAggregateAsync(0, productId, request, cancellationToken);

    private async Task<ActionResult<CalculationDetailDto>> SaveAggregateAsync(
        int id,
        int? productId,
        UpdateCalculationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var legacyUser = await legacyUserResolutionService.ResolveCurrentUserAsync(User);
        if (legacyUser is null)
        {
            return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
        }

        var isNew = productId.HasValue;
        if (isNew && !await dbContext.Products.AnyAsync(item => item.Id == productId, cancellationToken))
        {
            return NotFound();
        }
        if (isNew && (request.Rows?.Any(row => row.Id.HasValue) == true
            || request.DeletedRowIds?.Count > 0))
        {
            return BadRequest(new { message = "A new calculation cannot reference saved rows." });
        }

        var calculation = isNew
            ? new Calculation
            {
                ProductId = productId,
                CompanyId = legacyUser.CompanyId,
                CreatedAt = SwedishTime.Now,
                CreatedBy = legacyUser.Id,
            }
            : await dbContext.Calculations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (calculation is null)
        {
            return NotFound();
        }

        if (request.CustomerId.HasValue && !await dbContext.Customers
                .AnyAsync(customer => customer.Id == request.CustomerId.Value, cancellationToken))
        {
            return BadRequest(new { message = "The selected customer does not exist." });
        }

        if (request.SupplierId.HasValue && !await dbContext.Suppliers
            .AnyAsync(supplier => supplier.Id == request.SupplierId.Value, cancellationToken))
        {
            return BadRequest(new { message = "The selected supplier does not exist." });
        }

        if (request.UnitId.HasValue && !await dbContext.Units
            .AnyAsync(unit => unit.Id == request.UnitId.Value, cancellationToken))
        {
            return BadRequest(new { message = "The selected unit does not exist." });
        }

        if (request.PurchaseCurrencyId.HasValue && !await dbContext.Currencies
            .AnyAsync(currency => currency.Id == request.PurchaseCurrencyId.Value, cancellationToken))
        {
            return BadRequest(new { message = "The selected purchase currency does not exist." });
        }

        if (request.SalesCurrencyId.HasValue && !await dbContext.Currencies
            .AnyAsync(currency => currency.Id == request.SalesCurrencyId.Value, cancellationToken))
        {
            return BadRequest(new { message = "The selected sales currency does not exist." });
        }

        if (request.PalletFormatId.HasValue && !await dbContext.PalletFormats
            .AnyAsync(format => format.Id == request.PalletFormatId.Value, cancellationToken))
        {
            return BadRequest(new { message = "The selected pallet format does not exist." });
        }

        CalculationUpdateMapper.Apply(calculation, request, legacyUser.Id, SwedishTime.Now);
        CalculationUpdateMapper.ApplyApprovals(calculation, request, legacyUser.Name, SwedishTime.Now);

        if (request.Rows is not null)
        {
            var rowError = await ApplyCalculationRowsAsync(id, calculation, request.Rows, cancellationToken);
            if (rowError is not null)
            {
                return BadRequest(new { message = rowError });
            }
        }

        if (request.DeletedRowIds is not null)
        {
            var deleteError = await DeleteCalculationRowsAsync(id, request.DeletedRowIds, cancellationToken);
            if (deleteError is not null)
            {
                return BadRequest(new { message = deleteError });
            }
        }

        if (isNew)
        {
            dbContext.Calculations.Add(calculation);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedCalculation = await BuildAggregateQuery()
            .SingleOrDefaultAsync(item => item.Id == calculation.Id, cancellationToken);
        if (updatedCalculation is null)
        {
            return NotFound();
        }

        var details = await MapAggregateAsync(updatedCalculation, cancellationToken);
        return isNew
            ? CreatedAtAction(nameof(GetAggregate), new { id = calculation.Id }, details)
            : Ok(details);
    }

    [HttpGet("cost-prices")]
    [ProducesResponseType(typeof(IReadOnlyList<CalculationCostPriceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalculationCostPriceDto>>> GetCostPrices(
        [FromQuery] int? customerId,
        [FromQuery] int? supplierId,
        CancellationToken cancellationToken)
    {
        var prices = await dbContext.CostPrices
            .AsNoTracking()
            .Where(item => item.CostId != null
                && ((customerId != null && item.CustomerId == customerId && item.SupplierId == null)
                    || (supplierId != null && item.SupplierId == supplierId && item.CustomerId == null)))
            .OrderBy(item => item.Id)
            .Select(item => new CalculationCostPriceDto(
                item.CostId!.Value,
                item.CustomerId,
                item.SupplierId,
                item.DefaultInPrice,
                item.DefaultOutPrice,
                item.CalculationAutoAddSupplier))
            .ToListAsync(cancellationToken);
        return Ok(prices);
    }

    [HttpPost("draft/freight-refresh")]
    [ProducesResponseType(typeof(RefreshCalculationFreightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RefreshCalculationFreightResponse>> RefreshDraftFreightPrices(
        [FromBody] RefreshCalculationFreightRequest request,
        CancellationToken cancellationToken)
        => await RefreshFreightPrices(0, request, cancellationToken);

    [HttpPost("{id:int}/freight-refresh")]
    [ProducesResponseType(typeof(RefreshCalculationFreightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RefreshCalculationFreightResponse>> RefreshFreightPrices(
        int id,
        [FromBody] RefreshCalculationFreightRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var calculation = await dbContext.Calculations
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (id != 0 && calculation is null)
        {
            return NotFound();
        }

        if (request.SupplierFactoryId.HasValue && !await dbContext.SupplierFactories
                .AnyAsync(item => item.Id == request.SupplierFactoryId.Value
                    && (!request.SupplierId.HasValue || item.SupplierId == request.SupplierId), cancellationToken))
        {
            return BadRequest(new { message = "The selected supplier factory does not belong to the selected supplier." });
        }

        if (request.CustomerDeliveryAddressId.HasValue && !await dbContext.CustomerDeliveryAddresses
                .AnyAsync(item => item.Id == request.CustomerDeliveryAddressId.Value
                    && (!request.CustomerId.HasValue || item.CustomerId == request.CustomerId), cancellationToken))
        {
            return BadRequest(new { message = "The selected delivery address does not belong to the selected customer." });
        }

        var rowIds = request.Rows.Where(row => row.Id.HasValue).Select(row => row.Id!.Value).ToArray();
        var rowAddressIds = request.Rows
            .Where(row => row.CustomerDeliveryAddressId.HasValue)
            .Select(row => row.CustomerDeliveryAddressId!.Value)
            .Distinct()
            .ToArray();
        if (rowAddressIds.Length > 0 && await dbContext.CustomerDeliveryAddresses
                .CountAsync(item => rowAddressIds.Contains(item.Id)
                    && (!request.CustomerId.HasValue || item.CustomerId == request.CustomerId), cancellationToken) != rowAddressIds.Length)
        {
            return BadRequest(new { message = "A row delivery address does not belong to the selected customer." });
        }
        if (rowIds.Distinct().Count() != rowIds.Length)
        {
            return BadRequest(new { message = "A calculation row was included more than once." });
        }
        if (rowIds.Length > 0 && await dbContext.CalculationRows
                .CountAsync(row => row.CalculationId == id && !row.Archived && rowIds.Contains(row.Id), cancellationToken) != rowIds.Length)
        {
            return BadRequest(new { message = "A calculation row does not belong to this calculation or is archived." });
        }

        return Ok(await calculationFreightPriceService.RefreshAsync(request, cancellationToken));
    }

    private async Task<string?> ApplyCalculationRowsAsync(
        int calculationId,
        Calculation calculation,
        IReadOnlyList<UpdateCalculationRowRequest> requestedRows,
        CancellationToken cancellationToken)
    {
        var existingRows = await dbContext.CalculationRows
            .Include(row => row.CalculationRowCosts)
            .Where(row => row.CalculationId == calculationId)
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var unit = calculation.UnitId.HasValue
            ? await dbContext.Units
                .Where(item => item.Id == calculation.UnitId.Value)
                .Select(item => new { Multiplier = (int?)item.Multiplicator, item.NrOfCalcDecimals })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var unitMultiplier = unit?.Multiplier ?? 0;
        var decimals = Math.Clamp(calculation.NrOfCalcDecimalsSales ?? unit?.NrOfCalcDecimals ?? 0, 0, 10);
        var seenIds = new HashSet<int>();
        var requestedCosts = requestedRows
            .Where(item => item.Costs is not null)
            .SelectMany(item => item.Costs!)
            .ToArray();
        var costIds = requestedCosts
            .Where(item => item.CostId.HasValue)
            .Select(item => item.CostId!.Value)
            .Distinct()
            .ToArray();
        if (costIds.Length > 0 && await dbContext.Costs
                .CountAsync(item => costIds.Contains(item.Id), cancellationToken) != costIds.Length)
        {
            return "A selected cost does not exist.";
        }

        var costCurrencyIds = requestedCosts
            .Where(item => item.InPriceCurrencyId.HasValue)
            .Select(item => item.InPriceCurrencyId!.Value)
            .Distinct()
            .ToArray();
        if (costCurrencyIds.Length > 0 && await dbContext.Currencies
                .CountAsync(item => costCurrencyIds.Contains(item.Id), cancellationToken) != costCurrencyIds.Length)
        {
            return "A selected cost currency does not exist.";
        }
        var rowAddressIds = requestedRows
            .Where(item => item.CustomerDeliveryAddressId.HasValue)
            .Select(item => item.CustomerDeliveryAddressId!.Value)
            .Distinct()
            .ToArray();
        if (rowAddressIds.Length > 0 && await dbContext.CustomerDeliveryAddresses
                .CountAsync(item => rowAddressIds.Contains(item.Id) && item.CustomerId == calculation.CustomerId, cancellationToken) != rowAddressIds.Length)
        {
            return "A row delivery address does not belong to the selected customer.";
        }

        foreach (var requestRow in requestedRows)
        {
            if (requestRow.Id is > 0 && !seenIds.Add(requestRow.Id.Value))
            {
                return "A calculation row was included more than once.";
            }

            var hasInput = requestRow.Edition.HasValue
                || requestRow.PurchasePrice.HasValue
                || requestRow.Freight.HasValue
                || requestRow.Other.HasValue
                || requestRow.Storage.HasValue
                || requestRow.PackagingCost.HasValue
                || requestRow.PercentAddition.HasValue
                || requestRow.SalesPrice.HasValue
                || requestRow.StorageNrOfPallets.HasValue
                || requestRow.StorageMonths.HasValue
                || !string.IsNullOrWhiteSpace(requestRow.Info);
            if (!requestRow.Id.HasValue && !hasInput)
            {
                continue;
            }

            CalculationRow row;
            if (requestRow.Id.HasValue)
            {
                if (!existingRows.TryGetValue(requestRow.Id.Value, out row!))
                {
                    return "A calculation row does not belong to this calculation.";
                }
                if (row.Archived)
                {
                    return "Archived calculation rows cannot be edited.";
                }
            }
            else
            {
                row = new CalculationRow { Calculation = calculation };
                dbContext.CalculationRows.Add(row);
            }

            CalculationUpdateMapper.ApplyRow(row, requestRow, calculation, unitMultiplier, decimals);
            if (requestRow.Costs is not null)
            {
                var costError = SyncRowCosts(row, requestRow.Costs);
                if (costError is not null)
                {
                    return costError;
                }
            }
        }

        return null;
    }

    private string? SyncRowCosts(CalculationRow row, IReadOnlyList<UpdateCalculationRowCostRequest> requestedCosts)
    {
        var existingCosts = row.CalculationRowCosts.ToDictionary(item => item.Id);
        var seenCostIds = new HashSet<int>();
        foreach (var requestedCost in requestedCosts)
        {
            CalculationRowCost cost;
            if (requestedCost.Id.HasValue)
            {
                if (!seenCostIds.Add(requestedCost.Id.Value))
                {
                    return "A calculation row cost was included more than once.";
                }
                if (!existingCosts.TryGetValue(requestedCost.Id.Value, out cost!))
                {
                    return "A calculation row cost does not belong to this row.";
                }
            }
            else
            {
                cost = new CalculationRowCost();
                row.CalculationRowCosts.Add(cost);
            }

            cost.CostId = requestedCost.CostId;
            cost.CostTypeText = requestedCost.CostTypeText;
            cost.NrOf = requestedCost.NrOf;
            cost.InPrice = requestedCost.InPrice;
            cost.InPriceCurrencyId = requestedCost.InPriceCurrencyId;
            cost.OutPrice = requestedCost.OutPrice;
            cost.DoDebit = requestedCost.DoDebit;
        }

        foreach (var removedCost in existingCosts.Values.Where(item => !seenCostIds.Contains(item.Id)))
        {
            dbContext.CalculationRowCosts.Remove(removedCost);
        }

        return null;
    }

    private async Task<string?> DeleteCalculationRowsAsync(
        int calculationId,
        IReadOnlyList<int> rowIds,
        CancellationToken cancellationToken)
    {
        if (rowIds.Any(id => id <= 0) || rowIds.Distinct().Count() != rowIds.Count)
        {
            return "Calculation row IDs must be positive and unique.";
        }

        var rows = await dbContext.CalculationRows
            .Where(row => row.CalculationId == calculationId && rowIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        if (rows.Count != rowIds.Count)
        {
            return "A calculation row does not belong to this calculation.";
        }

        if (rows.Values.Any(row => row.Archived))
        {
            return "Archived calculation rows cannot be deleted.";
        }

        foreach (var row in rows.Values)
        {
            dbContext.InquiryRows.RemoveRange(await dbContext.InquiryRows
                .Where(item => item.CalculationRowId == row.Id)
                .ToListAsync(cancellationToken));
            dbContext.CalculationRowCosts.RemoveRange(await dbContext.CalculationRowCosts
                .Where(item => item.CalculationRowId == row.Id)
                .ToListAsync(cancellationToken));

            var isReferenced = await dbContext.QuotationRows.AnyAsync(item => item.CalculationRowId == row.Id, cancellationToken)
                || await dbContext.SupplierOrders.AnyAsync(item => item.SelectedCalculationRowId == row.Id, cancellationToken)
                || await dbContext.CustomerOrders.AnyAsync(item => item.SelectedCalculationRowId == row.Id, cancellationToken)
                || await dbContext.OrderCosts.AnyAsync(item => item.CalculationRowId == row.Id, cancellationToken);

            if (isReferenced)
            {
                row.Archived = true;
            }
            else
            {
                dbContext.CalculationRows.Remove(row);
            }
        }

        return null;
    }

    private IQueryable<Calculation> BuildAggregateQuery() => dbContext.Calculations.AsNoTracking().AsSplitQuery()
            .Include(item => item.Product).ThenInclude(item => item!.Material)
            .Include(item => item.Product).ThenInclude(item => item!.Construction)
            .Include(item => item.Product).ThenInclude(item => item!.Varnish)
            .Include(item => item.Supplier).Include(item => item.Customer)
            .Include(item => item.Unit).Include(item => item.PalletFormat)
            .Include(item => item.PurchaseCurrency).Include(item => item.SalesCurrency)
            .Include(item => item.CalculationRows).ThenInclude(row => row.CalculationRowCosts).ThenInclude(cost => cost.Cost)
            .Include(item => item.CalculationRows).ThenInclude(row => row.CalculationRowCosts).ThenInclude(cost => cost.InPriceCurrency)
            .Include(item => item.CalculationRows).ThenInclude(row => row.SupplierOrders)
            .Include(item => item.CalculationRows).ThenInclude(row => row.CustomerOrders)
            .Include(item => item.CalculationRows).ThenInclude(row => row.QuotationRows);

    private async Task<CalculationDetailDto> MapAggregateAsync(Calculation calculation, CancellationToken cancellationToken)
    {
        var userIds = new[] { calculation.CreatedBy, calculation.EditedBy }
            .Where(value => value.HasValue).Select(value => value!.Value).Distinct().ToArray();
        var userNames = await dbContext.LegacyUsers.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);
        return CalculationDetailMapper.Map(calculation, userNames);
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(PagedResultDto<CalculationSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResultDto<CalculationSearchResultDto>>> Search(
        [FromBody] SearchCalculationsRequest request,
        CancellationToken cancellationToken)
    {
        var pagination = request.Pagination ?? new PaginationRequest();
        var skip = ((long)pagination.PageNumber - 1) * pagination.PageSize;
        if (pagination.PageNumber < 1 || pagination.PageSize < 1 || pagination.PageSize > 1000 || skip > int.MaxValue)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid pagination.");
        }

        IQueryable<Calculation> query = dbContext.Calculations.AsNoTracking();
        IOrderedQueryable<Calculation> orderedQuery;
        try
        {
            foreach (var condition in request.Filter?.Conditions ?? [])
            {
                query = ApplyCondition(query, condition);
            }

            orderedQuery = ApplyOrdering(query, request.OrderBy);
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: exception.Message);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await orderedQuery
            .Skip((int)skip)
            .Take(pagination.PageSize)
            .Select(calculation => new
            {
                calculation.Id,
                ProductName = calculation.Product != null ? calculation.Product.Name : null,
                ProductMaterial = calculation.Product != null && calculation.Product.Material != null ? calculation.Product.Material.Name : null,
                ProductConstruction = calculation.Product != null && calculation.Product.Construction != null ? calculation.Product.Construction.Name : null,
                Length = calculation.Product != null ? calculation.Product.LengthMm : null,
                Width = calculation.Product != null ? calculation.Product.WidthMm : null,
                Height = calculation.Product != null ? calculation.Product.HeightMm : null,
                Format = calculation.Product != null ? calculation.Product.Format : null,
                SupplierName = calculation.Supplier != null ? calculation.Supplier.Name : null,
                CustomerName = calculation.Customer != null ? calculation.Customer.Name : null,
                calculation.CreatedAt,
                calculation.EditedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new CalculationSearchResultDto(
            row.Id,
            row.ProductName,
            row.ProductMaterial,
            row.ProductConstruction,
            FormatDimensions(row.Length, row.Width, row.Height, row.Format),
            row.SupplierName,
            row.CustomerName,
            ToSwedishOffset(row.CreatedAt),
            ToSwedishOffset(row.EditedAt))).ToList();

        return Ok(new PagedResultDto<CalculationSearchResultDto>
        {
            Items = items,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pagination.PageSize),
        });
    }

    private IQueryable<Calculation> ApplyCondition(IQueryable<Calculation> query, FilterConditionDto condition)
    {
        if (condition is null)
        {
            throw new ArgumentException("Filter conditions must not be null.");
        }

        var field = condition.Field?.Trim().ToLowerInvariant();
        var operation = condition.Operator?.Trim().ToLowerInvariant();
        if (field == "id")
        {
            if (operation != "eq" || condition.Value is not { ValueKind: JsonValueKind.Number } number ||
                !number.TryGetInt32(out var id) || id < 1)
            {
                throw new ArgumentException("Calculation number requires 'eq' and a positive integer.");
            }

            return query.Where(calculation => calculation.Id == id);
        }

        if (operation != "contains" || condition.Value is not { ValueKind: JsonValueKind.String } text)
        {
            throw new ArgumentException("Text filters require 'contains' and a string value.");
        }

        var value = text.GetString()!.Trim();
        return field switch
        {
            "productname" => query.Where(calculation => calculation.Product != null && calculation.Product.Name != null && calculation.Product.Name.Contains(value)),
            "suppliername" => query.Where(calculation => calculation.Supplier != null && calculation.Supplier.Name != null && calculation.Supplier.Name.Contains(value)),
            "customername" => query.Where(calculation => calculation.Customer != null && calculation.Customer.Name != null && calculation.Customer.Name.Contains(value)),
            "supplierordernr" => query.Where(calculation => dbContext.SupplierOrders.Any(order =>
                order.CalculationId == calculation.Id && order.SupplierOrderNr != null && order.SupplierOrderNr.Contains(value))),
            _ => throw new ArgumentException("Unsupported calculation filter field."),
        };
    }

    private static IOrderedQueryable<Calculation> ApplyOrdering(IQueryable<Calculation> query, List<SortRequest>? sorts)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return query.OrderByDescending(calculation => calculation.Id);
        }

        IOrderedQueryable<Calculation>? ordered = null;
        foreach (var sort in sorts)
        {
            if (sort is null)
            {
                throw new ArgumentException("Sort entries must not be null.");
            }

            var direction = sort.Direction?.Trim().ToLowerInvariant();
            if (direction is not ("asc" or "desc"))
            {
                throw new ArgumentException("Sort direction must be 'asc' or 'desc'.");
            }

            var descending = direction == "desc";
            switch (sort.Field?.Trim().ToLowerInvariant())
            {
                case "id":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Id, descending);
                    break;
                case "productname":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null ? calculation.Product.Name : null, descending);
                    break;
                case "productmaterial":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null && calculation.Product.Material != null ? calculation.Product.Material.Name : null, descending);
                    break;
                case "productconstruction":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null && calculation.Product.Construction != null ? calculation.Product.Construction.Name : null, descending);
                    break;
                case "productformat":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null ? calculation.Product.LengthMm : null, descending);
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null ? calculation.Product.WidthMm : null, descending);
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null ? calculation.Product.HeightMm : null, descending);
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Product != null ? calculation.Product.Format : null, descending);
                    break;
                case "suppliername":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Supplier != null ? calculation.Supplier.Name : null, descending);
                    break;
                case "customername":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.Customer != null ? calculation.Customer.Name : null, descending);
                    break;
                case "createdat":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.CreatedAt, descending);
                    break;
                case "editedat":
                    ordered = ApplyOrder(ordered, query, calculation => calculation.EditedAt, descending);
                    break;
                default:
                    throw new ArgumentException("Unsupported calculation sort field.");
            }
        }

        return ordered!.ThenBy(calculation => calculation.Id);
    }

    private static IOrderedQueryable<Calculation> ApplyOrder<TKey>(
        IOrderedQueryable<Calculation>? ordered,
        IQueryable<Calculation> source,
        Expression<Func<Calculation, TKey>> selector,
        bool descending)
    {
        if (ordered is null)
        {
            return descending ? source.OrderByDescending(selector) : source.OrderBy(selector);
        }

        return descending ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector);
    }

    private static string FormatDimensions(int? length, int? width, int? height, string? format)
    {
        var dimensions = string.Join(" x ", new[] { length, width, height }.Where(value => value.HasValue));
        return string.Join(" ", new[] { dimensions, format?.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static DateTimeOffset? ToSwedishOffset(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        var wallClock = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
        return new DateTimeOffset(wallClock, SwedenTimeZone.GetUtcOffset(wallClock));
    }
}
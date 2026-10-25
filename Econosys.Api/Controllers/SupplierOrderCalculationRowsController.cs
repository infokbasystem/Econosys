using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
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
[Route("api/supplierorders/{id:int}/calculation-row")]
public sealed class SupplierOrderCalculationRowsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CalculationRowEditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculationRowEditDto>> Get(int id, CancellationToken cancellationToken)
    {
        var order = await db.SupplierOrders.AsNoTracking()
            .Include(item => item.SelectedCalculationRow).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return NotFound(new { message = "Beställningen finns inte." });
        if (order.SelectedCalculationRow is null)
            return NotFound(new { message = "Beställningen saknar en kopplad kalkylrad." });
        return Ok(CalculationRowEditMapper.Map(order.SelectedCalculationRow));
    }

    [HttpPut]
    [ProducesResponseType(typeof(CalculationRowEditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CalculationRowEditDto>> Update(
        int id, UpdateLinkedCalculationRowRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true)
            || (request.Values is not null && !Validator.TryValidateObject(
                request.Values, new ValidationContext(request.Values), validationResults, true)))
        {
            foreach (var result in validationResults)
                ModelState.AddModelError(string.Join(".", result.MemberNames), result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        var order = await db.SupplierOrders.Include(item => item.SelectedCalculationRow)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return NotFound(new { message = "Beställningen finns inte." });
        if (order.SelectedCalculationRow is null)
            return NotFound(new { message = "Beställningen saknar en kopplad kalkylrad." });
        if (order.SelectedCalculationRowId != request.ExpectedRowId)
            return Conflict(new { message = "Beställningens kalkylrad har ändrats. Stäng och öppna redigeringen igen." });

        var row = order.SelectedCalculationRow;
        var values = request.Values!;
        await ValidateReferencesAsync(order, row, values, cancellationToken);
        var candidate = new CalculationRow();
        CalculationRowEditMapper.Apply(candidate, values);
        var entry = db.Entry(candidate);
        foreach (var property in entry.Properties)
        {
            if (property.CurrentValue is double number && !double.IsFinite(number))
                ModelState.AddModelError(property.Metadata.Name, "Ange ett ändligt tal.");
            if (property.CurrentValue is not decimal amount) continue;
            var columnType = property.Metadata.PropertyInfo?.GetCustomAttribute<ColumnAttribute>()?.TypeName;
            if (columnType is null || !columnType.StartsWith("decimal(", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = columnType[8..^1].Split(',');
            var precision = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var scale = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var bound = (decimal)Math.Pow(10, precision - scale);
            if (amount <= -bound || amount >= bound || decimal.Round(amount, scale) != amount)
                ModelState.AddModelError(property.Metadata.Name, $"Värdet måste rymmas i decimal({precision},{scale}).");
        }
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        CalculationRowEditMapper.Apply(row, values);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(CalculationRowEditMapper.Map(row));
    }

    private async Task ValidateReferencesAsync(
        SupplierOrder order, CalculationRow row, CalculationRowEditValues values, CancellationToken cancellationToken)
    {
        foreach (var (name, value, previous) in new[]
        {
            (nameof(values.CustomerDeliveryAddressId), values.CustomerDeliveryAddressId, row.CustomerDeliveryAddressId),
            (nameof(values.ArchivedCustomerDeliveryAddressId), values.ArchivedCustomerDeliveryAddressId, row.ArchivedCustomerDeliveryAddressId),
        })
        {
            if (value != previous && value.HasValue && !await db.CustomerDeliveryAddresses.AnyAsync(
                item => item.Id == value && item.CustomerId == order.CustomerId, cancellationToken))
                ModelState.AddModelError(name, "Leveransadressen tillhör inte beställningens kund.");
        }
        if (values.ArchivedSupplierFactoryId != row.ArchivedSupplierFactoryId
            && values.ArchivedSupplierFactoryId.HasValue && !await db.SupplierFactories.AnyAsync(
                item => item.Id == values.ArchivedSupplierFactoryId && item.SupplierId == order.SupplierId, cancellationToken))
            ModelState.AddModelError(nameof(values.ArchivedSupplierFactoryId), "Fabriken tillhör inte beställningens leverantör.");
        foreach (var (name, value, previous) in new[]
        {
            (nameof(values.ArchivedPalletFormatId), values.ArchivedPalletFormatId, row.ArchivedPalletFormatId),
            (nameof(values.ArchivedPalletFormatIdSupplierOrder), values.ArchivedPalletFormatIdSupplierOrder, row.ArchivedPalletFormatIdSupplierOrder),
            (nameof(values.ArchivedPalletFormatIdCustomerOrder), values.ArchivedPalletFormatIdCustomerOrder, row.ArchivedPalletFormatIdCustomerOrder),
        })
        {
            if (value != previous && value.HasValue && !await db.PalletFormats.AnyAsync(item => item.Id == value, cancellationToken))
                ModelState.AddModelError(name, "Pallformatet finns inte.");
        }
        foreach (var (name, value, previous) in new[]
        {
            (nameof(values.ArchivedPriceListCurrencyId), values.ArchivedPriceListCurrencyId, row.ArchivedPriceListCurrencyId),
            (nameof(values.ArchivedPriceListCurrencyIdFtl), values.ArchivedPriceListCurrencyIdFtl, row.ArchivedPriceListCurrencyIdFtl),
            (nameof(values.ArchivedPriceListCurrencyIdDomestic), values.ArchivedPriceListCurrencyIdDomestic, row.ArchivedPriceListCurrencyIdDomestic),
        })
        {
            if (value != previous && value.HasValue && (value < 1 || value > int.MaxValue
                || decimal.Truncate(value.Value) != value || !await db.Currencies.AnyAsync(item => item.Id == (int)value.Value, cancellationToken)))
                ModelState.AddModelError(name, "Valutan finns inte.");
        }
    }
}

using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;
using Snapshot = Econosys.Api.Services.DocumentProductText.Snapshot;

namespace Econosys.Api.Services;

/// <summary>Product details stored on a document and the current translated values.</summary>
public sealed record DocumentProductSnapshotResponse(
    int ProductId, Snapshot Saved, Snapshot Current, DateTime? Edited);

public interface IDocumentProductSnapshotService
{
    Task<DocumentProductSnapshotResponse?> GetAsync(string type, int id, int companyId, CancellationToken cancellationToken);
    Task<DocumentProductSnapshotResponse?> RefreshAsync(string type, int id, int companyId, int userId, DateTime? edited, CancellationToken cancellationToken);
    Task<Snapshot> FromCalculationAsync(int calculationId, int companyId, int? customerId, int? supplierId, int? languageId, CancellationToken cancellationToken);
}

public sealed class DocumentProductSnapshotService(ApplicationDbContext db) : IDocumentProductSnapshotService
{
    public async Task<Snapshot> FromCalculationAsync(int calculationId, int companyId, int? customerId, int? supplierId, int? languageId, CancellationToken cancellationToken)
    {
        languageId ??= customerId.HasValue
            ? await db.Customers.AsNoTracking().Where(x => x.Id == customerId && x.CompanyId == companyId)
                .Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken)
            : supplierId.HasValue
                ? await db.Suppliers.AsNoTracking().Where(x => x.Id == supplierId)
                    .Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken)
                : null;
        var (snapshot, _) = await BuildAsync(calculationId, companyId, languageId, cancellationToken);
        return snapshot;
    }

    public async Task<DocumentProductSnapshotResponse?> GetAsync(string type, int id, int companyId, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(type, id, companyId, cancellationToken);
        return entity is null ? null : await BuildResponseAsync(entity, companyId, cancellationToken);
    }

    public async Task<DocumentProductSnapshotResponse?> RefreshAsync(
        string type, int id, int companyId, int userId, DateTime? edited, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(type, id, companyId, cancellationToken);
        if (entity is null) return null;
        if (Edited(entity) != edited)
        {
            throw new DocumentProductConflictException();
        }

        var response = await BuildResponseAsync(entity, companyId, cancellationToken);
        ApplyValues(entity, response.Current);
        switch (entity)
        {
            case Inquiry x: x.EditedAt = SwedishTime.Now; x.EditedBy = userId; break;
            case Quotation x: x.EditedAt = SwedishTime.Now; x.EditedBy = userId; break;
            case CustomerOrder x: x.Edited = SwedishTime.Now; x.EditedBy = userId; break;
            case SupplierOrder x: x.Edited = SwedishTime.Now; x.EditedBy = userId; break;
        }
        await db.SaveChangesAsync(cancellationToken);
        return response with { Saved = response.Current, Edited = Edited(entity) };
    }

    private async Task<object?> LoadAsync(string type, int id, int companyId, CancellationToken cancellationToken) => type switch
    {
        "inquiries" => await db.Inquiries.Include(x => x.SelectedInquiryRecipient)
            .ThenInclude(x => x!.Supplier)
            .Include(x => x.SelectedInquiryRecipient).ThenInclude(x => x!.Language)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken),
        "quotations" => await db.Quotations.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken),
        "customerorders" => await db.CustomerOrders.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken),
        "supplierorders" => await db.SupplierOrders.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, cancellationToken),
        _ => null,
    };

    private async Task<DocumentProductSnapshotResponse> BuildResponseAsync(object entity, int companyId, CancellationToken cancellationToken)
    {
        int? calculationId = null, customerId = null, supplierId = null, languageId = null;
        switch (entity)
        {
            case Inquiry inquiry:
                calculationId = inquiry.CalculationId;
                supplierId = inquiry.SelectedInquiryRecipient?.SupplierId;
                languageId = !string.IsNullOrWhiteSpace(inquiry.SelectedInquiryRecipient?.Language?.LanguageCode)
                    ? inquiry.SelectedInquiryRecipient.LanguageId
                    : inquiry.SelectedInquiryRecipient?.Supplier?.LanguageId;
                break;
            case Quotation quotation:
                calculationId = quotation.CalculationId;
                customerId = quotation.CustomerId;
                break;
            case CustomerOrder order:
                calculationId = order.CalculationId;
                customerId = order.CustomerId;
                calculationId ??= await QuotationCalculationAsync(order.QuotationId, companyId, cancellationToken);
                if (!calculationId.HasValue && order.SupplierOrderId.HasValue)
                {
                    var supplierOrder = await db.SupplierOrders.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == order.SupplierOrderId && x.CompanyId == companyId, cancellationToken);
                    if (supplierOrder != null)
                        calculationId = await SupplierCalculationAsync(supplierOrder, companyId, cancellationToken);
                }
                break;
            case SupplierOrder order:
                calculationId = await SupplierCalculationAsync(order, companyId, cancellationToken);
                supplierId = order.SupplierId;
                break;
        }
        if (!calculationId.HasValue)
            throw new DocumentProductSourceException("Dokumentet saknar en kopplad kalkyl.");
        languageId ??= customerId.HasValue
            ? await db.Customers.AsNoTracking().Where(x => x.Id == customerId && x.CompanyId == companyId)
                .Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken)
            : supplierId.HasValue
                ? await db.Suppliers.AsNoTracking().Where(x => x.Id == supplierId)
                    .Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken)
                : null;
        var (current, productId) = await BuildAsync(calculationId.Value, companyId, languageId, cancellationToken);
        return new DocumentProductSnapshotResponse(productId, Saved(entity), current, Edited(entity));
    }

    private async Task<int?> QuotationCalculationAsync(int? id, int companyId, CancellationToken cancellationToken) =>
        id.HasValue ? await db.Quotations.AsNoTracking().Where(x => x.Id == id && x.CompanyId == companyId)
            .Select(x => x.CalculationId).FirstOrDefaultAsync(cancellationToken) : null;

    private async Task<int?> SupplierCalculationAsync(SupplierOrder order, int companyId, CancellationToken cancellationToken)
    {
        if (order.CalculationId.HasValue) return order.CalculationId;
        var inquiryCalculation = order.InquiryId.HasValue
            ? await db.Inquiries.AsNoTracking().Where(x => x.Id == order.InquiryId && x.CompanyId == companyId)
                .Select(x => x.CalculationId).FirstOrDefaultAsync(cancellationToken) : null;
        if (inquiryCalculation.HasValue) return inquiryCalculation;
        var quotationCalculation = await QuotationCalculationAsync(order.QuotationId, companyId, cancellationToken);
        if (quotationCalculation.HasValue) return quotationCalculation;
        return order.SelectedCalculationRowId.HasValue
            ? await db.CalculationRows.AsNoTracking().Where(x => x.Id == order.SelectedCalculationRowId && x.CompanyId == companyId)
                .Select(x => x.CalculationId).FirstOrDefaultAsync(cancellationToken) : null;
    }

    private async Task<(Snapshot Snapshot, int ProductId)> BuildAsync(int calculationId, int companyId, int? languageId, CancellationToken cancellationToken)
    {
        var calculation = await db.Calculations.AsNoTracking()
            .Include(x => x.Product).ThenInclude(x => x!.Material)
            .Include(x => x.Product).ThenInclude(x => x!.Construction)
            .Include(x => x.Product).ThenInclude(x => x!.Varnish)
            .Include(x => x.Product).ThenInclude(x => x!.InternalInfo)
            .FirstOrDefaultAsync(x => x.Id == calculationId && x.CompanyId == companyId, cancellationToken);
        if (calculation?.Product is null)
            throw new DocumentProductSourceException("Kalkylen eller dess produkt kunde inte hittas.");
        var translations = await DocumentProductText.LoadTranslationsAsync(db, calculation.Product, languageId, cancellationToken);
        var snapshot = DocumentProductText.Build(calculation.Product, translations);
        if (snapshot.Product?.Length > 50 || snapshot.Material?.Length > 100 || snapshot.MaterialThickness?.Length > 50 ||
            snapshot.Construction?.Length > 50 || snapshot.Format?.Length > 50 || snapshot.Color?.Length > 255 ||
            snapshot.Varnish?.Length > 255 || snapshot.PrintMethod?.Length > 50 ||
            snapshot.ProductNoteInternal?.Length > 255 || snapshot.ProductNoteExternal?.Length > 255)
            throw new DocumentProductSourceException("Produktinformationen är för lång för dokumentet.");
        return (snapshot, calculation.Product.Id);
    }

    private static Snapshot Saved(object entity) => entity switch
    {
        Inquiry x => new(x.Product, x.Material, x.MaterialThickness, x.Construction, x.Format, x.Color, x.Varnish, x.PrintMethod, x.ProductNoteInternal, x.ProductNoteExternal),
        Quotation x => new(x.Product, x.Material, x.MaterialThickness, x.Construction, x.Format, x.Color, x.Varnish, x.PrintMethod, x.ProductNoteInternal, x.ProductMessage),
        CustomerOrder x => new(x.Product, x.Material, x.MaterialThickness, x.Construction, x.Format, x.Color, x.Varnish, x.PrintMethod, x.ProductNoteInternal, x.ProductMessage),
        SupplierOrder x => new(x.Product, x.Material, x.MaterialThickness, x.Construction, x.Format, x.Color, x.Varnish, x.PrintMethod, x.ProductNoteInternal, x.ProductNoteExternal),
        _ => throw new ArgumentOutOfRangeException(nameof(entity)),
    };

    private static DateTime? Edited(object entity) => entity switch
    {
        Inquiry x => x.EditedAt, Quotation x => x.EditedAt,
        CustomerOrder x => x.Edited, SupplierOrder x => x.Edited,
        _ => throw new ArgumentOutOfRangeException(nameof(entity)),
    };

    public static void ApplyValues(object entity, Snapshot s)
    {
        switch (entity)
        {
            case Inquiry x:
                x.Product = s.Product; x.Material = s.Material; x.MaterialThickness = s.MaterialThickness; x.Construction = s.Construction;
                x.Format = s.Format; x.Color = s.Color; x.Varnish = s.Varnish; x.PrintMethod = s.PrintMethod;
                x.ProductNoteInternal = s.ProductNoteInternal; x.ProductNoteExternal = s.ProductNoteExternal;
                break;
            case Quotation x:
                x.Product = s.Product; x.Material = s.Material; x.MaterialThickness = s.MaterialThickness; x.Construction = s.Construction;
                x.Format = s.Format; x.Color = s.Color; x.Varnish = s.Varnish; x.PrintMethod = s.PrintMethod;
                x.ProductNoteInternal = s.ProductNoteInternal; x.ProductMessage = s.ProductNoteExternal;
                break;
            case CustomerOrder x:
                x.Product = s.Product; x.Material = s.Material; x.MaterialThickness = s.MaterialThickness; x.Construction = s.Construction;
                x.Format = s.Format; x.Color = s.Color; x.Varnish = s.Varnish; x.PrintMethod = s.PrintMethod;
                x.ProductNoteInternal = s.ProductNoteInternal; x.ProductMessage = s.ProductNoteExternal;
                break;
            case SupplierOrder x:
                x.Product = s.Product; x.Material = s.Material; x.MaterialThickness = s.MaterialThickness; x.Construction = s.Construction;
                x.Format = s.Format; x.Color = s.Color; x.Varnish = s.Varnish; x.PrintMethod = s.PrintMethod;
                x.ProductNoteInternal = s.ProductNoteInternal; x.ProductNoteExternal = s.ProductNoteExternal;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entity));
        }
    }
}

public sealed class DocumentProductSourceException(string message) : Exception(message);
public sealed class DocumentProductConflictException : Exception;

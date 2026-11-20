using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public interface IEmailRecipientService
{
    Task<EmailRecipientsResponse?> GetAsync(string itemType, int itemId, string? receiverType,
        int[] receiverIds, LegacyUser user, CancellationToken cancellationToken);
}

public sealed class EmailRecipientService(ApplicationDbContext db) : IEmailRecipientService
{
    public async Task<EmailRecipientsResponse?> GetAsync(string itemType, int itemId, string? receiverType,
        int[] receiverIds, LegacyUser user, CancellationToken cancellationToken)
    {
        var type = itemType.ToUpperInvariant();
        var recipients = new List<EmailRecipientResponse>();
        int? supplierId = null;
        int? customerId = null;
        int? shipperId = null;
        int? languageId = null;
        switch (type)
        {
            case "SUPPLIERORDER":
                var supplierOrder = await db.SupplierOrders.AsNoTracking()
                    .Where(x => x.Id == itemId).Select(x => new { x.SupplierId }).SingleOrDefaultAsync(cancellationToken);
                if (supplierOrder is null) return null;
                supplierId = supplierOrder.SupplierId;
                break;
            case "INQUIRY":
                var inquiry = await db.Inquiries.AsNoTracking()
                    .Where(x => x.Id == itemId).Select(x => new { x.SelectedInquiryRecipientId }).SingleOrDefaultAsync(cancellationToken);
                if (inquiry is null) return null;
                supplierId = await db.InquiryRecipients.AsNoTracking()
                    .Where(x => x.Id == inquiry.SelectedInquiryRecipientId && x.InquiryId == itemId)
                    .Select(x => x.SupplierId).SingleOrDefaultAsync(cancellationToken);
                break;
            case "QUOTATION":
                var quotation = await db.Quotations.AsNoTracking()
                    .Where(x => x.Id == itemId).Select(x => new { x.CustomerId }).SingleOrDefaultAsync(cancellationToken);
                if (quotation is null) return null;
                customerId = quotation.CustomerId;
                break;
            case "CUSTOMERORDER":
                var customerOrder = await db.CustomerOrders.AsNoTracking()
                    .Where(x => x.Id == itemId).Select(x => new { x.CustomerId }).SingleOrDefaultAsync(cancellationToken);
                if (customerOrder is null) return null;
                customerId = customerOrder.CustomerId;
                break;
            case "INVOICE":
                var invoice = await db.Invoices.AsNoTracking()
                    .Where(x => x.Id == itemId).Select(x => new { x.CustomerId }).SingleOrDefaultAsync(cancellationToken);
                if (invoice is null) return null;
                customerId = invoice.CustomerId;
                break;
            case "CALLOFF":
            case "CALLOFFCONFIRMATION":
                var callOff = await db.CallOffs.AsNoTracking()
                    .Where(x => x.Id == itemId)
                    .Select(x => new { x.ShipperId, CustomerId = x.CustomerDeliveryAddress == null ? null : x.CustomerDeliveryAddress.CustomerId })
                    .SingleOrDefaultAsync(cancellationToken);
                if (callOff is null) return null;
                if (type == "CALLOFFCONFIRMATION")
                {
                    customerId = await db.CallOffDeliveries.AsNoTracking()
                        .Where(x => x.CallOffId == itemId)
                        .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                        .Select(x => x.DeliveryFromStock == null || x.DeliveryFromStock.CustomerOrder == null
                            ? null : x.DeliveryFromStock.CustomerOrder.CustomerId)
                        .FirstOrDefaultAsync(cancellationToken);
                    customerId ??= callOff.CustomerId;
                }
                if (!customerId.HasValue) shipperId = callOff.ShipperId;
                break;
            case "TRANSPORTORDER":
                var transportOrder = await db.TransportOrders.AsNoTracking()
                    .Where(x => x.Id == itemId).Select(x => new { x.ShipperId }).SingleOrDefaultAsync(cancellationToken);
                if (transportOrder is null) return null;
                switch (receiverType?.ToUpperInvariant())
                {
                    case "TRANSPORTER":
                        shipperId = transportOrder.ShipperId;
                        break;
                    case "SUPPLIER":
                        var factories = await db.TransportOrderDeliveries.AsNoTracking()
                            .Where(x => x.TransportOrderId == itemId && x.SupplierOrder != null)
                            .Select(x => x.SupplierOrder!.SupplierFactoryId).Distinct().ToListAsync(cancellationToken);
                        if (receiverIds.Length == 0 || receiverIds.Any(id => !factories.Contains(id)))
                            throw new EmailValidationException("Ogiltig mottagare för transportordern.");
                        var suppliers = await db.SupplierFactories.AsNoTracking()
                            .Where(x => receiverIds.Contains(x.Id)).Select(x => x.SupplierId).Distinct().ToListAsync(cancellationToken);
                        var contacts = await db.SupplierContactPersons.AsNoTracking()
                            .Where(x => suppliers.Contains(x.SupplierId) && x.Email != null && x.Email != "")
                            .OrderByDescending(x => x.DoMailTransportOrder).ThenBy(x => x.Id)
                            .Select(x => new EmailRecipientResponse(x.Email!, x.DoMailTransportOrder, x.Title))
                            .ToListAsync(cancellationToken);
                        recipients.AddRange(contacts);
                        languageId = await db.Suppliers.AsNoTracking()
                            .Where(x => x.Id == suppliers.First()).Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken);
                        break;
                    case "RECEIVER":
                        if (receiverIds.Length != 1)
                            throw new EmailValidationException("Välj ett mottagande lager.");
                        var inventoryId = receiverIds[0];
                        if (!await db.TransportOrderDeliveries.AnyAsync(x => x.TransportOrderId == itemId
                            && x.DeliveryToStock != null && x.DeliveryToStock.InventoryId == inventoryId, cancellationToken))
                            throw new EmailValidationException("Ogiltigt lager för transportordern.");
                        var inventoryEmail = await db.Inventories.AsNoTracking()
                            .Where(x => x.Id == inventoryId).Select(x => x.Email).SingleOrDefaultAsync(cancellationToken);
                        if (!string.IsNullOrWhiteSpace(inventoryEmail))
                            recipients.Add(new EmailRecipientResponse(inventoryEmail, true));
                        break;
                    default:
                        throw new EmailValidationException("Ogiltig mottagartyp för transportordern.");
                }
                break;
            default:
                throw new EmailValidationException("Ogiltig dokumenttyp för e-post.");
        }

        if (supplierId.HasValue)
        {
            languageId = await db.Suppliers.AsNoTracking()
                .Where(x => x.Id == supplierId).Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken);
            recipients.AddRange(await db.SupplierContactPersons.AsNoTracking()
                .Where(x => x.SupplierId == supplierId && x.Email != null && x.Email != "")
                .OrderByDescending(x => type == "INQUIRY" ? x.MailInquiry : x.MailSupplierOrder).ThenBy(x => x.Id)
                .Select(x => new EmailRecipientResponse(x.Email!,
                    type == "INQUIRY" ? x.MailInquiry : x.MailSupplierOrder, x.Title))
                .ToListAsync(cancellationToken));
        }
        if (customerId.HasValue)
        {
            // Legacy never sets the language for call-off confirmations, so they fall back to Swedish.
            if (type != "CALLOFFCONFIRMATION")
            {
                languageId = await db.Customers.AsNoTracking()
                    .Where(x => x.Id == customerId).Select(x => x.LanguageId).FirstOrDefaultAsync(cancellationToken);
            }
            recipients.AddRange(await db.CustomerContactPersons.AsNoTracking()
                .Where(x => x.CustomerId == customerId && x.Email != null && x.Email != "")
                .OrderByDescending(x => type == "QUOTATION" ? x.MailQuotation
                    : type == "CUSTOMERORDER" ? x.MailCustomerOrder
                    : type == "INVOICE" ? x.MailInvoice : x.MailCallOffConfirmation).ThenBy(x => x.Id)
                .Select(x => new EmailRecipientResponse(x.Email!,
                    type == "QUOTATION" ? x.MailQuotation
                        : type == "CUSTOMERORDER" ? x.MailCustomerOrder
                        : type == "INVOICE" ? x.MailInvoice : x.MailCallOffConfirmation, x.Title))
                .ToListAsync(cancellationToken));
        }
        if (shipperId.HasValue)
        {
            var email = await db.Shippers.AsNoTracking().Where(x => x.Id == shipperId)
                .Select(x => x.Mail).SingleOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(email)) recipients.Add(new EmailRecipientResponse(email, true));
        }

        string[] copies = type switch
        {
            "INVOICE" => ["customerinvoice@econopack.se", "skickadepdffakturor@econopack.se"],
            "TRANSPORTORDER" or "CALLOFF" or "CALLOFFCONFIRMATION" => ["transport@econopack.se"],
            _ => string.IsNullOrWhiteSpace(user.Email) ? [] : [user.Email],
        };
        recipients.Add(new EmailRecipientResponse("", true));
        var cc = copies.Select(email => new EmailRecipientResponse(email, true)).ToList();
        cc.Add(new EmailRecipientResponse("", true));
        var (subject, body) = await EmailTextBuilder.BuildAsync(db, type, itemId, languageId ?? 1, user, cancellationToken);
        return new EmailRecipientsResponse(recipients, cc, subject, body);
    }
}

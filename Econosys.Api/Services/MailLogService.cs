using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public interface IMailLogService
{
    Task<IReadOnlyList<MailLogEntryResponse>> GetAsync(
        string item, int itemId, CancellationToken cancellationToken);
}

public sealed class MailLogService(ApplicationDbContext dbContext) : IMailLogService
{
    public const string CallOffItem = "CALLOFF";
    public const string TransportOrderItem = "TRANSPORTORDER";
    public const string CustomerOrderItem = "CUSTOMERORDER";
    public const string InvoiceItem = "INVOICE";
    public const string SendMailAction = "SENDMAIL";

    public async Task<IReadOnlyList<MailLogEntryResponse>> GetAsync(
        string item, int itemId, CancellationToken cancellationToken)
    {
        var entries = await (
            from log in dbContext.LogEntries.AsNoTracking()
            join employee in dbContext.LegacyUsers.AsNoTracking()
                on log.EmployeeId equals (int?)employee.Id into employees
            from employee in employees.DefaultIfEmpty()
            where log.Item == item && log.ItemId == itemId && log.Action == SendMailAction
            orderby log.DateTime, log.Id
            select new
            {
                log.Id,
                log.DateTime,
                log.Info1,
                log.Info2,
                EmployeeName = employee != null ? employee.Name : null,
            }).ToListAsync(cancellationToken);

        return entries.Select(log => new MailLogEntryResponse(
            log.Id,
            SwedishTime.ToDateTimeOffset(log.DateTime),
            log.Info1 ?? string.Empty,
            log.Info2 ?? string.Empty,
            log.EmployeeName ?? string.Empty)).ToList();
    }
}

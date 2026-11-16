namespace Econosys.Api.DTOs;

/// <summary>A sent email recorded for a call-off or transport order.</summary>
public sealed record MailLogEntryResponse(
    int Id,
    DateTimeOffset DateTime,
    string Recipients,
    string Subject,
    string EmployeeName);

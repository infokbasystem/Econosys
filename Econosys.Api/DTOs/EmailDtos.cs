namespace Econosys.Api.DTOs;

/// <summary>Multipart email content and selected recipients.</summary>
public sealed record SendEmailRequest
{
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public string? Receivers { get; init; }
    public string? CcRecipients { get; init; }
    public List<IFormFile> Files { get; init; } = new();
    public int? InquiryId { get; init; }
    public int? QuotationId { get; init; }
    public int? CallOffId { get; init; }
    public int? TransportOrderId { get; init; }
}

/// <summary>Confirmation that the SMTP server accepted the email.</summary>
public sealed record SendEmailResponse(string Message, int? InquiryId, int? QuotationId);
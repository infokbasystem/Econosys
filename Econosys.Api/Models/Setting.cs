using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Settings")]
    public class Setting
    {
        [Key]
        [Column("lngSettings_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("intNrOfInquiryAnswerDays")]
        public short? NrOfInquiryAnswerDays { get; set; }

        [Column("strVATInfo")]
        [MaxLength(255)]
        public string? VATInfo { get; set; }

        [Column("strCompany")]
        [MaxLength(50)]
        public string? Company { get; set; }

        [Column("lngInvoiceLastNr")]
        public int? InvoiceLastNr { get; set; }

        [Column("intHandlingTimesGoalNrOfDays")]
        public int? HandlingTimesGoalNrOfDays { get; set; }

        [Column("intHandlingTimesGoalMaxNrOfDays")]
        public int? HandlingTimesGoalMaxNrOfDays { get; set; }

        [Column("decHandlingTimesPercentHandledUnderGoalNrOfDays")]
        public decimal? HandlingTimesPercentHandledUnderGoalNrOfDays { get; set; }

        [Column("intHandlingTimesThresholdNrOfDays")]
        public int? HandlingTimesThresholdNrOfDays { get; set; }

        [Column("strDefaultCustomerMessage")]
        [MaxLength(255)]
        public string? DefaultCustomerMessage { get; set; }

        [Column("strEUText")]
        [MaxLength(255)]
        public string? EUText { get; set; }

        [Column("strExportText")]
        [MaxLength(255)]
        public string? ExportText { get; set; }

        [Column("JeevesApiEndpoint")]
        [MaxLength(500)]
        public string? JeevesApiEndpoint { get; set; }

        [Column("JeevesApiKeyProtected")]
        [MaxLength(2000)]
        public string? JeevesApiKeyProtected { get; set; }

        [Column("JeevesTestApiEndpoint")]
        [MaxLength(500)]
        public string? JeevesTestApiEndpoint { get; set; }

        [Column("JeevesTestApiKeyProtected")]
        [MaxLength(2000)]
        public string? JeevesTestApiKeyProtected { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        public virtual CompanyInfo? CompanyInfo { get; set; }
    }
}
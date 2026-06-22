using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Supplier")]
    public class Supplier
    {
        [Key]
        [Column("lngSupplier_ID")]
        public int Id { get; set; }

        [Column("strSupplier")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strSortName")]
        [MaxLength(50)]
        public string? SortName { get; set; }

        [Column("strReference")]
        [MaxLength(50)]
        public string? Reference { get; set; }

        [Column("strContactPerson")]
        [MaxLength(255)]
        public string? ContactPerson { get; set; }

        [Column("strAddress")]
        [MaxLength(50)]
        public string? Address { get; set; }

        [Column("strPostalNr")]
        [MaxLength(50)]
        public string? PostalNr { get; set; }

        [Column("strPostalAddress")]
        [MaxLength(50)]
        public string? PostalAddress { get; set; }

        [Column("strVisitingAddress")]
        [MaxLength(50)]
        public string? VisitingAddress { get; set; }

        [Column("strCountry")]
        [MaxLength(50)]
        public string? Country { get; set; }

        [Column("strTelephone1")]
        [MaxLength(50)]
        public string? Telephone1 { get; set; }

        [Column("strTelephone2")]
        [MaxLength(50)]
        public string? Telephone2 { get; set; }

        [Column("strTelephone3")]
        [MaxLength(50)]
        public string? Telephone3 { get; set; }

        [Column("strFax")]
        [MaxLength(50)]
        public string? Fax { get; set; }

        [Column("strNote")]
        [MaxLength(255)]
        public string? Note { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("lngLanguage_ID")]
        public int? LanguageId { get; set; }

        [Column("strTermsOfDelivery")]
        [MaxLength(255)]
        public string? TermsOfDelivery { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(255)]
        public string? TermsOfPayment { get; set; }

        [Column("strEmail")]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Column("lngInquiryCommunicationType_ID")]
        public int? InquiryCommunicationTypeId { get; set; }

        [Column("lngSupplierOrderCommunicationType_ID")]
        public int? SupplierOrderCommunicationTypeId { get; set; }

        [Column("strCostCenter")]
        [MaxLength(50)]
        public string? CostCenter { get; set; }

        [Column("lngCurrency_ID")]
        public int? CurrencyId { get; set; }

        [Column("strAddress2")]
        [MaxLength(50)]
        public string? Address2 { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("EconopackTransportResponsible")]
        public bool EconopackTransportResponsible { get; set; }

        [Column("PrintForDocumentScanning")]
        public bool PrintForDocumentScanning { get; set; }

        [Column("PricePerEurPallet", TypeName = "decimal(13,5)")]
        public decimal? PricePerEurPallet { get; set; }

        [Column("FscDefault")]
        public bool FscDefault { get; set; }

        [Column("SupplierOrderTemplateNr")]
        public int? SupplierOrderTemplateNr { get; set; }

        public virtual Language? Language { get; set; }
        public virtual ICollection<SupplierOrder> SupplierOrders { get; set; } = new List<SupplierOrder>();
        public virtual ICollection<OrderCost> OrderCosts { get; set; } = new List<OrderCost>();
        public virtual ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
        public virtual ICollection<Deviation> Deviations { get; set; } = new List<Deviation>();
        public virtual ICollection<InquiryRecipient> InquiryRecipients { get; set; } = new List<InquiryRecipient>();
        public virtual ICollection<SupplierContactPerson> SupplierContactPersons { get; set; } = new List<SupplierContactPerson>();
        public virtual ICollection<PalletFormatPrice> PalletFormatPrices { get; set; } = new List<PalletFormatPrice>();
    }
}

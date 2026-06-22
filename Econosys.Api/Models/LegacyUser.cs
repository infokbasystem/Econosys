using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("User")]
    public class LegacyUser
    {
        [Key]
        [Column("lngUser_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("strUser")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strLogin")]
        [MaxLength(50)]
        public string? Initials { get; set; }

        [Column("strPass")]
        [MaxLength(50)]
        public string? Password { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("bolSalesStat")]
        public bool SalesStat { get; set; }

        [Column("strEmail")]
        [MaxLength(255)]
        public string? Email { get; set; }

        [Column("MyPage")]
        public int? MyPage { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("Title")]
        [MaxLength(200)]
        public string? Title { get; set; }

        [Column("DirectPhone")]
        [MaxLength(200)]
        public string? DirectPhone { get; set; }

        [Column("MobilePhone")]
        [MaxLength(200)]
        public string? MobilePhone { get; set; }

        [Column("TitleEnglish")]
        [MaxLength(200)]
        public string? TitleEnglish { get; set; }

        [Column("AuthorizationLevel")]
        public int AuthorizationLevel { get; set; }

        [Column("IncludeInBudget")]
        public bool IncludeInBudget { get; set; }


        public virtual ICollection<Customer> ResponsibleForCustomers { get; set; } = new List<Customer>();
        public virtual ICollection<Customer> SupportingCustomers { get; set; } = new List<Customer>();
        public virtual ICollection<CustomerOrder> ResponsibleForCustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<Deviation> CreatedDeviations { get; set; } = new List<Deviation>();
        public virtual ICollection<Deviation> EditedDeviations { get; set; } = new List<Deviation>();
        public virtual ICollection<Deviation> ResponsibleForDeviations { get; set; } = new List<Deviation>();
        public virtual ICollection<DocumentFile> CreatedDocumentFiles { get; set; } = new List<DocumentFile>();
        public virtual ICollection<CallOff> CreatedCallOffs { get; set; } = new List<CallOff>();
        public virtual ICollection<Invoice> CreatedInvoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Invoice> EditedInvoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Inquiry> CreatedInquiries { get; set; } = new List<Inquiry>();
        public virtual ICollection<Inquiry> EditedInquiries { get; set; } = new List<Inquiry>();
        public virtual ICollection<OrderCost> CreatedOrderCosts { get; set; } = new List<OrderCost>();
        public virtual ICollection<OrderCost> EditedOrderCosts { get; set; } = new List<OrderCost>();
        public virtual ICollection<Quotation> CreatedQuotations { get; set; } = new List<Quotation>();
        public virtual ICollection<Quotation> EditedQuotations { get; set; } = new List<Quotation>();
    }
}

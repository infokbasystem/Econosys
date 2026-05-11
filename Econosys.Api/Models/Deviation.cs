using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Deviation")]
    public class Deviation
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerOrderId { get; set; }

        public bool IsInternal { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DeviationNr { get; set; }

        public int? CreatedByUserId { get; set; }

        public DateTime? CreatedTimeStamp { get; set; }

        public int? EditedByUserId { get; set; }

        public DateTime? EditedTimeStamp { get; set; }

        public int? ResponsibleUserId { get; set; }

        public DateTime? DeviationOpened { get; set; }

        public DateTime? DeviationClosed { get; set; }

        [MaxLength(50)]
        public string? DeviationProcessCode { get; set; }

        public int? SupplierId { get; set; }

        public int? CustomerId { get; set; }

        [MaxLength(50)]
        public string? DeviationTypeCode { get; set; }

        [MaxLength(4000)]
        public string? Description { get; set; }

        [MaxLength(4000)]
        public string? RootCause { get; set; }

        [MaxLength(4000)]
        public string? MeasureTaken { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedInternalCostSEK { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? EstimatedInternalCostPercentageOfOrder { get; set; }

        [MaxLength(100)]
        public string? SupplierDecisionCode { get; set; }

        [MaxLength(100)]
        public string? CompanyDecisionCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualInternalCostSEK { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NrOfProduced { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NrOfClaimed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FreightCostPer1000 { get; set; }

        public int? FreightCostCurrencyId { get; set; }

        [MaxLength(4000)]
        public string? ClaimReason { get; set; }

        [MaxLength(4000)]
        public string? FollowupInfo { get; set; }

        public DateTime? DeviationClosedToSupplier { get; set; }

        public virtual CustomerOrder? CustomerOrder { get; set; }
        public virtual LegacyUser? CreatedByUser { get; set; }
        public virtual LegacyUser? EditedByUser { get; set; }
        public virtual LegacyUser? ResponsibleUser { get; set; }
        public virtual Supplier? Supplier { get; set; }
        public virtual Customer? Customer { get; set; }
        public virtual Currency? FreightCostCurrency { get; set; }
        public virtual ICollection<DeviationCost> DeviationCosts { get; set; } = new List<DeviationCost>();
        public virtual ICollection<DocumentFile> DocumentFiles { get; set; } = new List<DocumentFile>();
        public virtual ICollection<DocumentFileRelation> DocumentFileRelations { get; set; } = new List<DocumentFileRelation>();
    }
}
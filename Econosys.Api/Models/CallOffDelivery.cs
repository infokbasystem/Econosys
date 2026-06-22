using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CallOffDelivery")]
    public class CallOffDelivery
    {
        [Key]
        public int Id { get; set; }

        public int? CallOffId { get; set; }

        public int? SortOrder { get; set; }

        public int? DeliveryFromStockId { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(500)]
        public string? DeliveryAddressFreeText { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? NrOfPalletPlaces { get; set; }

        public virtual CallOff? CallOff { get; set; }
        public virtual DeliveryFromStock? DeliveryFromStock { get; set; }
    }
}

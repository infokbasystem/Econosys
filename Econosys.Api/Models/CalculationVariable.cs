using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CalculationVariable")]
    public class CalculationVariable
    {
        [Key]
        [Column("lngCalculationVariable_ID")]
        public int Id { get; set; }

        [Column("dblFreightPerPallet")]
        public double? FreightPerPallet { get; set; }

        [Column("dblFreightPerVehicle")]
        public double? FreightPerVehicle { get; set; }

        [Column("dblLoadingPerPallet")]
        public double? LoadingPerPallet { get; set; }

        [Column("dblStoragePerM2")]
        public double? StoragePerM2 { get; set; }

        [Column("intInterest")]
        public short? Interest { get; set; }

        [Column("dblLoadingPerDelivery")]
        public double? LoadingPerDelivery { get; set; }

        [Column("dblFreightFromStockPerPallet")]
        public double? FreightFromStockPerPallet { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("TruckLoadingLengthMm")]
        public int? TruckLoadingLengthMm { get; set; }

        [Column("TruckLoadingWidthMm")]
        public int? TruckLoadingWidthMm { get; set; }
    }
}

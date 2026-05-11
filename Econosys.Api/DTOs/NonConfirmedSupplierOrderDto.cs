namespace Econosys.Api.DTOs
{
    public class NonConfirmedSupplierOrderRowDto
    {
        public int Id { get; set; }
        public DateTime? SentAt { get; set; }
        public string? FileSent { get; set; }
        public string Seller { get; set; } = string.Empty;
        public string OurReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public int Edition { get; set; }
        public int CustomerOrderedEdition { get; set; }
        public int EditiopnLeft { get; set; }
        public decimal OrderValueSek { get; set; }
        public decimal OrderValueLeftSek { get; set; }
        public decimal? MarkupPercent { get; set; }
        public decimal TotalTbSek { get; set; }
        public decimal TotalTbLeftSek { get; set; }
    }
}

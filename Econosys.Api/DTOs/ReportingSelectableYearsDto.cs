namespace Econosys.Api.DTOs
{
    public class ReportingSelectableYearsResponseDto
    {
        public string ItemType { get; set; } = string.Empty;
        public string DateField { get; set; } = string.Empty;
        public IReadOnlyList<int> Years { get; set; } = Array.Empty<int>();
    }
}
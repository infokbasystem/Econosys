namespace Econosys.Api.DTOs
{
    public class HeaderSearchRequest
    {
        public string? SearchTerm { get; set; }
    }

    public class HeaderSearchResultDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }
}
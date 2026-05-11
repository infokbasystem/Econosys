namespace Econosys.Api.DTOs
{
    public class FilterOptionDto<TId>
    {
        public TId Id { get; set; } = default!;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class PackagingFeeCategoryDto
    {
        public int Id { get; set; }
        public string? CategoryCode { get; set; }
        public string? Description { get; set; }
    }

    public class CreatePackagingFeeCategoryRequest
    {
        [MaxLength(20)]
        public string? CategoryCode { get; set; }

        [MaxLength(80)]
        public string? Description { get; set; }
    }

    public class UpdatePackagingFeeCategoryRequest
    {
        [MaxLength(20)]
        public string? CategoryCode { get; set; }

        [MaxLength(80)]
        public string? Description { get; set; }
    }
}

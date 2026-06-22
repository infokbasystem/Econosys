using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class ShipperDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ContactPerson { get; set; }
        public string? Telephone { get; set; }
        public string? Mail { get; set; }
    }

    public class CreateShipperRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(200)]
        public string? Telephone { get; set; }

        [MaxLength(200)]
        public string? Mail { get; set; }
    }

    public class UpdateShipperRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(200)]
        public string? Telephone { get; set; }

        [MaxLength(200)]
        public string? Mail { get; set; }
    }
}

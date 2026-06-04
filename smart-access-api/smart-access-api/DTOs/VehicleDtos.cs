using System.ComponentModel.DataAnnotations;
using smart_access_api.Models;

namespace smart_access_api.DTOs
{
    public class VehicleCreateDto
    {
        [Required(ErrorMessage = "La placa es obligatoria.")]
        [StringLength(15, MinimumLength = 3)]
        public string Plate { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Brand { get; set; }

        [StringLength(50)]
        public string? Model { get; set; }

        [StringLength(30)]
        public string? Color { get; set; }

        [Range(1950, 2100, ErrorMessage = "El año del vehículo no es válido.")]
        public int? Year { get; set; }
    }

    public class VehicleUpdateDto
    {
        [Required(ErrorMessage = "La placa es obligatoria.")]
        [StringLength(15, MinimumLength = 3)]
        public string Plate { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Brand { get; set; }

        [StringLength(50)]
        public string? Model { get; set; }

        [StringLength(30)]
        public string? Color { get; set; }

        [Range(1950, 2100, ErrorMessage = "El año del vehículo no es válido.")]
        public int? Year { get; set; }
    }

    public class VehicleResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ResidentId { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? Color { get; set; }
        public int? Year { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public static VehicleResponseDto From(Vehicle v) => new()
        {
            Id = v.Id,
            ResidentId = v.ResidentId,
            Plate = v.Plate,
            Brand = v.Brand,
            Model = v.Model,
            Color = v.Color,
            Year = v.Year,
            IsActive = v.IsActive,
            CreatedAt = v.CreatedAt.ToDateTime(),
        };
    }
}

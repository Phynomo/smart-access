using System.ComponentModel.DataAnnotations;
using smart_access_api.Models;

namespace smart_access_api.DTOs
{
    // El residente genera un QR de visita (date) o de larga duración (long_term).
    // El QR permanente NO se genera por aquí: se crea automáticamente con el residente.
    public class GenerateQrDto
    {
        [Required(ErrorMessage = "El tipo de QR es obligatorio.")]
        public string QrType { get; set; } = QrTypes.Date;

        [Required(ErrorMessage = "El nombre del visitante es obligatorio.")]
        [StringLength(120, MinimumLength = 2)]
        public string VisitorName { get; set; } = string.Empty;

        // Obligatoria para QR tipo 'date': el QR sólo es válido ese día.
        public DateTime? ValidDate { get; set; }

        // Opcional para QR tipo 'long_term': hasta cuándo es válido.
        // Si no se envía, el servidor usa el máximo configurado por residencia.
        public DateTime? ValidUntil { get; set; }
    }

    // Lo que envía el guardia al escanear un QR: el token contenido en el código.
    public class ValidateQrDto
    {
        [Required(ErrorMessage = "El token del QR es obligatorio.")]
        public string Token { get; set; } = string.Empty;

        // Tipo de movimiento (entrada/salida). Por defecto entrada.
        public string EventType { get; set; } = EventTypes.Entry;
    }

    public class QrCodeResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ResidentId { get; set; } = string.Empty;
        public string? VisitorName { get; set; }
        public string QrType { get; set; } = string.Empty;
        public DateTime? ValidDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }

        // El token ES el contenido que el frontend codifica dentro de la imagen QR.
        public string Token { get; set; } = string.Empty;

        public DateTime? UsedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public static QrCodeResponseDto From(QRCode q) => new()
        {
            Id = q.Id,
            ResidentId = q.ResidentId,
            VisitorName = q.VisitorName,
            QrType = q.QrType,
            ValidDate = q.ValidDate?.ToDateTime(),
            ExpiresAt = q.ExpiresAt?.ToDateTime(),
            IsUsed = q.IsUsed,
            IsRevoked = q.IsRevoked,
            Token = q.Token,
            UsedAt = q.UsedAt?.ToDateTime(),
            CreatedAt = q.CreatedAt.ToDateTime(),
        };
    }
}

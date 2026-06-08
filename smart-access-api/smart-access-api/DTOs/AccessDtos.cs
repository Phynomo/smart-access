using System.ComponentModel.DataAnnotations;
using smart_access_api.Models;

namespace smart_access_api.DTOs
{
    // Registro manual hecho por el guardia (sin QR).
    // Dos casos soportados:
    //   - Residente conocido: enviar ResidentId (búsqueda previa por nombre/casa).
    //   - Visitante: enviar VisitorName (+ identidad, placa y evidencia).
    public class ManualEntryDto
    {
        // Residente al que se asocia el ingreso (obligatorio para ingreso de residente;
        // para visitante, es el residente que lo recibe si se conoce).
        public string? ResidentId { get; set; }

        // Datos del visitante (obligatorios si no es un residente).
        [StringLength(120)]
        public string? VisitorName { get; set; }

        [StringLength(40)]
        public string? VisitorIdNumber { get; set; }

        [StringLength(15)]
        public string? VisitorVehiclePlate { get; set; }

        // URL de la evidencia fotográfica ya subida a Storage (/visitors/evidence/).
        public string? EvidencePhotoUrl { get; set; }

        public string EventType { get; set; } = EventTypes.Entry;
    }

    // Filtros para listar eventos de acceso (admin) o historiales.
    public class AccessQueryDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? AccessMethod { get; set; } // qr | manual
        public string? EventType { get; set; }    // entry | exit
        public string? Result { get; set; }       // authorized | rejected
        public string? ResidentId { get; set; }
    }

    public class AccessEventResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string ResidentId { get; set; } = string.Empty;
        public string? VisitorName { get; set; }
        public string? VisitorIdNumber { get; set; }
        public string? VisitorVehiclePlate { get; set; }
        public string? EvidencePhotoUrl { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string AccessMethod { get; set; } = string.Empty;
        public string? QrId { get; set; }
        public string? GuardId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Result { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }

        public static AccessEventResponseDto From(AccessEvent e) => new()
        {
            Id = e.Id,
            UserId = e.UserId,
            ResidentId = e.ResidentId,
            VisitorName = e.VisitorName,
            VisitorIdNumber = e.VisitorIdNumber,
            VisitorVehiclePlate = e.VisitorVehiclePlate,
            EvidencePhotoUrl = e.EvidencePhotoUrl,
            EventType = e.EventType,
            AccessMethod = e.AccessMethod,
            QrId = e.QrId,
            GuardId = e.GuardId,
            Timestamp = e.Timestamp.ToDateTime(),
            Result = e.Result,
            RejectionReason = e.RejectionReason,
        };
    }

    // Resultado de validar un QR (lo que el guardia ve tras escanear).
    public class ValidationResultDto
    {
        public bool Authorized { get; set; }
        public string? RejectionReason { get; set; }
        public AccessEventResponseDto Event { get; set; } = new();

        // Datos para mostrar en la confirmación visual.
        public string? ResidentName { get; set; }
        public string? HouseNumber { get; set; }
        public string? VisitorName { get; set; }
    }
}

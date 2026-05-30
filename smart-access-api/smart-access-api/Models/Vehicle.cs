using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class Vehicle
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        // Residente dueño del vehículo. Un residente puede tener N vehículos.
        [FirestoreProperty("residentId")]
        public string ResidentId { get; set; } = string.Empty;

        // Placa: única en el sistema; sirve como llave de búsqueda para el guardia
        // ("buscar vehículo por placa") y para validar duplicados al crear.
        [FirestoreProperty("plate")]
        public string Plate { get; set; } = string.Empty;

        [FirestoreProperty("brand")]
        public string? Brand { get; set; }

        [FirestoreProperty("model")]
        public string? Model { get; set; }

        [FirestoreProperty("color")]
        public string? Color { get; set; }

        [FirestoreProperty("year")]
        public int? Year { get; set; }

        // Permite "retirar" un vehículo sin borrar los eventos que lo referencian.
        [FirestoreProperty("isActive")]
        public bool IsActive { get; set; } = true;

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);
    }
}

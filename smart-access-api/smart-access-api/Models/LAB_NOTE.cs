using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class Lab_Note
    {
        // GUID generado por el servidor al crear.
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("title")]
        public string? Title { get; set; }

        [FirestoreProperty("observation")]
        public string Observation { get; set; } = string.Empty;

        // Solo acepta: Quimica, Biologia, Fisica u Otro.
        [FirestoreProperty("category")]
        public string? Category { get; set; }

        // 1 (baja), 2 (media) o 3 (alta).
        [FirestoreProperty("priority")]
        public int? Priority { get; set; }

        [FirestoreProperty("ispublic")]
        public bool? IsPublic { get; set; }

        // Etiquetas separadas por coma.
        [FirestoreProperty("tags")]
        public string? Tags { get; set; }

        // CAMBIO: antes inicializaba con EventTypes.Entry (un string) sobre un DateTime, lo cual no compila.
        // Asignado por el servidor en UTC.
        [FirestoreProperty("createat")]
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        // CAMBIO: antes el nombre Firestore era "useid" (typo) y el default era AccessMethods.Qr ("qr").
        // El UserId se extrae del token JWT, nunca se envía por el cliente.
        [FirestoreProperty("userId")]
        public string UserId { get; set; } = string.Empty;
    }
}

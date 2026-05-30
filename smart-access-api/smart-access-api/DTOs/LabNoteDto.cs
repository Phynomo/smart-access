namespace smart_access_api.DTOs
{
    // Datos que el cliente envía al crear una nota.
    // Id, CreatedAt y UserId los asigna el servidor — nunca el cliente.
    public class LabNoteDto
    {
        public string Title { get; set; } = string.Empty;
        public string Observation { get; set; } = string.Empty;

        // Solo acepta: Quimica, Biologia, Fisica u Otro.
        public string Category { get; set; } = string.Empty;

        // 1 (baja), 2 (media) o 3 (alta).
        public int Priority { get; set; } = 1;

        public bool IsPublic { get; set; } = false;

        // Etiquetas separadas por coma.
        public string Tags { get; set; } = string.Empty;
    }
}

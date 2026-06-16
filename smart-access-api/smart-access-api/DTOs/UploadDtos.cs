namespace smart_access_api.DTOs
{
    // Resultado de subir una imagen: la URL pública servida por el propio API.
    // El frontend la guarda tal cual en PhotoUrl del residente y la usa en <img>.
    public class PhotoUploadResultDto
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}

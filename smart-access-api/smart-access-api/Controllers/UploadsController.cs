using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;

namespace smart_access_api.Controllers
{
    // Subida de imágenes que el propio API almacena y sirve como archivos estáticos
    // (carpeta /uploads). Reemplaza la dependencia de Firebase Storage para las fotos.
    [Authorize]
    public class UploadsController : ApiControllerBase
    {
        private readonly IWebHostEnvironment _env;

        // Límites de la imagen del residente.
        private const long MaxBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private static readonly string[] AllowedContentTypes =
            ["image/jpeg", "image/png", "image/webp"];

        private const string ResidentsFolder = "residents";
        private const string VisitorsFolder  = "visitors";

        public UploadsController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Admin: sube la foto de un residente. Devuelve la URL pública para guardarla
        // luego en el PhotoUrl del residente.
        [HttpPost("resident-photo")]
        [Authorize(Roles = UserRoles.Admin)]
        [RequestSizeLimit(MaxBytes)]
        public async Task<IActionResult> UploadResidentPhoto(IFormFile? file)
        {
            if (file is null || file.Length == 0)
                throw BusinessException.BadRequest("No se recibió ningún archivo.");

            if (file.Length > MaxBytes)
                throw BusinessException.BadRequest("La imagen supera el tamaño máximo de 5 MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension) ||
                !AllowedContentTypes.Contains(file.ContentType))
            {
                throw BusinessException.BadRequest("Formato no permitido. Usa JPG, PNG o WEBP.");
            }

            // Carpeta física: {ContentRoot}/uploads/residents (se crea si no existe).
            var folder = Path.Combine(_env.ContentRootPath, "uploads", ResidentsFolder);
            Directory.CreateDirectory(folder);

            // Nombre único para evitar colisiones y no exponer el nombre original.
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            // Sólo la ruta relativa servida por UseStaticFiles. El frontend le antepone
            // el origen del API para construir la URL completa de la imagen.
            var relativePath = $"/uploads/{ResidentsFolder}/{fileName}";

            var result = new PhotoUploadResultDto { Url = relativePath, FileName = fileName };
            return ApiResponse.Created(result, "Imagen subida con éxito.").ToActionResult();
        }

        // Seguridad: sube la foto de evidencia de un visitante en registro manual.
        [HttpPost("visitor-evidence")]
        [Authorize(Roles = UserRoles.Security)]
        [RequestSizeLimit(MaxBytes)]
        public async Task<IActionResult> UploadVisitorEvidence(IFormFile? file)
        {
            if (file is null || file.Length == 0)
                throw BusinessException.BadRequest("No se recibió ningún archivo.");

            if (file.Length > MaxBytes)
                throw BusinessException.BadRequest("La imagen supera el tamaño máximo de 5 MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension) ||
                !AllowedContentTypes.Contains(file.ContentType))
                throw BusinessException.BadRequest("Formato no permitido. Usa JPG, PNG o WEBP.");

            var folder = Path.Combine(_env.ContentRootPath, "uploads", VisitorsFolder);
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            var relativePath = $"/uploads/{VisitorsFolder}/{fileName}";
            var result = new PhotoUploadResultDto { Url = relativePath, FileName = fileName };
            return ApiResponse.Created(result, "Evidencia subida con éxito.").ToActionResult();
        }
    }
}

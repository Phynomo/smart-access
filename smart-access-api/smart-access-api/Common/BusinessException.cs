using Microsoft.AspNetCore.Mvc;

namespace smart_access_api.Common
{
    // Excepción de reglas de negocio. Los servicios la lanzan cuando una operación
    // no se puede completar por una regla del dominio (no encontrado, sin permiso,
    // conflicto, dato inválido). El GlobalExceptionHandler la traduce a un
    // ApiResponse con el status code correcto, así los controllers no necesitan
    // bloques try/catch repetidos.
    public class BusinessException : Exception
    {
        public int StatusCode { get; }
        public IEnumerable<string>? Errors { get; }

        public BusinessException(
            string message,
            int statusCode = StatusCodes.Status400BadRequest,
            IEnumerable<string>? errors = null) : base(message)
        {
            StatusCode = statusCode;
            Errors = errors;
        }

        public static BusinessException BadRequest(string message, IEnumerable<string>? errors = null) =>
            new(message, StatusCodes.Status400BadRequest, errors);

        public static BusinessException Unauthorized(string message = "No autenticado.") =>
            new(message, StatusCodes.Status401Unauthorized);

        public static BusinessException Forbidden(string message = "No tienes permiso para esta acción.") =>
            new(message, StatusCodes.Status403Forbidden);

        public static BusinessException NotFound(string message = "Recurso no encontrado.") =>
            new(message, StatusCodes.Status404NotFound);

        public static BusinessException Conflict(string message) =>
            new(message, StatusCodes.Status409Conflict);
    }
}

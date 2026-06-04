using Microsoft.AspNetCore.Mvc;

namespace smart_access_api.Common
{
    // Envoltorio de respuesta estándar para TODA la API.
    // El frontend siempre recibe la misma forma:
    //   { success, code, message, data, errors }
    // Así puede tener un interceptor único que lea `success` para saber si la
    // operación funcionó, `message` para mostrar al usuario, `code` para el
    // status HTTP y `data` para la información.
    public class ApiResponse<T>
    {
        // true si la operación fue exitosa; false si hubo error de negocio/sistema.
        public bool Success { get; set; }

        // Código HTTP (200, 201, 400, 404, ...). Se replica también como status
        // real de la respuesta para que el front pueda usar cualquiera de los dos.
        public int Code { get; set; }

        // Mensaje legible para mostrar directamente al usuario final.
        public string Message { get; set; } = string.Empty;

        // Carga útil. Null en errores o en operaciones sin contenido.
        public T? Data { get; set; }

        // Lista de errores de detalle (p. ej. errores de validación por campo).
        public IEnumerable<string>? Errors { get; set; }

        public static ApiResponse<T> Ok(
            T? data,
            string message = "Operación realizada con éxito.",
            int code = StatusCodes.Status200OK) => new()
            {
                Success = true,
                Code = code,
                Message = message,
                Data = data,
            };

        public static ApiResponse<T> Created(
            T? data,
            string message = "Recurso creado con éxito.") => Ok(data, message, StatusCodes.Status201Created);

        public static ApiResponse<T> Fail(
            string message,
            int code = StatusCodes.Status400BadRequest,
            IEnumerable<string>? errors = null) => new()
            {
                Success = false,
                Code = code,
                Message = message,
                Errors = errors,
            };
    }

    // Helpers no genéricos para que el tipo T se infiera solo:
    //   ApiResponse.Ok(dto)  →  ApiResponse<DtoType>
    public static class ApiResponse
    {
        public static ApiResponse<T> Ok<T>(
            T data,
            string message = "Operación realizada con éxito.",
            int code = StatusCodes.Status200OK) => ApiResponse<T>.Ok(data, message, code);

        public static ApiResponse<T> Created<T>(
            T data,
            string message = "Recurso creado con éxito.") => ApiResponse<T>.Created(data, message);

        public static ApiResponse<object> Fail(
            string message,
            int code = StatusCodes.Status400BadRequest,
            IEnumerable<string>? errors = null) => ApiResponse<object>.Fail(message, code, errors);
    }

    public static class ApiResponseExtensions
    {
        // Convierte el ApiResponse en una respuesta HTTP cuyo status code coincide
        // con el campo Code. Mantiene los controllers de una sola línea.
        public static IActionResult ToActionResult<T>(this ApiResponse<T> response) =>
            new ObjectResult(response) { StatusCode = response.Code };
    }
}

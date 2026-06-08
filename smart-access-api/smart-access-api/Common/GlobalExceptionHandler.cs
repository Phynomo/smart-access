using Microsoft.AspNetCore.Diagnostics;

namespace smart_access_api.Common
{
    // Manejador global de excepciones (IExceptionHandler, patrón moderno de .NET).
    // Centraliza la conversión de cualquier excepción no controlada en un
    // ApiResponse uniforme, para que el frontend siempre reciba la misma forma.
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            ApiResponse<object> response;

            switch (exception)
            {
                // Reglas de negocio: se esperan, se loguean como warning.
                case BusinessException be:
                    _logger.LogWarning(be, "Regla de negocio: {Message}", be.Message);
                    response = ApiResponse<object>.Fail(be.Message, be.StatusCode, be.Errors);
                    break;

                // Cualquier otra cosa: error inesperado, se loguea como error y NO
                // se filtra el detalle interno al cliente.
                default:
                    _logger.LogError(exception, "Excepción no controlada");
                    response = ApiResponse<object>.Fail(
                        "Ocurrió un error inesperado. Inténtalo de nuevo más tarde.",
                        StatusCodes.Status500InternalServerError);
                    break;
            }

            httpContext.Response.StatusCode = response.Code;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}

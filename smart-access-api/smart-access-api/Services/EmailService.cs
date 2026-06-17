using System.Net;
using System.Net.Mail;

namespace smart_access_api.Services
{
    // Envío de correos vía SMTP. Toda la configuración vive en appsettings (sección
    // "Email"), por lo que las credenciales del remitente son configurables sin tocar
    // código. El envío es best-effort: si falla, se registra pero NO se propaga, para
    // no romper la operación de negocio que lo disparó (p. ej. crear un residente).
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private bool Enabled => _config.GetValue("Email:Enabled", false);

        // Envía al residente recién creado su correo de acceso y la contraseña inicial.
        public async Task SendResidentWelcomeAsync(string toEmail, string name, string loginEmail, string password)
        {
            if (!Enabled)
            {
                _logger.LogInformation(
                    "Email deshabilitado (Email:Enabled=false). No se envió la credencial a {Email}.", toEmail);
                return;
            }

            try
            {
                var host = _config["Email:Host"] ?? throw new InvalidOperationException("Falta Email:Host.");
                var port = _config.GetValue("Email:Port", 587);
                var enableSsl = _config.GetValue("Email:EnableSsl", true);
                var user = _config["Email:User"];
                var pass = _config["Email:Password"];
                var fromAddress = _config["Email:FromAddress"] ?? user
                    ?? throw new InvalidOperationException("Falta Email:FromAddress.");
                var fromName = _config["Email:FromName"] ?? "ResidentPass";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = "Tu acceso a ResidentPass",
                    IsBodyHtml = true,
                    Body = BuildWelcomeBody(name, loginEmail, password),
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(user, pass),
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Correo de bienvenida enviado a {Email}.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el correo de bienvenida a {Email}.", toEmail);
            }
        }

        // Envía al residente una nueva contraseña temporal tras un restablecimiento
        // iniciado por el administrador.
        public async Task SendPasswordResetAsync(string toEmail, string name, string loginEmail, string password)
        {
            if (!Enabled)
            {
                _logger.LogInformation(
                    "Email deshabilitado (Email:Enabled=false). No se envió el restablecimiento a {Email}.", toEmail);
                return;
            }

            try
            {
                var host = _config["Email:Host"] ?? throw new InvalidOperationException("Falta Email:Host.");
                var port = _config.GetValue("Email:Port", 587);
                var enableSsl = _config.GetValue("Email:EnableSsl", true);
                var user = _config["Email:User"];
                var pass = _config["Email:Password"];
                var fromAddress = _config["Email:FromAddress"] ?? user
                    ?? throw new InvalidOperationException("Falta Email:FromAddress.");
                var fromName = _config["Email:FromName"] ?? "ResidentPass";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = "Restablecimiento de contraseña · ResidentPass",
                    IsBodyHtml = true,
                    Body = BuildResetBody(name, loginEmail, password),
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(user, pass),
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Correo de restablecimiento enviado a {Email}.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el correo de restablecimiento a {Email}.", toEmail);
            }
        }

        // Envía el enlace de un solo uso para que el usuario defina una nueva
        // contraseña (flujo "olvidé mi contraseña", iniciado por el propio usuario).
        public async Task SendPasswordResetLinkAsync(string toEmail, string name, string resetUrl)
        {
            if (!Enabled)
            {
                _logger.LogInformation(
                    "Email deshabilitado (Email:Enabled=false). No se envió el enlace de restablecimiento a {Email}.", toEmail);
                return;
            }

            try
            {
                var host = _config["Email:Host"] ?? throw new InvalidOperationException("Falta Email:Host.");
                var port = _config.GetValue("Email:Port", 587);
                var enableSsl = _config.GetValue("Email:EnableSsl", true);
                var user = _config["Email:User"];
                var pass = _config["Email:Password"];
                var fromAddress = _config["Email:FromAddress"] ?? user
                    ?? throw new InvalidOperationException("Falta Email:FromAddress.");
                var fromName = _config["Email:FromName"] ?? "ResidentPass";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = "Recupera tu contraseña · ResidentPass",
                    IsBodyHtml = true,
                    Body = BuildResetLinkBody(name, resetUrl),
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(user, pass),
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Enlace de restablecimiento enviado a {Email}.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el enlace de restablecimiento a {Email}.", toEmail);
            }
        }

        private static string BuildWelcomeBody(string name, string loginEmail, string password) => $"""
            <div style="font-family: Arial, sans-serif; color: #1e293b;">
              <h2 style="color: #2563eb;">Bienvenido a ResidentPass</h2>
              <p>Hola {WebUtility.HtmlEncode(name)},</p>
              <p>Se ha creado tu cuenta de acceso al sistema de control de acceso residencial.
                 Estas son tus credenciales para iniciar sesión:</p>
              <table style="border-collapse: collapse; margin: 16px 0;">
                <tr>
                  <td style="padding: 6px 12px; color: #64748b;">Correo</td>
                  <td style="padding: 6px 12px; font-weight: bold;">{WebUtility.HtmlEncode(loginEmail)}</td>
                </tr>
                <tr>
                  <td style="padding: 6px 12px; color: #64748b;">Contraseña</td>
                  <td style="padding: 6px 12px; font-weight: bold;">{WebUtility.HtmlEncode(password)}</td>
                </tr>
              </table>
              <p>También puedes iniciar sesión usando tu número de casa en lugar del correo.</p>
              <p style="color: #64748b; font-size: 13px;">Por seguridad, te recomendamos cambiar la
                 contraseña después de tu primer ingreso.</p>
            </div>
            """;

        private static string BuildResetBody(string name, string loginEmail, string password) => $"""
            <div style="font-family: Arial, sans-serif; color: #1e293b;">
              <h2 style="color: #2563eb;">Restablecimiento de contraseña</h2>
              <p>Hola {WebUtility.HtmlEncode(name)},</p>
              <p>Un administrador restableció la contraseña de tu cuenta de ResidentPass.
                 Usa esta contraseña temporal para iniciar sesión:</p>
              <table style="border-collapse: collapse; margin: 16px 0;">
                <tr>
                  <td style="padding: 6px 12px; color: #64748b;">Correo</td>
                  <td style="padding: 6px 12px; font-weight: bold;">{WebUtility.HtmlEncode(loginEmail)}</td>
                </tr>
                <tr>
                  <td style="padding: 6px 12px; color: #64748b;">Contraseña temporal</td>
                  <td style="padding: 6px 12px; font-weight: bold;">{WebUtility.HtmlEncode(password)}</td>
                </tr>
              </table>
              <p>Al iniciar sesión se te pedirá crear una contraseña nueva.</p>
              <p style="color: #64748b; font-size: 13px;">Si no solicitaste este cambio, contacta a la administración.</p>
            </div>
            """;

        private static string BuildResetLinkBody(string name, string resetUrl) => $"""
            <div style="font-family: Arial, sans-serif; color: #1e293b;">
              <h2 style="color: #2563eb;">Recupera tu contraseña</h2>
              <p>Hola {WebUtility.HtmlEncode(name)},</p>
              <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta de ResidentPass.
                 Haz clic en el siguiente botón para crear una nueva:</p>
              <p style="margin: 24px 0;">
                <a href="{WebUtility.HtmlEncode(resetUrl)}"
                   style="background: #2563eb; color: #ffffff; padding: 10px 20px; border-radius: 8px;
                          text-decoration: none; font-weight: bold; display: inline-block;">
                  Restablecer contraseña
                </a>
              </p>
              <p style="color: #64748b; font-size: 13px;">Este enlace vence en 1 hora y solo puede usarse una vez.
                 Si no solicitaste este cambio, puedes ignorar este correo.</p>
            </div>
            """;
    }
}

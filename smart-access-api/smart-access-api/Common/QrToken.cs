using System.Security.Cryptography;
using System.Text;

namespace smart_access_api.Common
{
    // Genera y verifica el token firmado que viaja dentro del código QR.
    // Formato: "{id}.{firma}" donde firma = Base64Url(HMACSHA256(id, secret)).
    //
    // Por qué firmado: el PDF exige que cada QR contenga un "token único firmado y
    // verificado en el backend". Así, aunque alguien copie el formato, no puede
    // fabricar un token válido sin la clave secreta del servidor → previene
    // suplantación. El `id` además es el Id del documento en Firestore, por lo que
    // validar es una lectura directa (sin query).
    public static class QrToken
    {
        public static string Generate(string id, string secret)
        {
            var signature = Sign(id, secret);
            return $"{id}.{signature}";
        }

        // Verifica la firma y devuelve el id embebido. false si el token es
        // inválido o fue manipulado.
        public static bool TryGetId(string token, string secret, out string id)
        {
            id = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var parts = token.Split('.');
            if (parts.Length != 2)
                return false;

            var candidateId = parts[0];
            var providedSignature = parts[1];
            var expectedSignature = Sign(candidateId, secret);

            // Comparación en tiempo constante para evitar timing attacks.
            var match = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedSignature),
                Encoding.UTF8.GetBytes(expectedSignature));

            if (!match)
                return false;

            id = candidateId;
            return true;
        }

        private static string Sign(string value, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }
}

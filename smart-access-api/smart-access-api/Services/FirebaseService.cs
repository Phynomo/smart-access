using Google.Cloud.Firestore;

namespace smart_access_api.Services
{
    // Su única responsabilidad ahora es construir el FirestoreDb (credenciales +
    // project id). Las operaciones de lectura/escritura viven en FirestoreContext.
    public class FirebaseService
    {
        public FirestoreDb FirestoreDb { get; }

        public FirebaseService()
        {
            var credentialPath = Path.Combine(
                AppContext.BaseDirectory, "Config", "firebase-smart-access.json");
            Environment.SetEnvironmentVariable(
                "GOOGLE_APPLICATION_CREDENTIALS", credentialPath);

            FirestoreDb = FirestoreDb.Create("smart-access-1d12e");
        }
    }
}

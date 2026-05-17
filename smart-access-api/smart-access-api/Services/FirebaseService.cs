
using Google.Cloud.Firestore;

namespace smart_access_api.Services
{
    public class FirebaseService
    {
        private readonly FirestoreDb _firestoreDb;

        public FirebaseService()
        {
            var credentialPath = Path.Combine(AppContext.BaseDirectory, "Config" ,"firebase-smart-access.json");
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
            _firestoreDb = FirestoreDb.Create("smart-access-1d12e");
        }

        public CollectionReference GetCollection(string collectionName)
        {
            return _firestoreDb.Collection(collectionName);
        }

        

    }
}

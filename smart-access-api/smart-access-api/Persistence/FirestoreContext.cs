using Google.Cloud.Firestore;

namespace smart_access_api.Persistence
{
    // Equivalente Firestore de un DbContext: centraliza el acceso a las
    // colecciones y expone "DbSets" tipados (CollectionReference por entidad).
    public class FirestoreContext
    {
        private readonly FirestoreDb _db;

        public FirestoreContext(FirestoreDb db)
        {
            _db = db;
        }

        // "DbSets" — referencias tipadas a las colecciones.
        public CollectionReference Users => _db.Collection(CollectionNames.Users);
        public CollectionReference Residents => _db.Collection(CollectionNames.Residents);
        public CollectionReference Vehicles => _db.Collection(CollectionNames.Vehicles);
        public CollectionReference QRCodes => _db.Collection(CollectionNames.QRCodes);
        public CollectionReference AccessEvents => _db.Collection(CollectionNames.AccessEvents);
        public CollectionReference LabNotes => _db.Collection(CollectionNames.LabNotes);

        // Acceso crudo al FirestoreDb para casos avanzados (transacciones,
        // batched writes, colecciones que no figuren en el contexto).
        public FirestoreDb Db => _db;

        // Permite iniciar una transacción que abarca varias colecciones de este
        // contexto (clave para "marcar QR como usado + crear AccessEvent"
        // atómicamente — requerimiento del PDF).
        public Task<T> RunTransactionAsync<T>(Func<Transaction, Task<T>> callback) =>
            _db.RunTransactionAsync(callback);
    }
}

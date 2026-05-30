namespace smart_access_api.Persistence
{
    // Nombres de colecciones Firestore centralizados.
    // Análogo (modesto) al ToTable("...") de EF Core: un único lugar donde se decide
    // cómo se llama la colección en la base.
    public static class CollectionNames
    {
        public const string Users = "users";
        public const string Residents = "residents";
        public const string Vehicles = "vehicles";
        public const string QRCodes = "qrcodes";
        public const string AccessEvents = "accessevents";
        public const string LabNotes = "labnotes";

        // Opcional: agregado precalculado del dashboard. Sólo se usa si se
        // habilita el caché de estadísticas (ver MODELS.md §6).
        public const string DailyStats = "dailystats";
    }
}

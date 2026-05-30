namespace smart_access_api.Models
{
    public static class UserRoles
    {
        public const string Admin = "admin";
        public const string Security = "security";
        public const string Resident = "resident";
    }

    public static class QrTypes
    {
        public const string Permanent = "permanent";
        public const string Date = "date";
        public const string LongTerm = "long_term";
    }

    public static class EventTypes
    {
        public const string Entry = "entry";
        public const string Exit = "exit";
    }

    public static class AccessMethods
    {
        public const string Qr = "qr";
        public const string Manual = "manual";
    }

    public static class AccessResults
    {
        public const string Authorized = "authorized";
        public const string Rejected = "rejected";
    }

    // Categorías válidas para una LabNote.
    public static class LabCategories
    {
        public const string Quimica = "Quimica";
        public const string Biologia = "Biologia";
        public const string Fisica = "Fisica";
        public const string Otro = "Otro";

        public static bool IsValid(string category) =>
            category == Quimica || category == Biologia ||
            category == Fisica || category == Otro;
    }
}

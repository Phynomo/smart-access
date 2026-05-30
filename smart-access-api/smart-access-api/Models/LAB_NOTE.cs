using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class Lab_Note
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("title")]
        public string? Title { get; set; }

        [FirestoreProperty("observation")]
        public string Observation { get; set; } = string.Empty;

        [FirestoreProperty("category")]
        public string? Category { get; set; }

        [FirestoreProperty("priority")]
        public int? Priority { get; set; }

        [FirestoreProperty("ispublic")]
        public bool? IsPublic { get; set; }

        [FirestoreProperty("tags")]
        public string? Tags { get; set; }

        // EventTypes: Entry | Exit
        [FirestoreProperty("createat")]
        public DateTime CreateAt { get; set; } = EventTypes.Entry;

        // AccessMethods: Qr | Manual
        [FirestoreProperty("useid")]
        public string UserId { get; set; } = AccessMethods.Qr;

    }
}

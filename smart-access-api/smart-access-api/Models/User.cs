namespace smart_access_api.Models
{
    public class User
    {
        public string Id { get; set; } = string.Empty; 
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

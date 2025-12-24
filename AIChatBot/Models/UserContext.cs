// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Models\UserContext.cs
namespace AIChatBot.Models
{
    public class UserContext
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = "User";  // ✅ Eklendi
        public List<int> AllowedProductIds { get; set; } = new();  // ✅ Eklendi
    }
}
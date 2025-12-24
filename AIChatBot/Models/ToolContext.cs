// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Models\ToolContext.cs
namespace AIChatBot.Models
{
    public class ToolContext
    {
        public string UserId { get; set; } = string.Empty;  // ✅ string
        public string Role { get; set; } = "User";
        public List<int> AllowedProductIds { get; set; } = new();
    }
}
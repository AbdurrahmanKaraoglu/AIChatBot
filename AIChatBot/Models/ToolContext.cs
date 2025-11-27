// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Models\ToolContext.cs
namespace AIChatBot.Models
{
    public class ToolContext
    {
        public int UserId { get; set; }
        public string Role { get; set; } = "User";  // "Admin", "Customer", "Moderator"
        public List<int> AllowedProductIds { get; set; } = new();  // Erişebileceği ürünler
    }
}
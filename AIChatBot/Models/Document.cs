// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Models\Document.cs
namespace AIChatBot.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public bool HasEmbedding { get; set; }  // ✅ Yeni property
        public DateTime CreatedDate { get; set; }
    }
}
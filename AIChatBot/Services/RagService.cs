using AIChatBot.Models;

namespace AIChatBot.Services
{
    public class RagService
    {
        private readonly List<Document> _documents = new()
        {
            new Document { Id = 1, Title = "Ürünler", Category = "Info", Content = "Ürün A: 500TL, Ürün B: 1500TL" },
            new Document { Id = 2, Title = "Kargo", Category = "FAQ", Content = "Kargo 100TL üzeri bedava." }
        };

        public List<Document> SearchDocuments(string query)
        {
            return _documents
                .Where(d => d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           d.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public string FormatDocumentsAsContext(List<Document> documents)
        {
            if (!documents.Any()) return "";
            return "BİLGİ BANKASI:\n" + string.Join("\n", documents.Select(d => $"- {d.Title}: {d.Content}"));
        }

        public List<Document> GetAllDocuments() => _documents;
    }
}
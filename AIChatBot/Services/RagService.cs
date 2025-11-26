using AIChatBot.Models;

namespace AIChatBot.Services
{
    public class RagService
    {
        private readonly List<Document> _documents = new()
        {
            new Document
            {
                Id = 1,
                Title = "Ürünler",
                Category = "Info",
                Content = "Ürün A: 500 TL, Ürün B: 1500 TL.  Her iki ürün de stokta mevcuttur."
            },
            new Document
            {
                Id = 2,
                Title = "Kargo",
                Category = "FAQ",
                Content = "Kargo ücreti 100 TL ve üzeri siparişlerde ücretsizdir.  100 TL altı siparişlerde kargo ücreti 30 TL'dir.  Kargolar 2-5 iş günü içinde teslim edilir."
            },
            new Document
            {
                Id = 3,
                Title = "İade",
                Category = "FAQ",
                Content = "Ürünü teslim aldıktan sonra 14 gün içinde iade edebilirsiniz.  İade için ürünün kullanılmamış ve ambalajında olması gerekir.  İade kargo ücreti tarafımızca karşılanır."
            },
            new Document
            {
                Id = 4,
                Title = "Ödeme",
                Category = "Info",
                Content = "Kredi kartı, banka havalesi ve kapıda ödeme seçeneklerini kabul ediyoruz. Taksit imkanları mevcuttur."
            }
        };

        public List<Document> SearchDocuments(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Document>();

            var lowerQuery = query.ToLower();

            return _documents
                .Where(d =>
                    d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    d.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (lowerQuery.Contains("ürün") || lowerQuery.Contains("fiyat")) && d.Title == "Ürünler" ||
                    (lowerQuery.Contains("kargo") || lowerQuery.Contains("teslimat")) && d.Title == "Kargo" ||
                    (lowerQuery.Contains("iade") || lowerQuery.Contains("geri")) && d.Title == "İade" ||
                    (lowerQuery.Contains("ödeme") || lowerQuery.Contains("ücret") || lowerQuery.Contains("taksit")) && d.Title == "Ödeme"
                )
                .ToList();
        }

        public string FormatDocumentsAsContext(List<Document> documents)
        {
            if (!documents.Any()) return "";

            return "BİLGİ BANKASI:\n" +
                   string.Join("\n", documents.Select(d => $"• {d.Title}: {d.Content}"));
        }

        public List<Document> GetAllDocuments() => _documents;
    }
}
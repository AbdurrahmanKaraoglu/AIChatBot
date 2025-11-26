using AIChatBot.Models;
using AIChatBot.Repository.KnowledgeBase;

namespace AIChatBot.Services
{
    public class RagService
    {
        private readonly IKnowledgeBaseRepository _knowledgeBaseRepository;
        private readonly ILogger<RagService> _logger;

        public RagService(IKnowledgeBaseRepository knowledgeBaseRepository, ILogger<RagService> logger)
        {
            _knowledgeBaseRepository = knowledgeBaseRepository;
            _logger = logger;
        }

        // ✅ YENİ: AI için akıllı arama
        public async Task<List<Document>> SmartSearchForAI(string userQuery)
        {
            // Fiyat aralığı çıkar (örn: "500-1000 TL arası")
            decimal? minPrice = null;
            decimal? maxPrice = null;

            var priceMatch = System.Text.RegularExpressions.Regex.Match(
                userQuery,
                @"(\d+)\s*-\s*(\d+)\s*TL"
            );

            if (priceMatch.Success)
            {
                minPrice = decimal.Parse(priceMatch.Groups[1].Value);
                maxPrice = decimal.Parse(priceMatch.Groups[2].Value);
                _logger.LogInformation($"[AI-SMART-SEARCH] Fiyat aralığı tespit edildi: {minPrice}-{maxPrice} TL");
            }

            // Kategori çıkar
            string? category = null;
            if (userQuery.ToLower().Contains("bilgisayar")) category = "Bilgisayar";
            else if (userQuery.ToLower().Contains("elektronik")) category = "Elektronik";
            else if (userQuery.ToLower().Contains("aksesuar")) category = "Aksesuar";

            // Akıllı arama yap
            if (minPrice.HasValue || maxPrice.HasValue || category != null)
            {
                return await _knowledgeBaseRepository.SmartProductSearch(
                    userQuery,
                    minPrice,
                    maxPrice,
                    category
                );
            }

            // Normal arama
            return await SearchDocumentsAsync(userQuery);
        }

        public async Task<List<Document>> SearchDocumentsAsync(string query)
        {
            // ✅ Türkçe stopwords'leri çıkar ve keyword'leri ayır
            var keywords = ExtractKeywords(query);

            _logger.LogInformation($"[RAG] Query: '{query}' → Keywords: {string.Join(", ", keywords)}");

            var allDocuments = new List<Document>();

            // Her keyword için arama yap
            foreach (var keyword in keywords)
            {
                var docs = await _knowledgeBaseRepository.SearchDocuments(keyword);
                allDocuments.AddRange(docs);
            }

            // Duplicate'leri temizle
            var uniqueDocs = allDocuments
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation($"[RAG] Toplam {uniqueDocs.Count} benzersiz belge bulundu");

            return uniqueDocs;
        }

        private List<string> ExtractKeywords(string query)
        {
            // Türkçe stopwords (gereksiz kelimeler)
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bir", "ve", "veya", "ile", "için", "ne", "nedir", "nasıl",
                "mi", "mu", "mı", "mü", "da", "de", "ta", "te",
                "kaç", "hangi", "şu", "bu", "o"
            };

            // ✅ String array kullan (char array yerine)
            var separators = new[] { " ", "? ", "!", ".", ",", ";", ":" };

            var words = query
                .ToLowerInvariant()
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopwords.Contains(w))
                .Distinct()
                .ToList();

            // En az bir keyword yoksa orijinal query'yi kullan
            return words.Any() ? words : new List<string> { query };
        }

        public string FormatDocumentsAsContext(List<Document> documents)
        {
            if (!documents.Any()) return "";

            return "BİLGİ BANKASI:\n" +
                   string.Join("\n", documents.Select(d => $"• {d.Title}: {d.Content}"));
        }

        public async Task<List<Document>> GetAllDocumentsAsync()
        {
            return await _knowledgeBaseRepository.GetAllDocuments();
        }
    }
}
// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Services\RagService.cs
using AIChatBot.Models;
using AIChatBot.Repository.KnowledgeBase;

namespace AIChatBot.Services
{
    public class RagService
    {
        private readonly IKnowledgeBaseRepository _knowledgeBaseRepository;
        private readonly EmbeddingService? _embeddingService;  // ✅ Nullable (henüz eklenmemiş olabilir)
        private readonly ILogger<RagService> _logger;

        public RagService(
            IKnowledgeBaseRepository knowledgeBaseRepository,
            ILogger<RagService> logger,
            EmbeddingService? embeddingService = null)  // ✅ Opsiyonel
        {
            _knowledgeBaseRepository = knowledgeBaseRepository;
            _logger = logger;
            _embeddingService = embeddingService;
        }

        /// <summary>
        /// Semantic search (Vector-based) - Öncelikli arama yöntemi
        /// </summary>
        // Services/RagService.cs - DÜZELTME

        public async Task<List<Document>> SemanticSearchAsync(string query, int topK = 5)
        {
            if (_embeddingService == null)
            {
                _logger.LogWarning("[RAG] EmbeddingService bulunamadı, keyword search kullanılıyor");
                return await SearchDocumentsAsync(query);
            }

            try
            {
                _logger.LogInformation("[SEMANTIC-SEARCH] Query: '{Query}', TopK: {TopK}", query, topK);

                // 1. Query'yi vektöre çevir (JSON formatında)
                var queryVectorJson = await _embeddingService.GetEmbeddingAsJsonAsync(query);

                _logger.LogDebug("[SEMANTIC-SEARCH] Embedding JSON oluşturuldu");

                // 2. Vector search yap (JSON parametreli yeni metot)
                var results = await _knowledgeBaseRepository.VectorSearchWithJsonAsync(
                    queryVectorJson,
                    topK,
                    minSimilarity: 0.5
                );

                _logger.LogInformation(
                    "[SEMANTIC-SEARCH] {Count} belge bulundu",
                    results.Count
                );

                return results.Select(r => r.Doc).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[SEMANTIC-SEARCH-ERROR] Query:  '{Query}', Fallback to keyword search",
                    query
                );

                return await SearchDocumentsAsync(query);
            }
        }

        /// <summary>
        /// Keyword-based search (Fallback yöntemi)
        /// </summary>
        public async Task<List<Document>> SearchDocumentsAsync(string query)
        {
            _logger.LogInformation("[KEYWORD-SEARCH] Query: '{Query}'", query);

            // Türkçe stopwords'leri çıkar ve keyword'leri ayır
            var keywords = ExtractKeywords(query);

            _logger.LogDebug(
                "[KEYWORD-SEARCH] Keywords: {Keywords}",
                string.Join(", ", keywords)
            );

            var allDocuments = new List<Document>();

            // Her keyword için arama yap
            foreach (var keyword in keywords)
            {
                var docs = await _knowledgeBaseRepository.SearchDocuments(keyword);
                allDocuments.AddRange(docs);

                _logger.LogDebug(
                    "[KEYWORD-SEARCH] Keyword '{Keyword}': {Count} belge bulundu",
                    keyword,
                    docs.Count
                );
            }

            // Duplicate'leri temizle
            var uniqueDocs = allDocuments
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation(
                "[KEYWORD-SEARCH] Toplam {Count} benzersiz belge bulundu",
                uniqueDocs.Count
            );

            return uniqueDocs;
        }

        /// <summary>
        /// Akıllı arama (Fiyat + Kategori filtreli)
        /// </summary>
        public async Task<List<Document>> SmartSearchForAI(string userQuery)
        {
            _logger.LogInformation("[SMART-SEARCH] Query: '{Query}'", userQuery);

            // Fiyat aralığı çıkar (örn: "500-1000 TL arası")
            var (minPrice, maxPrice) = ExtractPriceRange(userQuery);

            // Kategori çıkar
            var category = ExtractCategory(userQuery);

            // Akıllı arama yap
            if (minPrice.HasValue || maxPrice.HasValue || category != null)
            {
                _logger.LogInformation(
                    "[SMART-SEARCH] Filtreler: MinPrice={MinPrice}, MaxPrice={MaxPrice}, Category={Category}",
                    minPrice,
                    maxPrice,
                    category
                );

                var results = await _knowledgeBaseRepository.SmartProductSearch(
                    userQuery,
                    minPrice,
                    maxPrice,
                    category
                );

                _logger.LogInformation("[SMART-SEARCH] {Count} ürün bulundu", results.Count);

                return results;
            }

            // Filtre yoksa semantic search dene
            _logger.LogDebug("[SMART-SEARCH] Filtre yok, semantic search deneniyor");
            return await SemanticSearchAsync(userQuery, topK: 5);
        }

        /// <summary>
        /// Belgeleri context formatına çevirir (LLM için)
        /// </summary>
        public string FormatDocumentsAsContext(List<Document> documents)
        {
            if (documents == null || !documents.Any())
            {
                _logger.LogWarning("[FORMAT] Belge listesi boş");
                return "";
            }

            _logger.LogDebug("[FORMAT] {Count} belge formatlanıyor", documents.Count);

            var context = "📚 BİLGİ BANKASI:\n\n";

            foreach (var doc in documents)
            {
                // İçeriği kısalt (çok uzunsa)
                var content = doc.Content.Length > 500
                    ? doc.Content.Substring(0, 500) + "..."
                    : doc.Content;

                context += $"• **{doc.Title}**\n  {content}\n\n";
            }

            context += "⚠️ SADECE yukarıdaki bilgileri kullan!\n";

            return context;
        }

        /// <summary>
        /// Tüm aktif belgeleri getirir
        /// </summary>
        public async Task<List<Document>> GetAllDocumentsAsync()
        {
            _logger.LogInformation("[GET-ALL] Tüm belgeler istendi");

            var documents = await _knowledgeBaseRepository.GetAllDocuments();

            _logger.LogInformation("[GET-ALL] {Count} belge bulundu", documents.Count);

            return documents;
        }

        /// <summary>
        /// Keyword extraction (Türkçe NLP)
        /// </summary>
        private List<string> ExtractKeywords(string query)
        {
            // Türkçe stopwords (gereksiz kelimeler)
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bir", "ve", "veya", "ile", "için", "ne", "nedir", "nasıl",
                "mi", "mu", "mı", "mü", "da", "de", "ta", "te",
                "kaç", "hangi", "şu", "bu", "o", "olan", "olarak",
                "ise", "eğer", "ancak", "ama", "fakat", "ya", "yani"
            };

            var separators = new[] { " ", "? ", "!", ".", ",", ";", ":" };

            var words = query
                .ToLowerInvariant()
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopwords.Contains(w))
                .Distinct()
                .ToList();

            _logger.LogDebug(
                "[KEYWORD-EXTRACTION] '{Query}' → {Count} keyword",
                query,
                words.Count
            );

            // En az bir keyword yoksa orijinal query'yi kullan
            return words.Any() ? words : new List<string> { query };
        }

        /// <summary>
        /// Fiyat aralığı çıkarır (Regex ile)
        /// </summary>
        private (decimal? minPrice, decimal? maxPrice) ExtractPriceRange(string query)
        {
            // Pattern: "500-1000 TL", "500 ile 1000 TL arası"
            var patterns = new[]
            {
                @"(\d+)\s*-\s*(\d+)\s*TL",                    // 500-1000 TL
                @"(\d+)\s+ile\s+(\d+)\s*TL",                  // 500 ile 1000 TL
                @"(\d+)\s*TL\s*ile\s*(\d+)\s*TL",            // 500 TL ile 1000 TL
                @"(\d+)\s*TL.*?(\d+)\s*TL\s+arası"           // 500 TL 1000 TL arası
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(query, pattern);
                if (match.Success)
                {
                    var min = decimal.Parse(match.Groups[1].Value);
                    var max = decimal.Parse(match.Groups[2].Value);

                    _logger.LogDebug(
                        "[PRICE-EXTRACTION] Fiyat aralığı: {Min}-{Max} TL",
                        min,
                        max
                    );

                    return (min, max);
                }
            }

            // Tek fiyat (maksimum olarak kullan)
            var singlePriceMatch = System.Text.RegularExpressions.Regex.Match(
                query,
                @"(\d+)\s*TL\s+(altı|altında|kadar)"
            );

            if (singlePriceMatch.Success)
            {
                var price = decimal.Parse(singlePriceMatch.Groups[1].Value);
                _logger.LogDebug("[PRICE-EXTRACTION] Max fiyat: {Price} TL", price);
                return (null, price);
            }

            return (null, null);
        }

        /// <summary>
        /// Kategori çıkarır (Pattern matching)
        /// </summary>
        private string? ExtractCategory(string query)
        {
            var queryLower = query.ToLowerInvariant();

            var categoryMappings = new Dictionary<string, string[]>
            {
                { "Bilgisayar", new[] { "bilgisayar", "laptop", "pc", "masaüstü", "notebook" } },
                { "Elektronik", new[] { "elektronik", "telefon", "tablet", "akıllı saat" } },
                { "Aksesuar", new[] { "aksesuar", "kulaklık", "kablo", "şarj", "kılıf" } },
                { "Ev", new[] { "ev", "mobilya", "dekorasyon" } },
                { "Giyim", new[] { "giyim", "kıyafet", "ayakkabı", "çanta" } }
            };

            foreach (var (category, keywords) in categoryMappings)
            {
                if (keywords.Any(k => queryLower.Contains(k)))
                {
                    _logger.LogDebug("[CATEGORY-EXTRACTION] Kategori: {Category}", category);
                    return category;
                }
            }

            return null;
        }
    }
}
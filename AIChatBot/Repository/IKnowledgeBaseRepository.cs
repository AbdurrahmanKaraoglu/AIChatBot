// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Repository\IKnowledgeBaseRepository.cs
using AIChatBot.Models;

namespace AIChatBot.Repository.KnowledgeBase
{
    /// <summary>
    /// KnowledgeBase repository interface
    /// </summary>
    public interface IKnowledgeBaseRepository
    {
        /// <summary>
        /// Keyword-based search (basit arama)
        /// </summary>
        Task<List<Document>> SearchDocuments(string query);

        /// <summary>
        /// Tüm aktif belgeleri getirir
        /// </summary>
        Task<List<Document>> GetAllDocuments();

        /// <summary>
        /// Akıllı ürün arama (Fiyat + Kategori filtreli)
        /// </summary>
        Task<List<Document>> SmartProductSearch(
            string query,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? category = null
        );

        /// <summary>
        /// Vector-based semantic search (SQL Server 2025 VECTOR)
        /// </summary>
        Task<List<(Document Doc, float Similarity)>> VectorSearchAsync(
            float[] queryVector,
            int topK = 5,
            float minSimilarity = 0.5f
        );

        /// <summary>
        /// Belgeye embedding ekler (migration için)
        /// </summary>
        Task UpdateDocumentEmbeddingAsync(int documentId, float[] embedding);

        /// <summary>
        /// Belge ekler (embedding ile)
        /// </summary>
        Task<int> AddDocumentAsync(Document document, float[] embedding);

        /// <summary>
        /// Belge günceller
        /// </summary>
        Task UpdateDocumentAsync(Document document);

        /// <summary>
        /// Belge siler (soft delete)
        /// </summary>
        Task DeleteDocumentAsync(int documentId);

        /// <summary>
        /// Belge ID'sine göre getirir
        /// </summary>
        Task<Document?> GetDocumentByIdAsync(int documentId);



        // ✅ YENİ METODLAR - Migration için
  
        Task<Document?> GetDocumentById(int documentId);
        Task<bool> UpdateEmbedding(int documentId, string embeddingJson);

        // ✅ YENİ: Full-Text Search
        Task<List<Document>> FullTextSearchAsync(string query, int topN = 10);

        // ✅ YENİ:  Kategori listesi
        Task<List<string>> GetAllCategoriesAsync();

        // ✅ YENİ: Vector Search JSON parametreli
        Task<List<(Document Doc, double Similarity)>> VectorSearchWithJsonAsync(
            string queryVectorJson,
            int topK = 5,
            double minSimilarity = 0.5
        );
    }
}
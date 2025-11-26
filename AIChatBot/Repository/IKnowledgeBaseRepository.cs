using AIChatBot.Models;

namespace AIChatBot.Repository.KnowledgeBase
{
    public interface IKnowledgeBaseRepository
    {
        Task<List<Document>> SearchDocuments(string query);
        Task<List<Document>> GetAllDocuments();

        // ✅ YENİ: Akıllı ürün arama
        Task<List<Document>> SmartProductSearch(
            string query,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? category = null
        );
    }
}
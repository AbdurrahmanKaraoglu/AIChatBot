using AIChatBot.Models;

namespace AIChatBot.Repository.KnowledgeBase
{
    public interface IKnowledgeBaseRepository
    {
        Task<List<Document>> SearchDocuments(string query);
        Task<List<Document>> GetAllDocuments();
    }
}
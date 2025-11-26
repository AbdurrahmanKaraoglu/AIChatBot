using AIChatBot.Models;
using System.Data;
using System.Data.SqlClient;

namespace AIChatBot.Repository.KnowledgeBase
{
    public class KnowledgeBaseRepository : IKnowledgeBaseRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<KnowledgeBaseRepository> _logger;

        public KnowledgeBaseRepository(IConfiguration configuration, ILogger<KnowledgeBaseRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
        }

        public async Task<List<Document>> SearchDocuments(string query)
        {
            List<Document> documents = new List<Document>();

            try
            {
                // ✅ DEBUG: Connection string ve query logla
                _logger.LogInformation($"[RAG-DB] SearchDocuments çağrıldı.  Query: '{query}'");
                _logger.LogInformation($"[RAG-DB] Connection String: {_connectionString}");

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_SearchKnowledgeBase", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@SearchQuery", SqlDbType.NVarChar, 200)
                        {
                            Value = query ?? ""
                        });

                        await conn.OpenAsync();
                        _logger.LogInformation($"[RAG-DB] Veritabanı bağlantısı açıldı");

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            int rowCount = 0;

                            while (await reader.ReadAsync())
                            {
                                rowCount++;

                                var doc = new Document
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                                    Title = reader.GetString(reader.GetOrdinal("Title")),
                                    Content = reader.GetString(reader.GetOrdinal("Content")),
                                    Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                        ? ""
                                        : reader.GetString(reader.GetOrdinal("Category"))
                                };

                                documents.Add(doc);

                                // ✅ DEBUG: Her belgeyi logla
                                _logger.LogInformation($"[RAG-DB] Belge #{rowCount}: ID={doc.Id}, Title={doc.Title}");
                            }

                            _logger.LogInformation($"[RAG-DB] Toplam {rowCount} satır okundu");
                        }
                    }
                }

                _logger.LogInformation($"[RAG-DB] SearchDocuments tamamlandı. Bulunan: {documents.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[RAG-DB] SearchDocuments hatası: {ex.Message}");
                _logger.LogError($"[RAG-DB] StackTrace: {ex.StackTrace}");
            }

            return documents;
        }

        public async Task<List<Document>> GetAllDocuments()
        {
            List<Document> documents = new List<Document>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT DocumentId, Title, Content, Category FROM dbo.KnowledgeBase WHERE IsActive = 1", conn))
                    {
                        cmd.CommandType = CommandType.Text;

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                documents.Add(new Document
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                                    Title = reader.GetString(reader.GetOrdinal("Title")),
                                    Content = reader.GetString(reader.GetOrdinal("Content")),
                                    Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                        ? ""
                                        : reader.GetString(reader.GetOrdinal("Category"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetAllDocuments hatası: {ex.Message}");
            }

            return documents;
        }
    }
}
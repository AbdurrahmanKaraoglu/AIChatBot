// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Repository\KnowledgeBaseRepository.cs
using AIChatBot.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace AIChatBot.Repository.KnowledgeBase
{
    public class KnowledgeBaseRepository : IKnowledgeBaseRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<KnowledgeBaseRepository> _logger;

        public KnowledgeBaseRepository(
            IConfiguration configuration,
            ILogger<KnowledgeBaseRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection bulunamadı");
            _logger = logger;
        }

        #region Keyword Search

        public async Task<List<Document>> SearchDocuments(string query)
        {
            var documents = new List<Document>();

            try
            {
                _logger.LogInformation("[SEARCH-KEYWORD] Query: '{Query}'", query);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_SearchKnowledgeBase", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@SearchQuery", SqlDbType.NVarChar, 200) { Value = query ?? "" });

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                documents.Add(ReadDocumentFromReader(reader));
                            }
                        }
                    }
                }

                _logger.LogInformation("[SEARCH-KEYWORD] {Count} belge bulundu", documents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SEARCH-KEYWORD-ERROR] Query: '{Query}'", query);
                throw;
            }

            return documents;
        }

        #endregion

        #region Smart Product Search

        public async Task<List<Document>> SmartProductSearch(
            string query,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? category = null)
        {
            var documents = new List<Document>();

            try
            {
                _logger.LogInformation(
                    "[SMART-SEARCH] Query: '{Query}', MinPrice: {MinPrice}, MaxPrice: {MaxPrice}, Category: {Category}",
                    query, minPrice, maxPrice, category
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_SmartProductSearch", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@SearchQuery", SqlDbType.NVarChar, 200) { Value = query ?? "" });
                        cmd.Parameters.Add(new SqlParameter("@MinPrice", SqlDbType.Decimal) { Value = (object?)minPrice ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@MaxPrice", SqlDbType.Decimal) { Value = (object?)maxPrice ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@Category", SqlDbType.NVarChar, 100) { Value = (object?)category ?? DBNull.Value });

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                documents.Add(ReadDocumentFromReader(reader));
                            }
                        }
                    }
                }

                _logger.LogInformation("[SMART-SEARCH] {Count} ürün bulundu", documents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SMART-SEARCH-ERROR] Query: '{Query}'", query);
                throw;
            }

            return documents;
        }

        #endregion

        #region Vector Search (SQL Server 2025 VECTOR)

        /// <summary>
        /// Vector-based semantic search (SQL Server 2025 VECTOR_DISTANCE)
        /// </summary>
        public async Task<List<(Document Doc, float Similarity)>> VectorSearchAsync(
       float[] queryVector,
       int topK = 5,
       float minSimilarity = 0.5f)
        {
            var results = new List<(Document, float)>();

            try
            {
                if (queryVector == null || queryVector.Length != 768)
                    throw new ArgumentException("Query vector 768-boyutlu olmalı", nameof(queryVector));

                _logger.LogInformation(
                    "[VECTOR-SEARCH] TopK: {TopK}, MinSimilarity: {MinSimilarity}",
                    topK, minSimilarity
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_VectorSearch", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // ✅ DÜZELTME: Sadece JSON string gönder (CAST SQL'de yapılacak)
                        var vectorJson = "[" + string.Join(",", queryVector.Select(v => v.ToString("G", System.Globalization.CultureInfo.InvariantCulture))) + "]";

                        _logger.LogDebug("[VECTOR-SEARCH] Vector JSON length: {Length}", vectorJson.Length);

                        // ✅ Sadece JSON string olarak gönder
                        cmd.Parameters.Add(new SqlParameter("@QueryVector", SqlDbType.NVarChar)
                        {
                            Value = vectorJson  // CAST yok! 
                        });
                        cmd.Parameters.Add(new SqlParameter("@TopK", SqlDbType.Int) { Value = topK });
                        cmd.Parameters.Add(new SqlParameter("@MinSimilarity", SqlDbType.Float) { Value = minSimilarity });

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var doc = new Document
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                                    Title = reader.GetString(reader.GetOrdinal("Title")),
                                    Content = reader.GetString(reader.GetOrdinal("Content")),
                                    Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                        ? ""
                                        : reader.GetString(reader.GetOrdinal("Category"))
                                };

                                var similarity = (float)reader.GetDouble(reader.GetOrdinal("Similarity"));

                                results.Add((doc, similarity));

                                _logger.LogDebug(
                                    "[VECTOR-SEARCH] • {Title} (Similarity: {Similarity:P2})",
                                    doc.Title, similarity
                                );
                            }
                        }
                    }
                }

                _logger.LogInformation("[VECTOR-SEARCH] {Count} belge bulundu", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VECTOR-SEARCH-ERROR] TopK: {TopK}", topK);
                throw;
            }

            return results;
        }

        #endregion

        #region Document CRUD

        // GetAllDocuments metodu ekle
        public async Task<List<Document>> GetAllDocuments()
        {
            var documents = new List<Document>();

            try
            {
                _logger.LogInformation("[REPO] Tüm belgeler getiriliyor...");

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT DocumentId, Title, Content, Category, Tags, Price, Embedding, CreatedDate FROM dbo.KnowledgeBase WHERE IsActive = 1", conn))
                    {
                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var doc = new Document
                                {
                                    Id = reader.GetInt32(0),
                                    Title = reader.GetString(1),
                                    Content = reader.GetString(2),
                                    Category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Tags = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Price = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                                    HasEmbedding = !reader.IsDBNull(6),  // ✅ Embedding var mı?
                                    CreatedDate = reader.GetDateTime(7)
                                };

                                documents.Add(doc);
                            }
                        }
                    }
                }

                _logger.LogInformation("[REPO] {Count} belge getirildi", documents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPO-ERROR] GetAllDocuments hatası");
                throw;
            }

            return documents;
        }

        // GetDocumentById metodu ekle
        public async Task<Document?> GetDocumentById(int documentId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_GetById", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new Document
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                                    Title = reader.GetString(reader.GetOrdinal("Title")),
                                    Content = reader.GetString(reader.GetOrdinal("Content")),
                                    Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                        ? ""
                                        : reader.GetString(reader.GetOrdinal("Category")),
                                    Tags = reader.IsDBNull(reader.GetOrdinal("Tags"))
                                        ? ""
                                        : reader.GetString(reader.GetOrdinal("Tags")),
                                    Price = reader.IsDBNull(reader.GetOrdinal("Price"))
                                        ? (decimal?)null
                                        : reader.GetDecimal(reader.GetOrdinal("Price"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPO-ERROR] GetDocumentById: {DocumentId}", documentId);
                throw;
            }

            return null;
        }

        // UpdateEmbedding metodu ekle
        public async Task<bool> UpdateEmbedding(int documentId, string embeddingJson)
        {
            try
            {
                _logger.LogDebug("[REPO] DocumentId:{DocumentId} embedding güncelleniyor...", documentId);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_UpdateEmbedding", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));
                        cmd.Parameters.Add(new SqlParameter("@Embedding", SqlDbType.NVarChar)
                        {
                            Value = embeddingJson
                        });

                        await conn.OpenAsync();
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogDebug("[REPO] DocumentId:{DocumentId} - {Rows} satır güncellendi", documentId, rowsAffected);

                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REPO-ERROR] UpdateEmbedding: DocumentId:{DocumentId}", documentId);
                throw;
            }
        }

        public async Task<Document?> GetDocumentByIdAsync(int documentId)
        {
            try
            {
                _logger.LogInformation("[GET-BY-ID] DocumentId: {DocumentId}", documentId);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_GetById", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return ReadDocumentFromReader(reader);
                            }
                        }
                    }
                }

                _logger.LogWarning("[GET-BY-ID] Belge bulunamadı: {DocumentId}", documentId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GET-BY-ID-ERROR] DocumentId: {DocumentId}", documentId);
                throw;
            }
        }

        public async Task<int> AddDocumentAsync(Document document, float[] embedding)
        {
            try
            {
                _logger.LogInformation(
                    "[ADD-DOCUMENT] Title: '{Title}', Category: {Category}",
                    document.Title, document.Category
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_Add", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add(new SqlParameter("@Title", document.Title));
                        cmd.Parameters.Add(new SqlParameter("@Content", document.Content));
                        cmd.Parameters.Add(new SqlParameter("@Category", (object?)document.Category ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Tags", DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Price", DBNull.Value));

                        // ✅ VECTOR parametresi (JSON string)
                        var vectorJson = "[" + string.Join(",", embedding.Select(v => v.ToString("F6"))) + "]";
                        cmd.Parameters.Add(new SqlParameter("@Embedding", SqlDbType.NVarChar)
                        {
                            Value = vectorJson
                        });

                        await conn.OpenAsync();

                        var newId = (int)(await cmd.ExecuteScalarAsync() ?? 0);

                        _logger.LogInformation("[ADD-DOCUMENT] ✅ Eklendi: DocumentId={DocumentId}", newId);

                        return newId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ADD-DOCUMENT-ERROR] Title: '{Title}'", document.Title);
                throw;
            }
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            try
            {
                _logger.LogInformation("[UPDATE-DOCUMENT] DocumentId: {DocumentId}", document.Id);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_Update", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add(new SqlParameter("@DocumentId", document.Id));
                        cmd.Parameters.Add(new SqlParameter("@Title", document.Title));
                        cmd.Parameters.Add(new SqlParameter("@Content", document.Content));
                        cmd.Parameters.Add(new SqlParameter("@Category", (object?)document.Category ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Tags", DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Price", DBNull.Value));

                        await conn.OpenAsync();
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation("[UPDATE-DOCUMENT] ✅ {RowsAffected} satır güncellendi", rowsAffected);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPDATE-DOCUMENT-ERROR] DocumentId: {DocumentId}", document.Id);
                throw;
            }
        }

        public async Task UpdateDocumentEmbeddingAsync(int documentId, float[] embedding)
        {
            try
            {
                _logger.LogInformation("[UPDATE-EMBEDDING] DocumentId: {DocumentId}", documentId);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_UpdateEmbedding", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));

                        // ✅ VECTOR parametresi (JSON string)
                        var vectorJson = "[" + string.Join(",", embedding.Select(v => v.ToString("F6"))) + "]";
                        cmd.Parameters.Add(new SqlParameter("@Embedding", SqlDbType.NVarChar)
                        {
                            Value = vectorJson
                        });

                        await conn.OpenAsync();
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation("[UPDATE-EMBEDDING] ✅ {RowsAffected} satır güncellendi", rowsAffected);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPDATE-EMBEDDING-ERROR] DocumentId: {DocumentId}", documentId);
                throw;
            }
        }

        public async Task DeleteDocumentAsync(int documentId)
        {
            try
            {
                _logger.LogInformation("[DELETE-DOCUMENT] DocumentId: {DocumentId}", documentId);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_Delete", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));

                        await conn.OpenAsync();
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation("[DELETE-DOCUMENT] ✅ {RowsAffected} satır silindi", rowsAffected);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DELETE-DOCUMENT-ERROR] DocumentId: {DocumentId}", documentId);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private Document ReadDocumentFromReader(SqlDataReader reader)
        {
            return new Document
            {
                Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Content = reader.GetString(reader.GetOrdinal("Content")),
                Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                    ? ""
                    : reader.GetString(reader.GetOrdinal("Category"))
            };
        }

        #endregion
    }
}
using AIChatBot.Models;
using System.Data;
using System.Data.SqlClient;

namespace AIChatBot.Repository.KnowledgeBase
{
    /// <summary>
    /// KnowledgeBase repository implementation (ADO.NET)
    /// </summary>
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

        /// <summary>
        /// Keyword-based search (sp_SearchKnowledgeBase)
        /// </summary>
        public async Task<List<Document>> SearchDocuments(string query)
        {
            var documents = new List<Document>();

            try
            {
                _logger.LogInformation(
                    "[SEARCH-KEYWORD] Query: '{Query}'",
                    query
                );

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

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                documents.Add(ReadDocumentFromReader(reader));
                            }
                        }
                    }
                }

                _logger.LogInformation(
                    "[SEARCH-KEYWORD] {Count} belge bulundu",
                    documents.Count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[SEARCH-KEYWORD-ERROR] Query: '{Query}'",
                    query
                );
                throw;
            }

            return documents;
        }

        #endregion

        #region Smart Product Search

        /// <summary>
        /// Akıllı ürün arama (sp_SmartProductSearch)
        /// </summary>
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
                    query,
                    minPrice,
                    maxPrice,
                    category
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_SmartProductSearch", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add(new SqlParameter("@SearchQuery", SqlDbType.NVarChar, 200)
                        {
                            Value = query ?? ""
                        });

                        cmd.Parameters.Add(new SqlParameter("@MinPrice", SqlDbType.Decimal)
                        {
                            Value = (object?)minPrice ?? DBNull.Value
                        });

                        cmd.Parameters.Add(new SqlParameter("@MaxPrice", SqlDbType.Decimal)
                        {
                            Value = (object?)maxPrice ?? DBNull.Value
                        });

                        cmd.Parameters.Add(new SqlParameter("@Category", SqlDbType.NVarChar, 100)
                        {
                            Value = (object?)category ?? DBNull.Value
                        });

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

                _logger.LogInformation(
                    "[SMART-SEARCH] {Count} ürün bulundu",
                    documents.Count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[SMART-SEARCH-ERROR] Query: '{Query}'",
                    query
                );
                throw;
            }

            return documents;
        }

        #endregion

        #region Vector Search

        /// <summary>
        /// Vector-based semantic search (Manual cosine similarity)
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
                    topK,
                    minSimilarity
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_KnowledgeBase_VectorSearch", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Query vector parametresi (kullanılmayacak ama SP signature için)
                        cmd.Parameters.Add(new SqlParameter("@QueryVector", SqlDbType.VarBinary)
                        {
                            Value = SerializeVector(queryVector)
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

                                // ✅ Embedding'i oku
                                var embeddingBytes = reader.IsDBNull(reader.GetOrdinal("Embedding"))
                                    ? null
                                    : (byte[])reader["Embedding"];

                                if (embeddingBytes != null)
                                {
                                    var docEmbedding = DeserializeVector(embeddingBytes);

                                    // ✅ C# tarafında cosine similarity hesapla
                                    var similarity = CalculateCosineSimilarity(queryVector, docEmbedding);

                                    // Minimum similarity filtresi
                                    if (similarity >= minSimilarity)
                                    {
                                        results.Add((doc, similarity));
                                    }
                                }
                            }
                        }
                    }
                }

                // ✅ Similarity'ye göre sırala ve TopK al
                results = results
                    .OrderByDescending(r => r.Item2)
                    .Take(topK)
                    .ToList();

                _logger.LogInformation(
                    "[VECTOR-SEARCH] {Count} belge bulundu",
                    results.Count
                );

                foreach (var (doc, similarity) in results)
                {
                    _logger.LogDebug(
                        "[VECTOR-SEARCH] • {Title} (Similarity: {Similarity:P2})",
                        doc.Title,
                        similarity
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[VECTOR-SEARCH-ERROR] TopK: {TopK}",
                    topK
                );
                throw;
            }

            return results;
        }

        /// <summary>
        /// İki vektör arasındaki cosine similarity hesaplar (0-1 arası)
        /// </summary>
        private float CalculateCosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
                throw new ArgumentException("Vektörler aynı boyutta olmalı");

            float dotProduct = 0;
            float magnitude1 = 0;
            float magnitude2 = 0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0;

            return dotProduct / (magnitude1 * magnitude2);
        }
        #endregion

        #region Document CRUD

        /// <summary>
        /// Tüm aktif belgeleri getirir
        /// </summary>
        public async Task<List<Document>> GetAllDocuments()
        {
            var documents = new List<Document>();

            try
            {
                _logger.LogInformation("[GET-ALL] Tüm belgeler istendi");

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT DocumentId, Title, Content, Category FROM dbo.KnowledgeBase WHERE IsActive = 1",
                        conn))
                    {
                        cmd.CommandType = CommandType.Text;

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

                _logger.LogInformation("[GET-ALL] {Count} belge bulundu", documents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GET-ALL-ERROR]");
                throw;
            }

            return documents;
        }

        /// <summary>
        /// Belge ID'sine göre getirir
        /// </summary>
        public async Task<Document?> GetDocumentByIdAsync(int documentId)
        {
            try
            {
                _logger.LogInformation("[GET-BY-ID] DocumentId: {DocumentId}", documentId);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT DocumentId, Title, Content, Category FROM dbo.KnowledgeBase WHERE DocumentId = @Id AND IsActive = 1",
                        conn))
                    {
                        cmd.Parameters.Add(new SqlParameter("@Id", documentId));

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

        /// <summary>
        /// Belge ekler (embedding ile)
        /// </summary>
        public async Task<int> AddDocumentAsync(Document document, float[] embedding)
        {
            try
            {
                _logger.LogInformation(
                    "[ADD-DOCUMENT] Title: '{Title}', Category: {Category}",
                    document.Title,
                    document.Category
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(
                        @"INSERT INTO dbo.KnowledgeBase (Title, Content, Category, Tags, Embedding, IsActive, CreatedDate)
                          VALUES (@Title, @Content, @Category, @Tags, @Embedding, 1, GETDATE());
                          SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        conn))
                    {
                        cmd.Parameters.Add(new SqlParameter("@Title", document.Title));
                        cmd.Parameters.Add(new SqlParameter("@Content", document.Content));
                        cmd.Parameters.Add(new SqlParameter("@Category", (object?)document.Category ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Tags", DBNull.Value)); // Tags eklenebilir
                        cmd.Parameters.Add(new SqlParameter("@Embedding", SqlDbType.VarBinary)
                        {
                            Value = SerializeVector(embedding)
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

        /// <summary>
        /// Belge günceller
        /// </summary>
        public async Task UpdateDocumentAsync(Document document)
        {
            try
            {
                _logger.LogInformation(
                    "[UPDATE-DOCUMENT] DocumentId: {DocumentId}",
                    document.Id
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(
                        @"UPDATE dbo.KnowledgeBase
                          SET Title = @Title,
                              Content = @Content,
                              Category = @Category,
                              UpdatedDate = GETDATE()
                          WHERE DocumentId = @Id",
                        conn))
                    {
                        cmd.Parameters.Add(new SqlParameter("@Id", document.Id));
                        cmd.Parameters.Add(new SqlParameter("@Title", document.Title));
                        cmd.Parameters.Add(new SqlParameter("@Content", document.Content));
                        cmd.Parameters.Add(new SqlParameter("@Category", (object?)document.Category ?? DBNull.Value));

                        await conn.OpenAsync();

                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation(
                            "[UPDATE-DOCUMENT] ✅ {RowsAffected} satır güncellendi",
                            rowsAffected
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPDATE-DOCUMENT-ERROR] DocumentId: {DocumentId}", document.Id);
                throw;
            }
        }

        /// <summary>
        /// Belgeye embedding ekler (migration için)
        /// </summary>
        public async Task UpdateDocumentEmbeddingAsync(int documentId, float[] embedding)
        {
            try
            {
                _logger.LogInformation(
                    "[UPDATE-EMBEDDING] DocumentId: {DocumentId}",
                    documentId
                );

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE dbo.KnowledgeBase SET Embedding = @Embedding WHERE DocumentId = @Id",
                        conn))
                    {
                        cmd.Parameters.Add(new SqlParameter("@Id", documentId));
                        cmd.Parameters.Add(new SqlParameter("@Embedding", SqlDbType.VarBinary)
                        {
                            Value = SerializeVector(embedding)
                        });

                        await conn.OpenAsync();

                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation(
                            "[UPDATE-EMBEDDING] ✅ {RowsAffected} satır güncellendi",
                            rowsAffected
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPDATE-EMBEDDING-ERROR] DocumentId: {DocumentId}", documentId);
                throw;
            }
        }

        /// <summary>
        /// Belge siler (soft delete)
        /// </summary>
        public async Task DeleteDocumentAsync(int documentId)
        {
            try
            {
                _logger.LogInformation("[DELETE-DOCUMENT] DocumentId: {DocumentId}", documentId);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE dbo.KnowledgeBase SET IsActive = 0 WHERE DocumentId = @Id",
                        conn))
                    {
                        cmd.Parameters.Add(new SqlParameter("@Id", documentId));

                        await conn.OpenAsync();

                        var rowsAffected = await cmd.ExecuteNonQueryAsync();

                        _logger.LogInformation(
                            "[DELETE-DOCUMENT] ✅ {RowsAffected} satır silindi",
                            rowsAffected
                        );
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

        /// <summary>
        /// SqlDataReader'dan Document nesnesini okur
        /// </summary>
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

        /// <summary>
        /// Float array'i byte array'e çevirir (SQL VECTOR için)
        /// </summary>
        private byte[] SerializeVector(float[] vector)
        {
            byte[] bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Byte array'i float array'e çevirir (SQL VECTOR'den okuma)
        /// </summary>
        private float[] DeserializeVector(byte[] bytes)
        {
            float[] vector = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
            return vector;
        }

        #endregion
    }
}
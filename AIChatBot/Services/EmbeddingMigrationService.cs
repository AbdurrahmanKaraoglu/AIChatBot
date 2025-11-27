using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Models;

namespace AIChatBot.Services
{
    /// <summary>
    /// KnowledgeBase belgelerine embedding ekleyen servis
    /// </summary>
    public class EmbeddingMigrationService
    {
        private readonly IKnowledgeBaseRepository _knowledgeBaseRepo;
        private readonly EmbeddingService _embeddingService;
        private readonly ILogger<EmbeddingMigrationService> _logger;

        public EmbeddingMigrationService(
            IKnowledgeBaseRepository knowledgeBaseRepo,
            EmbeddingService embeddingService,
            ILogger<EmbeddingMigrationService> logger)
        {
            _knowledgeBaseRepo = knowledgeBaseRepo;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        /// <summary>
        /// Embedding'i olmayan tüm belgeleri bulur
        /// </summary>
        public async Task<List<Document>> GetPendingDocumentsAsync()
        {
            try
            {
                _logger.LogInformation("[MIGRATION] Embedding'i olmayan belgeler aranıyor...");

                var allDocs = await _knowledgeBaseRepo.GetAllDocuments();
                var pendingDocs = allDocs.Where(d => !d.HasEmbedding).ToList();

                _logger.LogInformation(
                    "[MIGRATION] {TotalDocs} belge bulundu, {PendingCount} belgenin embedding'i yok",
                    allDocs.Count,
                    pendingDocs.Count
                );

                return pendingDocs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION-ERROR] Pending belgeler getirilemedi");
                throw;
            }
        }

        /// <summary>
        /// Tek bir belgeye embedding ekler
        /// </summary>
        public async Task<bool> MigrateSingleDocumentAsync(int documentId)
        {
            try
            {
                _logger.LogInformation("[MIGRATION] DocumentId:{DocumentId} için embedding oluşturuluyor...", documentId);

                // 1. Belgeyi getir
                var doc = await _knowledgeBaseRepo.GetDocumentById(documentId);
                if (doc == null)
                {
                    _logger.LogWarning("[MIGRATION] DocumentId:{DocumentId} bulunamadı", documentId);
                    return false;
                }

                // 2. Embedding oluştur
                var embedding = await _embeddingService.CreateEmbeddingAsync(doc.Content);

                if (embedding == null || embedding.Length != 768)
                {
                    _logger.LogError("[MIGRATION] DocumentId:{DocumentId} için embedding oluşturulamadı", documentId);
                    return false;
                }

                // 3. JSON string'e çevir (SQL Server VECTOR için)
                var vectorJson = "[" + string.Join(",", embedding.Select(v =>
                    v.ToString("G", System.Globalization.CultureInfo.InvariantCulture))) + "]";

                // 4. Veritabanına kaydet
                await _knowledgeBaseRepo.UpdateEmbedding(documentId, vectorJson);

                _logger.LogInformation(
                    "[MIGRATION] ✅ DocumentId:{DocumentId} '{Title}' - Embedding eklendi ({Length} boyut)",
                    documentId,
                    doc.Title,
                    embedding.Length
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION-ERROR] DocumentId:{DocumentId} hatası", documentId);
                return false;
            }
        }

        /// <summary>
        /// Tüm belgelere toplu embedding ekler
        /// </summary>
        public async Task<MigrationResult> MigrateAllAsync()
        {
            var result = new MigrationResult
            {
                StartTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("========================================");
                _logger.LogInformation("[MIGRATION] 🚀 Toplu embedding migration başlatılıyor...");
                _logger.LogInformation("========================================");

                // 1. Pending belgeleri getir
                var pendingDocs = await GetPendingDocumentsAsync();
                result.TotalDocuments = pendingDocs.Count;

                if (pendingDocs.Count == 0)
                {
                    _logger.LogInformation("[MIGRATION] ✅ Tüm belgelerin embedding'i mevcut");
                    result.EndTime = DateTime.UtcNow;
                    return result;
                }

                // 2. Her belge için embedding oluştur
                foreach (var doc in pendingDocs)
                {
                    var success = await MigrateSingleDocumentAsync(doc.Id);

                    if (success)
                        result.SuccessCount++;
                    else
                        result.FailureCount++;

                    // İlerleme logu
                    if ((result.SuccessCount + result.FailureCount) % 5 == 0)
                    {
                        _logger.LogInformation(
                            "[MIGRATION] İlerleme: {Current}/{Total} - Başarılı:{Success}, Hatalı:{Fail}",
                            result.SuccessCount + result.FailureCount,
                            result.TotalDocuments,
                            result.SuccessCount,
                            result.FailureCount
                        );
                    }

                    // Rate limiting (Ollama'yı yormamak için)
                    await Task.Delay(500);  // 500ms bekleme
                }

                result.EndTime = DateTime.UtcNow;

                _logger.LogInformation("========================================");
                _logger.LogInformation("[MIGRATION] ✅ Migration tamamlandı!");
                _logger.LogInformation("  📊 Toplam: {Total}", result.TotalDocuments);
                _logger.LogInformation("  ✅ Başarılı: {Success}", result.SuccessCount);
                _logger.LogInformation("  ❌ Hatalı: {Fail}", result.FailureCount);
                _logger.LogInformation("  ⏱️ Süre: {Duration:0.00} saniye", result.Duration.TotalSeconds);
                _logger.LogInformation("========================================");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION] ❌ Kritik hata!");
                result.EndTime = DateTime.UtcNow;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Migration istatistiklerini getirir
        /// </summary>
        public async Task<MigrationStats> GetStatsAsync()
        {
            try
            {
                var allDocs = await _knowledgeBaseRepo.GetAllDocuments();

                var stats = new MigrationStats
                {
                    TotalDocuments = allDocs.Count,
                    WithEmbedding = allDocs.Count(d => d.HasEmbedding),
                    WithoutEmbedding = allDocs.Count(d => !d.HasEmbedding),
                    CategoryStats = allDocs
                        .GroupBy(d => d.Category ?? "Kategorisiz")
                        .Select(g => new CategoryStat
                        {
                            Category = g.Key,
                            Total = g.Count(),
                            WithEmbedding = g.Count(d => d.HasEmbedding),
                            WithoutEmbedding = g.Count(d => !d.HasEmbedding)
                        })
                        .ToList()
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION-STATS] Hata");
                throw;
            }
        }
    }

    // =============================================
    // DTO Models
    // =============================================

    public class MigrationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public int TotalDocuments { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSuccess => FailureCount == 0 && string.IsNullOrEmpty(ErrorMessage);
    }

    public class MigrationStats
    {
        public int TotalDocuments { get; set; }
        public int WithEmbedding { get; set; }
        public int WithoutEmbedding { get; set; }
        public double CompletionPercentage => TotalDocuments > 0
            ? (WithEmbedding * 100.0 / TotalDocuments) 
            : 0;
        public List<CategoryStat> CategoryStats { get; set; } = new();
    }

    public class CategoryStat
    {
        public string Category { get; set; } = "";
        public int Total { get; set; }
        public int WithEmbedding { get; set; }
        public int WithoutEmbedding { get; set; }
        public double Percentage => Total > 0
            ? (WithEmbedding * 100.0 / Total)
            : 0;
    }
}
using AIChatBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly EmbeddingMigrationService _migrationService;
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(
            EmbeddingMigrationService migrationService,
            ILogger<MigrationController> logger)
        {
            _migrationService = migrationService;
            _logger = logger;
        }

        /// <summary>
        /// Migration istatistiklerini getirir
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult> GetStats()
        {
            var stats = await _migrationService.GetStatsAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Embedding'i olmayan belgeleri listeler
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult> GetPendingDocuments()
        {
            var docs = await _migrationService.GetPendingDocumentsAsync();

            return Ok(new
            {
                count = docs.Count,
                documents = docs.Select(d => new
                {
                    documentId = d.Id,
                    title = d.Title,
                    category = d.Category,
                    contentLength = d.Content.Length
                })
            });
        }

        /// <summary>
        /// Tek bir belgeye embedding ekler
        /// </summary>
        [HttpPost("single/{documentId}")]
        public async Task<ActionResult> MigrateSingle(int documentId)
        {
            _logger.LogInformation("[API] Tek belge migration: DocumentId={DocumentId}", documentId);

            var success = await _migrationService.MigrateSingleDocumentAsync(documentId);

            if (success)
            {
                return Ok(new
                {
                    message = $"DocumentId {documentId} için embedding eklendi",
                    documentId,
                    success = true
                });
            }
            else
            {
                return BadRequest(new
                {
                    message = $"DocumentId {documentId} için embedding eklenemedi",
                    documentId,
                    success = false
                });
            }
        }

        /// <summary>
        /// Tüm belgelere toplu embedding ekler
        /// </summary>
        [HttpPost("all")]
        public async Task<ActionResult> MigrateAll()
        {
            _logger.LogInformation("[API] ========================================");
            _logger.LogInformation("[API] TOPLU EMBEDDING MIGRATION BAŞLATILDI");
            _logger.LogInformation("[API] ========================================");

            var result = await _migrationService.MigrateAllAsync();

            return Ok(new
            {
                success = result.IsSuccess,
                totalDocuments = result.TotalDocuments,
                successCount = result.SuccessCount,
                failureCount = result.FailureCount,
                durationSeconds = result.Duration.TotalSeconds,
                startTime = result.StartTime,
                endTime = result.EndTime,
                errorMessage = result.ErrorMessage
            });
        }
    }
}
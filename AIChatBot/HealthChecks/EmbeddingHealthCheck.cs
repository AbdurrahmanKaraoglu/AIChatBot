using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIChatBot.HealthChecks
{
    /// <summary>
    /// Ollama embedding modelinin sağlık kontrolü
    /// </summary>
    public class EmbeddingHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmbeddingHealthCheck> _logger;

        public EmbeddingHealthCheck(
            IConfiguration configuration,
            ILogger<EmbeddingHealthCheck> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var ollamaEndpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
                var embedModel = _configuration["Ollama:EmbedModel"] ?? "nomic-embed-text";

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

                var response = await httpClient.GetAsync($"{ollamaEndpoint}/api/tags", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Unhealthy("Ollama API erişilemez");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var modelExists = content.Contains(embedModel);

                if (!modelExists)
                {
                    return HealthCheckResult.Degraded(
                        $"Embedding modeli '{embedModel}' bulunamadı.  Vector search çalışmayacak.",
                        data: new Dictionary<string, object>
                        {
                            { "embedModel", embedModel },
                            { "modelExists", false }
                        }
                    );
                }

                return HealthCheckResult.Healthy(
                    "Embedding modeli hazır",
                    data: new Dictionary<string, object>
                    {
                        { "embedModel", embedModel }
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HEALTH] Embedding check hatası");
                return HealthCheckResult.Unhealthy($"Hata: {ex.Message}");
            }
        }
    }
}
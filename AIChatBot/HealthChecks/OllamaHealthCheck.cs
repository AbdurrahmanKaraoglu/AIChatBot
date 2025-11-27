using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIChatBot.HealthChecks
{
    /// <summary>
    /// Ollama servisinin sağlık kontrolü
    /// </summary>
    public class OllamaHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OllamaHealthCheck> _logger;

        public OllamaHealthCheck(
            IConfiguration configuration,
            ILogger<OllamaHealthCheck> logger)
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
                var model = _configuration["Ollama:Model"] ?? "llama3. 1";

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

                // 1. API erişilebilir mi?
                var response = await httpClient.GetAsync($"{ollamaEndpoint}/api/tags", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Ollama API yanıt vermiyor (HTTP {response.StatusCode})",
                        data: new Dictionary<string, object>
                        {
                            { "endpoint", ollamaEndpoint },
                            { "statusCode", response.StatusCode }
                        }
                    );
                }

                // 2. Model yüklü mü?
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var modelExists = content.Contains(model);

                if (!modelExists)
                {
                    return HealthCheckResult.Degraded(
                        $"Ollama erişilebilir ama model '{model}' bulunamadı",
                        data: new Dictionary<string, object>
                        {
                            { "endpoint", ollamaEndpoint },
                            { "model", model },
                            { "modelExists", false }
                        }
                    );
                }

                return HealthCheckResult.Healthy(
                    "Ollama servisi çalışıyor ve model hazır",
                    data: new Dictionary<string, object>
                    {
                        { "endpoint", ollamaEndpoint },
                        { "model", model },
                        { "responseTime", response.Headers.Date }
                    }
                );
            }
            catch (TaskCanceledException)
            {
                return HealthCheckResult.Unhealthy("Ollama timeout (5 saniye)");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[HEALTH] Ollama erişim hatası");
                return HealthCheckResult.Unhealthy($"Ollama erişilemez: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HEALTH] Beklenmeyen hata");
                return HealthCheckResult.Unhealthy($"Hata: {ex.Message}");
            }
        }
    }
}
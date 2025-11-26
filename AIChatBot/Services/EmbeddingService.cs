using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AIChatBot.Services
{
    /// <summary>
    /// Ollama embedding servisi - Text'i 768-boyutlu vektöre çevirir
    /// </summary>
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaEndpoint;
        private readonly string _embedModel;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(
            IConfiguration configuration,
            ILogger<EmbeddingService> logger)
        {
            _ollamaEndpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            _embedModel = configuration["Ollama:EmbedModel"] ?? "nomic-embed-text";
            _logger = logger;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_ollamaEndpoint),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _logger.LogInformation(
                "[EMBEDDING-INIT] Endpoint: {Endpoint}, Model: {Model}",
                _ollamaEndpoint,
                _embedModel
            );
        }

        /// <summary>
        /// Metni 768-boyutlu vektöre çevirir
        /// </summary>
        /// <param name="text">Dönüştürülecek metin</param>
        /// <returns>768-boyutlu float array (embedding vektörü)</returns>
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("[EMBEDDING] Boş text gönderildi");
                throw new ArgumentException("Text boş olamaz", nameof(text));
            }

            try
            {
                _logger.LogDebug(
                    "[EMBEDDING] Embedding oluşturuluyor: {TextLength} karakter",
                    text.Length
                );

                var request = new OllamaEmbeddingRequest
                {
                    Model = _embedModel,
                    Prompt = text
                };

                var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "[EMBEDDING-ERROR] HTTP {StatusCode}: {Error}",
                        response.StatusCode,
                        errorContent
                    );
                    throw new HttpRequestException($"Ollama embedding hatası: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();

                if (result?.Embedding == null || result.Embedding.Length == 0)
                {
                    _logger.LogError("[EMBEDDING-ERROR] Boş embedding döndü");
                    throw new InvalidOperationException("Embedding oluşturulamadı");
                }

                // Boyut kontrolü (nomic-embed-text = 768 dimension)
                if (result.Embedding.Length != 768)
                {
                    _logger.LogWarning(
                        "[EMBEDDING] Beklenmeyen boyut: {ActualDim} (Beklenen: 768)",
                        result.Embedding.Length
                    );
                }

                _logger.LogInformation(
                    "[EMBEDDING] ✅ Başarılı: {Dimension} boyut, Text: '{TextPreview}.. .'",
                    result.Embedding.Length,
                    text.Substring(0, Math.Min(50, text.Length))
                );

                return result.Embedding;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "[EMBEDDING-TIMEOUT] Timeout aşıldı (30s)");
                throw new TimeoutException("Embedding oluşturma zaman aşımına uğradı", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[EMBEDDING-HTTP-ERROR] HTTP isteği başarısız");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EMBEDDING-ERROR] Beklenmeyen hata: {Message}",
                    ex.Message
                );
                throw;
            }
        }

        /// <summary>
        /// Birden fazla text için batch embedding (paralel)
        /// </summary>
        /// <param name="texts">Text listesi</param>
        /// <param name="maxParallelism">Maksimum paralel istek sayısı (varsayılan: 3)</param>
        /// <returns>Embedding vektörleri listesi</returns>
        public async Task<List<float[]>> GetBatchEmbeddingsAsync(
            List<string> texts,
            int maxParallelism = 3)
        {
            if (texts == null || !texts.Any())
            {
                _logger.LogWarning("[EMBEDDING-BATCH] Boş text listesi");
                return new List<float[]>();
            }

            _logger.LogInformation(
                "[EMBEDDING-BATCH] {Count} text için embedding oluşturuluyor (Parallelism: {Parallelism})",
                texts.Count,
                maxParallelism
            );

            var embeddings = new List<float[]>();
            var semaphore = new SemaphoreSlim(maxParallelism);

            var tasks = texts.Select(async (text, index) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var embedding = await GetEmbeddingAsync(text);

                    _logger.LogDebug(
                        "[EMBEDDING-BATCH] {Index}/{Total} tamamlandı",
                        index + 1,
                        texts.Count
                    );

                    return embedding;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            embeddings = (await Task.WhenAll(tasks)).ToList();

            _logger.LogInformation(
                "[EMBEDDING-BATCH] ✅ {Count} embedding oluşturuldu",
                embeddings.Count
            );

            return embeddings;
        }

        /// <summary>
        /// Ollama embedding servisinin sağlık kontrolü
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                _logger.LogDebug("[EMBEDDING-HEALTH] Health check başlatılıyor.. .");

                var response = await _httpClient.GetAsync("/api/tags");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[EMBEDDING-HEALTH] ✅ Ollama erişilebilir");
                    return true;
                }

                _logger.LogWarning(
                    "[EMBEDDING-HEALTH] ❌ Ollama yanıt vermiyor: {StatusCode}",
                    response.StatusCode
                );
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EMBEDDING-HEALTH] ❌ Ollama erişilemez: {Message}",
                    ex.Message
                );
                return false;
            }
        }

        /// <summary>
        /// İki vektör arasındaki cosine similarity hesaplar (0-1 arası)
        /// </summary>
        public float CalculateCosineSimilarity(float[] vector1, float[] vector2)
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
    }

    #region DTOs

    /// <summary>
    /// Ollama embedding request modeli
    /// </summary>
    internal class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "nomic-embed-text";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    /// <summary>
    /// Ollama embedding response modeli
    /// </summary>
    internal class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    #endregion
}
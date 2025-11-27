using AIChatBot.Models;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIChatBot.Services
{
    public class OllamaChatClient : IChatClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelId;
        private readonly OllamaSettings _settings;

        public ChatClientMetadata Metadata { get; }

        public OllamaChatClient(string endpoint, string modelId)
            : this(endpoint, modelId, new OllamaSettings())
        {
        }

        public OllamaChatClient(string endpoint, string modelId, OllamaSettings settings)
        {
            _modelId = modelId;
            _settings = settings ?? new OllamaSettings();

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint),
                Timeout = TimeSpan.FromSeconds(_settings.Timeout)
            };

            Metadata = new ChatClientMetadata("Ollama", new Uri(endpoint), modelId);
        }

        public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(chatMessages, options, stream: false);

            var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);

            var assistantMessage = new ChatMessage(ChatRole.Assistant, ollamaResponse?.Message?.Content ?? "");

            return new Microsoft.Extensions.AI.ChatResponse(assistantMessage);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(chatMessages, options, stream: true);
            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });

            // ✅ Debug logging
            Console.WriteLine("========================================");
            Console.WriteLine("[OLLAMA-REQUEST] /api/chat");
            Console.WriteLine(jsonContent);
            Console.WriteLine("========================================");

            var requestContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = requestContent };

            // ✅ DÜZELTME: try-catch dışında response al
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                Console.WriteLine($"[OLLAMA-RESPONSE] Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"[OLLAMA-ERROR-BODY] {errorBody}");
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[OLLAMA-HTTP-ERROR] {ex.Message}");
                throw;
            }

            // ✅ yield return try-catch dışında
            using (response)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    OllamaResponse? chunk = null;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<OllamaResponse>(line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OLLAMA-PARSE-ERROR] {ex.Message}");
                        Console.WriteLine($"[OLLAMA-RAW-LINE] {line}");
                        continue;
                    }

                    if (chunk?.Message?.Content != null)
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.Message.Content);
                    }
                }
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(IChatClient) ? this : null;

        public void Dispose() => _httpClient.Dispose();

        // ✅ Ollama'nın beklediği format
        private OllamaRequest BuildRequest(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options,
            bool stream)
        {
            var request = new OllamaRequest
            {
                Model = _modelId,
                Stream = stream,
                Messages = chatMessages.Select(m => new OllamaMessage
                {
                    Role = m.Role.Value?.ToLower() ?? "user",
                    Content = m.Text ?? ""
                }).ToList()
            };

            // ✅ Options sadece gerekli alanlarla
            if (options != null)
            {
                request.Options = new OllamaOptions
                {
                    Temperature = (float)(options.Temperature ?? _settings.Temperature),
                    TopP = (float)(options.TopP ?? _settings.TopP)
                };
            }

            return request;
        }

        // --- INTERNAL DTOs ---
        private class OllamaRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("messages")]
            public List<OllamaMessage> Messages { get; set; } = new();

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }

            [JsonPropertyName("options")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public OllamaOptions? Options { get; set; }
        }

        private class OllamaOptions
        {
            [JsonPropertyName("temperature")]
            public float Temperature { get; set; }

            [JsonPropertyName("top_p")]
            public float TopP { get; set; }
        }

        private class OllamaMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }

        private class OllamaResponse
        {
            [JsonPropertyName("message")]
            public OllamaMessage? Message { get; set; }

            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }
    }
}
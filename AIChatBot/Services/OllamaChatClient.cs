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

        public ChatClientMetadata Metadata { get; }

        public OllamaChatClient(string endpoint, string modelId)
        {
            _modelId = modelId;
            _httpClient = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromMinutes(5) };
            Metadata = new ChatClientMetadata("Ollama", new Uri(endpoint), modelId);
        }

        public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(chatMessages, stream: false);

            // PostAsJsonAsync kullanımı
            var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);

            // ChatResponse oluşturulurken ChatMessage nesnesi verilir
            return new Microsoft.Extensions.AI.ChatResponse(new ChatMessage(ChatRole.Assistant, ollamaResponse?.Message?.Content ?? ""));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = BuildRequest(chatMessages, stream: true);
            var jsonContent = JsonSerializer.Serialize(request);
            var requestContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = requestContent };
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                OllamaResponse? chunk = null;
                try { chunk = JsonSerializer.Deserialize<OllamaResponse>(line); } catch { }

                if (chunk?.Message?.Content != null)
                {
                    // HATA DÜZELTİLDİ: Property initializer yerine Constructor kullanıldı
                    yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.Message.Content);
                }
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(IChatClient) ? this : null;

        public void Dispose() => _httpClient.Dispose();

        private OllamaRequest BuildRequest(IEnumerable<ChatMessage> chatMessages, bool stream)
        {
            return new OllamaRequest
            {
                Model = _modelId,
                Stream = stream,
                Messages = chatMessages.Select(m => new OllamaMessage
                {
                    Role = m.Role.Value?.ToLower() ?? "user",
                    Content = m.Text ?? ""
                }).ToList()
            };
        }

        // --- INTERNAL DTOs ---
        private class OllamaRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = "";
            [JsonPropertyName("messages")] public List<OllamaMessage> Messages { get; set; } = new();
            [JsonPropertyName("stream")] public bool Stream { get; set; }
        }

        private class OllamaMessage
        {
            [JsonPropertyName("role")] public string Role { get; set; } = "";
            [JsonPropertyName("content")] public string Content { get; set; } = "";
        }

        private class OllamaResponse
        {
            [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
            [JsonPropertyName("done")] public bool Done { get; set; }
        }
    }
}
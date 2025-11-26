using System.Text.Json.Serialization;

namespace AIChatBot.Models
{
    /// <summary>
    /// AI'nın cevabı
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// Oturum ID'si
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// AI'nın cevabı
        /// </summary>
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// Hangi tool'lar kullanıldı?
        /// Örnek: ["GetCustomerInfo", "GetOrderHistory"]
        /// </summary>
        [JsonPropertyName("usedTools")]
        public List<string> UsedTools { get; set; } = new();

        /// <summary>
        /// İşlemin başarılı olup olmadığı
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Hata varsa hata mesajı
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; }
    }
}
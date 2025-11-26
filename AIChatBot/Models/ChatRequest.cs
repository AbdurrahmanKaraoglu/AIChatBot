using System.Text.Json.Serialization;

namespace AIChatBot.Models
{
    /// <summary>
    /// Kullanıcının sohbet isteği
    /// </summary>
    public class ChatRequest
    {
        /// <summary>
        /// Her konuşma oturumunun benzersiz ID'si
        /// Örnek: "user123-session-001"
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Kullanıcının ID'si (Opsiyonel)
        /// Örnek: "user123"
        /// </summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; } // ✅ Nullable yapıldı

        /// <summary>
        /// Kullanıcının sorusu/mesajı
        /// Örnek: "Merhaba, bana yardım edebilir misin?"
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
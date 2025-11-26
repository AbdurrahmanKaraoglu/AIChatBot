namespace AIChatBot.Models
{
    /// <summary>
    /// Ollama yapılandırma ayarları
    /// </summary>
    public class OllamaSettings
    {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "llama2";
        public double Temperature { get; set; } = 0.3;
        public double TopP { get; set; } = 0.9;
        public double RepeatPenalty { get; set; } = 1.1;
        public int Timeout { get; set; } = 300;
        public int RetryCount { get; set; } = 3;
    }
}
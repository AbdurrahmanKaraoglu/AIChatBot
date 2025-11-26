namespace AIChatBot.Models
{
    /// <summary>
    /// RAG için belge modeli
    /// </summary>
    public class Document
    {
        /// <summary>
        /// Belge ID'si
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Belge başlığı
        /// Örnek: "Kargo Politikası"
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Belge içeriği
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Belgenin kategorisi
        /// Örnek: "FAQ", "PolicyDocument", "ProductInfo"
        /// </summary>
        public string Category { get; set; } = string.Empty;
    }    
}
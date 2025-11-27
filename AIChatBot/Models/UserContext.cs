// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Models\UserContext.cs
namespace AIChatBot.Models
{
    /// <summary>
    /// Kullanıcının yetki bilgileri
    /// RBAC (Role-Based Access Control) için
    /// </summary>
    public class UserContext
    {
        /// <summary>
        /// Kullanıcı ID'si
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Kullanıcının rolü
        /// Örnek: "Admin", "Customer", "User"
        /// </summary>
        public string Role { get; set; } = "User";

        /// <summary>
        /// Kullanıcının adı
        /// </summary>
        public string UserName { get; set; } = "Guest";
    }
}
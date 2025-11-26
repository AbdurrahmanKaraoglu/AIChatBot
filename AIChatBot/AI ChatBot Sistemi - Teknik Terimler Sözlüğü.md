# 📚 AI ChatBot Sistemi - Teknik Terimler Sözlüğü

## 📑 İçindekiler

1. [Yazılım Mimarisi Terimleri](#1-yazılım-mimarisi-terimleri)
2. [AI & Machine Learning Terimleri](#2-ai--machine-learning-terimleri)
3. [Veritabanı Terimleri](#3-veritabanı-terimleri)
4. [Backend & API Terimleri](#4-backend--api-terimleri)
5. [C# & .NET Terimleri](#5-c--net-terimleri)
6. [HTTP & Web Terimleri](#6-http--web-terimleri)
7. [Genel Yazılım Terimleri](#7-genel-yazılım-terimleri)

---

## 1. Yazılım Mimarisi Terimleri

### 🏗️ Layered Architecture (Katmanlı Mimari)

**Ne:** Uygulamanın farklı sorumlulukları olan katmanlara ayrılması. 

**Neden Kullanılır:**
- Kodun düzenli ve okunabilir olması
- Her katmanın bağımsız test edilebilmesi
- Değişiklik yaparken sadece bir katmanı etkileme

**Örnek:**
```
┌─────────────────────┐
│  Presentation Layer │  ← Controller (API endpoint'leri)
├─────────────────────┤
│   Business Layer    │  ← Service (ChatService, RagService)
├─────────────────────┤
│   Data Access Layer │  ← Repository (Database işlemleri)
├─────────────────────┤
│     Database        │  ← SQL Server
└─────────────────────┘
```

---

### 🔗 Dependency Injection (DI)

**Ne:** Bir sınıfın ihtiyaç duyduğu bağımlılıkların dışarıdan verilmesi.

**Neden Kullanılır:**
- Loose coupling (Gevşek bağlılık)
- Test edilebilirlik (Mock nesneler inject edilebilir)
- Kod yeniden kullanılabilirliği

**Örnek:**
```csharp
// ❌ Kötü (Tight Coupling)
public class ChatService
{
    private ChatMemoryRepository _repo = new ChatMemoryRepository(); // Hard-coded
}

// ✅ İyi (Dependency Injection)
public class ChatService
{
    private readonly IChatMemoryRepository _repo;
    
    public ChatService(IChatMemoryRepository repo) // Constructor Injection
    {
        _repo = repo;
    }
}

// Program.cs'de kayıt
builder.Services.AddScoped<IChatMemoryRepository, ChatMemoryRepository>();
```

**Avantaj:** Test sırasında gerçek DB yerine fake repository kullanılabilir.

---

### 📦 Repository Pattern

**Ne:** Veritabanı işlemlerini soyutlayan bir tasarım deseni.

**Neden Kullanılır:**
- Business Logic ile Data Access Layer'ı ayırır
- Veritabanı değişikliklerinde sadece Repository güncellenir
- Kodun test edilmesini kolaylaştırır

**Örnek:**
```csharp
// Interface (Contract)
public interface IChatMemoryRepository
{
    Task SaveMessageAsync(string sessionId, string role, string content);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId);
}

// Implementation (SQL Server)
public class ChatMemoryRepository : IChatMemoryRepository
{
    public async Task SaveMessageAsync(...)
    {
        // SQL Server ile kaydet
    }
}

// Implementation (MongoDB - Alternatif)
public class MongoDbChatMemoryRepository : IChatMemoryRepository
{
    public async Task SaveMessageAsync(...)
    {
        // MongoDB ile kaydet
    }
}

// Service sadece interface'i bilir
public class ChatService
{
    private readonly IChatMemoryRepository _repo;
    
    public ChatService(IChatMemoryRepository repo)
    {
        _repo = repo; // SQL ya da Mongo olabilir, Service umursamaz
    }
}
```

---

### 🎨 DTO (Data Transfer Object)

**Ne:** Katmanlar arasında veri taşıyan basit sınıflar.

**Neden Kullanılır:**
- İç domain modellerini dış dünyadan gizler
- Sadece gerekli alanları taşır (güvenlik)
- Veri doğrulama (validation) eklenebilir

**Örnek:**
```csharp
// Database Entity (İç model)
public class ChatSessionEntity
{
    public string SessionId { get; set; }
    public string UserId { get; set; }
    public DateTime StartDate { get; set; }
    public byte[] PasswordHash { get; set; } // Hassas veri
    // ...  20 alan daha
}

// DTO (API'ye dönülen model)
public class ChatSessionDto
{
    public string SessionId { get; set; }
    public string UserName { get; set; } // PasswordHash yok! 
}
```

**Projemizdeki DTO'lar:**
- `ChatRequest`
- `ChatResponse`
- `SmartSearchRequest`
- `Document`

---

### 🔌 Interface (Arayüz)

**Ne:** Sınıfların uyması gereken bir sözleşme (contract).

**Neden Kullanılır:**
- Polimorfizm (Farklı implementasyonlar aynı interface'i kullanabilir)
- Dependency Injection için gerekli
- Test sırasında mock nesneler oluşturma

**Örnek:**
```csharp
// Interface
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}

// Implementation 1: SendGrid
public class SendGridEmailService : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // SendGrid API kullan
    }
}

// Implementation 2: SMTP
public class SmtpEmailService : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // SMTP sunucusu kullan
    }
}

// Kullanım (Interface'e bağımlı, implementasyona değil)
public class OrderService
{
    private readonly IEmailService _emailService;
    
    public OrderService(IEmailService emailService)
    {
        _emailService = emailService; // SendGrid ya da SMTP olabilir
    }
}
```

---

## 2. AI & Machine Learning Terimleri

### 🤖 LLM (Large Language Model)

**Ne:** Milyarlarca parametre ile eğitilmiş büyük dil modeli.

**Nasıl Çalışır:**
1.  Girdi metni alır (prompt)
2. Bir sonraki kelimeyi tahmin eder
3.  Bu işlemi tekrarlayarak cümle oluşturur

**Örnekler:**
- GPT-4 (OpenAI) - 1. 76 trilyon parametre
- Gemma 2 (Google) - 2 milyar parametre
- Llama 3 (Meta) - 70 milyar parametre

**Projemizde:** `gemma2:2b` (2 milyar parametre)

---

### 📚 RAG (Retrieval-Augmented Generation)

**Ne:** LLM'e dışarıdan bilgi sağlayarak hallüsinasyonu azaltma tekniği.

**Nasıl Çalışır:**
```
1. User Question: "Kargo ücreti ne kadar?"
                    ↓
2.  Keyword Extraction: ["kargo", "ücreti"]
                    ↓
3. Database Search: → KnowledgeBase
   Result: "Kargo ücreti 100 TL üzeri ücretsiz"
                    ↓
4. Build Prompt:
   System: "Sen müşteri destek asistanısın."
   Context: "BİLGİ BANKASI: Kargo ücreti 100 TL üzeri ücretsiz"
   User: "Kargo ücreti ne kadar?"
                    ↓
5. LLM Response: "100 TL ve üzeri siparişlerde kargo ücretsizdir."
```

**Avantajlar:**
- ✅ Hallüsinasyon azalır (LLM bilmediği şeyi uydurmaz)
- ✅ Güncel bilgi (DB güncellenince AI'da güncellenir)
- ✅ Domain-specific cevaplar

---

### 🎲 Temperature (Sıcaklık)

**Ne:** LLM'nin yaratıcılık seviyesini kontrol eden parametre (0-2 arası).

**Değerler:**
- **0.0-0.3:** Deterministik, tutarlı, güvenilir (Müşteri desteği için ideal)
- **0.5-0.7:** Dengeli, yaratıcı ama tutarlı
- **0.8-2.0:** Çok yaratıcı, rastgele (Hikaye yazma için)

**Örnek:**
```csharp
// Projemizde
Temperature = 0.3  // Müşteri desteği için düşük değer
```

**Aynı prompt ile farklı temperature'ler:**

**Prompt:** "Kargo ücreti ne kadar?"

**Temperature=0.1:**
```
"100 TL ve üzeri siparişlerde kargo ücretsizdir."
```

**Temperature=0.9:**
```
"Harika bir soru! 🎉 100 TL'nin üzerine çıkan siparişlerinizde 
kargo bedavamıza gelir dostum!  🚚✨"
```

---

### 🔝 Top-P (Nucleus Sampling)

**Ne:** LLM'nin kelime seçerken olasılık dağılımını kesen parametre (0-1). 

**Nasıl Çalışır:**
```
Bir sonraki kelime için olasılıklar:
"ücretsiz" → %40
"bedava"   → %30
"parasız"  → %20
"free"     → %5
"muaf"     → %3
"meccani"  → %2

Top-P = 0.9 → İlk %90'ı al
Seçenekler: "ücretsiz", "bedava", "parasız" (Toplam %90)
"free", "muaf", "meccani" → Elenir
```

**Projemizde:**
```csharp
TopP = 0.9  // İlk %90'lık olasılıklardan seç
```

---

### 🚫 Repeat Penalty (Tekrar Cezası)

**Ne:** Aynı kelimenin tekrar edilmesini engelleyen parametre (1. 0-2.0).

**Örnek:**

**Repeat Penalty = 1.0 (YOK):**
```
"Ürün çok güzel, çok güzel, çok güzel bir ürün."
```

**Repeat Penalty = 1.5:**
```
"Ürün kaliteli, dayanıklı ve kullanışlı."
```

**Projemizde:**
```csharp
RepeatPenalty = 1.1  // Hafif tekrar önleme
```

---

### 💬 Chat Roles (Sohbet Rolleri)

**Ne:** LLM'de her mesajın kim tarafından söylendiğini belirten etiket.

**Roller:**

| Rol | Açıklama | Örnek |
|-----|----------|-------|
| **system** | AI'ya talimatlar verir | "Sen müşteri destek asistanısın" |
| **user** | Kullanıcının mesajı | "Ürün fiyatları nedir?" |
| **assistant** | AI'nın cevabı | "Ürün A: 500 TL" |

**Örnek Conversation:**
```csharp
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System, "Sen yardımcı bir asistansın."),
    new ChatMessage(ChatRole.User, "Merhaba"),
    new ChatMessage(ChatRole.Assistant, "Merhaba!  Nasıl yardımcı olabilirim?"),
    new ChatMessage(ChatRole.User, "Ürün fiyatları nedir?")
};
```

---

### 🌊 Streaming

**Ne:** LLM'nin cevabı kelime kelime gönderme yöntemi.

**Fark:**

**Non-Streaming:**
```
User: "Uzun bir makale yaz"
[...  30 saniye bekle ...]
AI: "İşte makaleniz: Lorem ipsum dolor sit amet...  (500 kelime)"
```

**Streaming:**
```
User: "Uzun bir makale yaz"
AI: "İşte"
AI: "makaleniz:"
AI: "Lorem"
AI: "ipsum"
...  (Kullanıcı hemen okumaya başlar)
```

**Kod:**
```csharp
// Non-Streaming
var response = await _chatClient.GetResponseAsync(messages);
Console.WriteLine(response. Text); // Tümü bir seferde

// Streaming
await foreach (var chunk in _chatClient.GetStreamingResponseAsync(messages))
{
    Console.Write(chunk.Text); // Kelime kelime
}
```

---

### 🎯 Prompt Engineering

**Ne:** LLM'den istenen çıktıyı almak için prompt (talimat) tasarlama sanatı.

**Kötü Prompt:**
```
"Ürün fiyatlarını söyle"
```
**Sonuç:** LLM uydurabilir, hallüsinasyon yapabilir.

**İyi Prompt (Projemizdeki):**
```
Sen bir müşteri destek asistanısın. 

KURALLAR:
1.  SADECE bilgi bankasındaki bilgileri kullan
2.  Bilmediğin şeyi ASLA uydurma
3. Türkçe konuş

BİLGİ BANKASI:
• Ürün A: 500 TL
• Ürün B: 1500 TL

SADECE yukarıdaki bilgileri kullan! 
```
**Sonuç:** LLM sadece verilen bilgilerle cevap verir.

---

### 🧠 Hallucination (Hallüsinasyon)

**Ne:** LLM'nin bilmediği bir şeyi uydurması.

**Örnek:**

**Prompt:** "2025 yılında Türkiye'nin başkenti nedir?"

**Hallüsinasyon Cevabı:**
```
"2025 yılında Türkiye'nin başkenti İstanbul olarak değiştirildi."
```
*(Gerçek değil, uydurma! )*

**Doğru Cevap:**
```
"Türkiye'nin başkenti Ankara'dır."
```

**Nasıl Önlenir:**
- RAG kullan (bilgi bankasından çek)
- System prompt'a "Bilmiyorsan 'bilmiyorum' de" kuralı ekle
- Temperature'ü düşük tut

---

## 3.  Veritabanı Terimleri

### 🔑 Primary Key (Birincil Anahtar)

**Ne:** Tablodaki her satırı benzersiz şekilde tanımlayan kolon.

**Özellikler:**
- ✅ Her satır için UNIQUE (benzersiz)
- ✅ NULL olamaz
- ✅ Otomatik INDEX oluşturur

**Örnek:**
```sql
CREATE TABLE ChatSessions (
    SessionId NVARCHAR(100) PRIMARY KEY,  -- PK
    UserId NVARCHAR(100)
);

-- Bu çalışır
INSERT INTO ChatSessions VALUES ('session-001', 'user1');

-- ❌ Bu HATA verir (Duplicate PK)
INSERT INTO ChatSessions VALUES ('session-001', 'user2');
```

---

### 🔗 Foreign Key (Yabancı Anahtar)

**Ne:** Bir tablonun başka tabloya referans vermesi (ilişki).

**Neden Kullanılır:**
- Referential Integrity (Veri bütünlüğü)
- Orphan kayıtları önler

**Örnek:**
```sql
CREATE TABLE ChatMessages (
    MessageId BIGINT PRIMARY KEY,
    SessionId NVARCHAR(100),
    Content NVARCHAR(MAX),
    FOREIGN KEY (SessionId) REFERENCES ChatSessions(SessionId)
);

-- ✅ Bu çalışır (session-001 var)
INSERT INTO ChatMessages VALUES (1, 'session-001', 'Merhaba');

-- ❌ Bu HATA verir (session-999 yok)
INSERT INTO ChatMessages VALUES (2, 'session-999', 'Test');
```

**CASCADE DELETE:**
```sql
FOREIGN KEY (SessionId) REFERENCES ChatSessions(SessionId)
ON DELETE CASCADE;

-- ChatSessions'dan session-001 silinirse
-- ChatMessages'daki tüm session-001 mesajları da silinir
```

---

### 📇 Index (İndeks)

**Ne:** Veritabanında arama hızını artıran veri yapısı (kitap indeksi gibi).

**Örnek:**

**Index OLMADAN:**
```sql
SELECT * FROM KnowledgeBase WHERE Price = 500;
-- Tüm 10,000 satırı tek tek tarar (SLOW)
```

**Index VARSA:**
```sql
CREATE INDEX IX_Price ON KnowledgeBase(Price);

SELECT * FROM KnowledgeBase WHERE Price = 500;
-- Direkt ilgili satırlara gider (FAST)
```

**Index Tipleri:**
- **CLUSTERED:** Veriyi fiziksel olarak sıralar (1 tane olabilir, genelde PK)
- **NONCLUSTERED:** Ayrı bir yapı oluşturur (birden fazla olabilir)

---

### 🗄️ Stored Procedure

**Ne:** Veritabanında önceden derlenmiş SQL kodları.

**Avantajlar:**
- ✅ Performance (Önceden derlenmiş)
- ✅ Security (SQL Injection önler)
- ✅ Kod tekrarını azaltır

**Örnek:**
```sql
-- Stored Procedure
CREATE PROCEDURE sp_GetUserMessages
    @UserId NVARCHAR(100)
AS
BEGIN
    SELECT * FROM ChatMessages 
    WHERE SessionId IN (SELECT SessionId FROM ChatSessions WHERE UserId = @UserId);
END

-- Kullanım
EXEC sp_GetUserMessages @UserId = 'user1';
```

**C#'tan Çağırma:**
```csharp
using (SqlCommand cmd = new SqlCommand("sp_GetUserMessages", conn))
{
    cmd.CommandType = CommandType.StoredProcedure;
    cmd. Parameters.Add(new SqlParameter("@UserId", "user1"));
    
    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
    {
        // Verileri oku
    }
}
```

---

### 🔒 Transaction (İşlem)

**Ne:** Birden fazla SQL komutunu tek bir birim olarak çalıştırma (hepsi başarılı ya da hiçbiri).

**ACID Özellikleri:**
- **A**tomicity: Ya hepsi ya hiçbiri
- **C**onsistency: Veri tutarlı kalır
- **I**solation: Paralel işlemler birbirini etkilemez
- **D**urability: Commit sonrası veri kalıcıdır

**Örnek:**
```sql
BEGIN TRANSACTION;

-- 1. Mesajları sil
DELETE FROM ChatMessages WHERE SessionId = 'session-001';

-- 2. Session'ı sil
DELETE FROM ChatSessions WHERE SessionId = 'session-001';

-- Her ikisi de başarılıysa kaydet
COMMIT TRANSACTION;

-- Hata olursa geri al
-- ROLLBACK TRANSACTION;
```

**C#'ta Transaction:**
```csharp
using (SqlTransaction transaction = conn. BeginTransaction())
{
    try
    {
        // SQL komutları
        cmd1.Transaction = transaction;
        await cmd1.ExecuteNonQueryAsync();
        
        cmd2.Transaction = transaction;
        await cmd2.ExecuteNonQueryAsync();
        
        transaction. Commit(); // Başarılı
    }
    catch
    {
        transaction.Rollback(); // Hata, geri al
    }
}
```

---

### 📊 Normalization (Normalizasyon)

**Ne:** Veri tekrarını azaltmak için tabloları bölme. 

**1NF (First Normal Form):**
```sql
-- ❌ Kötü (Tekrar var)
CREATE TABLE Orders (
    OrderId INT,
    CustomerName NVARCHAR(100),
    CustomerEmail NVARCHAR(100),
    Products NVARCHAR(MAX)  -- "Ürün1, Ürün2, Ürün3" (CSV)
);

-- ✅ İyi (1NF)
CREATE TABLE Orders (
    OrderId INT,
    CustomerId INT
);

CREATE TABLE OrderItems (
    OrderId INT,
    ProductId INT
);
```

**2NF (Second Normal Form):**
```sql
-- ❌ Kötü (CustomerName her siparişte tekrar)
CREATE TABLE Orders (
    OrderId INT,
    CustomerId INT,
    CustomerName NVARCHAR(100),
    CustomerEmail NVARCHAR(100)
);

-- ✅ İyi (2NF - Müşteri bilgisi ayrı tablo)
CREATE TABLE Customers (
    CustomerId INT PRIMARY KEY,
    CustomerName NVARCHAR(100),
    CustomerEmail NVARCHAR(100)
);

CREATE TABLE Orders (
    OrderId INT PRIMARY KEY,
    CustomerId INT,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);
```

---

### 🎯 ADO.NET

**Ne:** . NET'in veritabanı erişim teknolojisi (Microsoft'un resmi kütüphanesi).

**Bileşenler:**
- `SqlConnection`: Veritabanı bağlantısı
- `SqlCommand`: SQL komutu çalıştırma
- `SqlDataReader`: Verileri okuma
- `SqlParameter`: Parametreli sorgu

**Örnek:**
```csharp
using (SqlConnection conn = new SqlConnection(connectionString))
{
    await conn.OpenAsync();
    
    using (SqlCommand cmd = new SqlCommand("SELECT * FROM Users WHERE UserId = @UserId", conn))
    {
        cmd.Parameters.Add(new SqlParameter("@UserId", "user1"));
        
        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                string userName = reader. GetString(reader.GetOrdinal("UserName"));
            }
        }
    }
}
```

**Alternatifler:**
- Entity Framework Core (ORM)
- Dapper (Micro-ORM)

---

## 4. Backend & API Terimleri

### 🌐 REST API (RESTful API)

**Ne:** HTTP protokolü kullanarak veri alışverişi yapan API standardı.

**REST Prensipleri:**
1. **Stateless:** Her istek bağımsızdır
2. **Client-Server:** Sunucu ve istemci ayrıdır
3. **Uniform Interface:** Standart HTTP metodları kullanılır

**HTTP Metodları:**

| Metod | Amaç | Örnek |
|-------|------|-------|
| **GET** | Veri okuma | `GET /api/Chat/history? sessionId=test-001` |
| **POST** | Veri oluşturma | `POST /api/Chat/message` |
| **PUT** | Veri güncelleme | `PUT /api/Products/123` |
| **DELETE** | Veri silme | `DELETE /api/Chat/clear? sessionId=test-001` |

---

### 📡 Endpoint

**Ne:** API'deki belirli bir fonksiyona erişilen URL.

**Örnek:**
```
Base URL: https://localhost:7090

Endpoints:
- POST   /api/Chat/message          → Mesaj gönder
- GET    /api/Chat/history           → Geçmişi getir
- DELETE /api/Chat/clear             → Session sil
- POST   /api/Chat/smart-search      → Akıllı arama
```

**C# Tanımı:**
```csharp
[HttpPost("message")]  // Endpoint: POST /api/Chat/message
public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
{
    // ... 
}
```

---

### 🎛️ Controller

**Ne:** HTTP isteklerini karşılayan sınıf (MVC pattern'in "C"si).

**Sorumlulukları:**
- İsteği al
- Validasyon yap
- Service'i çağır
- Yanıt dön

**Örnek:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    
    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }
    
    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.Message))
            return BadRequest(new { error = "Mesaj zorunlu" });
        
        var response = await _chatService.ProcessMessageAsync(request);
        return Ok(response);
    }
}
```

---

### ⚙️ Middleware

**Ne:** HTTP isteği ile yanıt arasında çalışan ara katman.

**Örnek Middleware'ler:**
- Authentication (Kimlik doğrulama)
- Logging (Loglama)
- Error Handling (Hata yönetimi)
- CORS (Cross-Origin istekleri)

**Pipeline:**
```
HTTP Request
    ↓
[ Authentication Middleware ]  ← Token kontrolü
    ↓
[ Logging Middleware ]         ← İstek logla
    ↓
[ Controller ]                 ← İşlem yap
    ↓
[ Error Handling Middleware ]  ← Hata varsa yakala
    ↓
HTTP Response
```

**Kod:**
```csharp
// Program.cs
app.UseAuthentication();  // Middleware 1
app.UseAuthorization();   // Middleware 2
app.MapControllers();      // Controller'lara route et
```

---

### 📝 Swagger / OpenAPI

**Ne:** API dokümantasyon ve test aracı.

**Özellikleri:**
- ✅ Tüm endpoint'leri listeler
- ✅ Parametre örnekleri gösterir
- ✅ Tarayıcıdan test edilebilir
- ✅ Otomatik client kod üretir

**Kullanım:**
```
https://localhost:7090/swagger

→ Tarayıcıda tüm API'yi görürsün
→ "Try it out" butonuyla test edebilirsin
```

---

### 🔄 Asynchronous Programming (Async/Await)

**Ne:** İşlemlerin sırayla değil, paralel çalışmasını sağlama.

**Neden Kullanılır:**
- Thread'i bloklamaz (Sunucu daha fazla istek karşılar)
- I/O işlemlerinde (DB, HTTP) verimliliği artırır

**Fark:**

**Synchronous (Blocking):**
```csharp
var data = GetDataFromDatabase();  // 5 saniye bekle (Thread bloke)
var result = ProcessData(data);    // İşle
return result;
```

**Asynchronous (Non-Blocking):**
```csharp
var data = await GetDataFromDatabaseAsync();  // 5 saniye beklerken Thread serbest
var result = ProcessData(data);
return result;
```

**Async Methodlar:**
```csharp
// Async metod tanımı
public async Task<string> GetMessageAsync(int id)
{
    var message = await _repository.GetMessageAsync(id);
    return message;
}

// Çağırma
var msg = await GetMessageAsync(123);
```

---

## 5. C# & .NET Terimleri

### 🏭 . NET (Dot NET)

**Ne:** Microsoft'un açık kaynaklı geliştirme platformu.

**Bileşenler:**
- **Runtime:** Uygulamaları çalıştırır (CLR - Common Language Runtime)
- **Kütüphaneler:** Hazır fonksiyonlar (BCL - Base Class Library)
- **SDK:** Geliştirme araçları

**Versiyonlar:**
- .NET Framework (Windows-only, eski)
- .NET Core (Cross-platform, modern)
- . NET 5+ (Birleştirilmiş, şu an . NET 10)

---

### 📦 NuGet Package

**Ne:** . NET için paket yöneticisi (npm gibi).

**Projemizdeki Paketler:**
```xml
<PackageReference Include="Microsoft.Extensions.AI" Version="10.0.1" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
<PackageReference Include="System.Data.SqlClient" Version="4.9.0" />
```

**Kurulum:**
```bash
dotnet add package Microsoft.Extensions.AI --version 10.0.1
```

---

### 🎯 LINQ (Language Integrated Query)

**Ne:** C#'ta koleksiyonlar üzerinde sorgulama dili (SQL benzeri).

**Örnek:**
```csharp
var products = new List<Product>
{
    new Product { Name = "Ürün A", Price = 500 },
    new Product { Name = "Ürün B", Price = 1500 },
    new Product { Name = "Ürün C", Price = 250 }
};

// LINQ ile filtreleme
var cheapProducts = products
    .Where(p => p. Price < 1000)  // Fiyatı 1000'den küçük
    .OrderBy(p => p.Price)       // Fiyata göre sırala
    .Select(p => p.Name)         // Sadece isim al
    .ToList();

// Sonuç: ["Ürün C", "Ürün A"]
```

**Projemizdeki LINQ:**
```csharp
var uniqueDocs = allDocuments
    .GroupBy(d => d.Id)
    .Select(g => g.First())
    .ToList();
```

---

### 🔢 Generic Types

**Ne:** Farklı veri tipleriyle çalışabilen sınıf/metod.

**Örnek:**
```csharp
// Generic List (T = herhangi bir tip)
List<string> names = new List<string> { "Ali", "Veli" };
List<int> numbers = new List<int> { 1, 2, 3 };

// Generic Metod
public T GetFirst<T>(List<T> list)
{
    return list.FirstOrDefault();
}

var firstName = GetFirst<string>(names);  // "Ali"
var firstNum = GetFirst<int>(numbers);    // 1
```

**Projemizdeki Generic:**
```csharp
Task<List<Document>> GetAllDocumentsAsync();
//   ^^^^^^^^^^^^^^
//   Generic return type
```

---

### 🎭 Nullable Types

**Ne:** Null değer alabilen tipler.

**C# 8.0+ (Nullable Reference Types):**
```csharp
// ❌ Nullable değil (null olamaz)
string name = "Ali";
name = null;  // HATA

// ✅ Nullable (null olabilir)
string? name = "Ali";
name = null;  // OK
```

**Projemizdeki Nullable:**
```csharp
public string? UserId { get; set; }  // Null olabilir
public decimal? MinPrice { get; set; }  // Null olabilir
```

---

### 📜 Extension Methods

**Ne:** Mevcut bir sınıfa metod eklemek (sınıfı değiştirmeden).

**Örnek:**
```csharp
// Extension Method
public static class StringExtensions
{
    public static bool IsValidEmail(this string email)
    {
        return email.Contains("@");
    }
}

// Kullanım
string email = "test@example.com";
bool valid = email.IsValidEmail();  // true
```

---

## 6. HTTP & Web Terimleri

### 📨 HTTP Status Codes

| Kod | Anlam | Ne Zaman Kullanılır |
|-----|-------|---------------------|
| **200 OK** | Başarılı | İstek sorunsuz tamamlandı |
| **201 Created** | Oluşturuldu | Yeni kayıt eklendi |
| **400 Bad Request** | Hatalı istek | Validation hatası |
| **401 Unauthorized** | Yetkisiz | Token eksik/geçersiz |
| **404 Not Found** | Bulunamadı | Kayıt yok |
| **500 Internal Server Error** | Sunucu hatası | Beklenmeyen hata |

**Projemizdeki Kullanım:**
```csharp
if (string.IsNullOrEmpty(request.Message))
    return BadRequest(new { error = "Mesaj zorunlu" });  // 400

return Ok(response);  // 200
```

---

### 🎫 JSON (JavaScript Object Notation)

**Ne:** Veri alışverişinde kullanılan hafif format.

**Örnek:**
```json
{
  "sessionId": "test-001",
  "userId": "user1",
  "message": "Merhaba"
}
```

**C# ile Serialization:**
```csharp
// Object → JSON
var request = new ChatRequest { SessionId = "test-001", Message = "Merhaba" };
string json = JsonSerializer. Serialize(request);

// JSON → Object
var request2 = JsonSerializer.Deserialize<ChatRequest>(json);
```

---

### 🔐 CORS (Cross-Origin Resource Sharing)

**Ne:** Farklı domain'lerden API'ye erişim izni. 

**Örnek:**
```
Frontend: http://localhost:3000 (React)
Backend:  http://localhost:7090 (API)

→ Tarayıcı normalde engellerdi
→ CORS ile izin verilir
```

**Kod:**
```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

app.UseCors("AllowAll");
```

---

## 7. Genel Yazılım Terimleri

### 🐛 Debugging

**Ne:** Kodda hata bulma ve düzeltme süreci.

**Araçlar:**
- Breakpoint (Kod satırını durdur)
- Watch (Değişken değerlerini izle)
- Call Stack (Metod çağrı sırasını gör)

---

### 📊 Logging

**Ne:** Uygulamanın çalışma sırasında bilgi kaydetmesi.

**Log Seviyeleri:**
```csharp
_logger.LogTrace("Detaylı debug bilgisi");
_logger.LogDebug("Debug bilgisi");
_logger. LogInformation("Bilgi");
_logger.LogWarning("Uyarı");
_logger.LogError("Hata");
_logger.LogCritical("Kritik hata");
```

---

### 🔄 CI/CD (Continuous Integration/Continuous Deployment)

**Ne:** Kod değişikliklerini otomatik test edip deploy etme.

**Pipeline:**
```
1. Kod Push (Git)
2. Otomatik Test (Unit Tests)
3. Build (Compile)
4. Deploy (Sunucuya yükle)
```

---

### 🧪 Unit Testing

**Ne:** Kodun küçük parçalarını test etme.

**Örnek:**
```csharp
[Fact]
public async Task ExtractKeywords_ShouldRemoveStopwords()
{
    var rag = new RagService(_repo, _logger);
    
    var keywords = rag.ExtractKeywords("ürün fiyatları nedir");
    
    Assert.Contains("ürün", keywords);
    Assert.Contains("fiyatları", keywords);
    Assert. DoesNotContain("nedir", keywords);  // Stopword
}
```

---

## 🎓 Sonuç

Bu terimler şunlar için önemlidir:

✅ **Mülakatlarda:** "DI nedir? ", "RAG nasıl çalışır?" gibi sorular  
✅ **Dokümantasyon okurken:** Teknik terimleri anlama  
✅ **Kod yazarken:** Doğru pattern'leri uygulama  
✅ **Takım çalışmasında:** Aynı dili konuşma  

**Daha fazla bilgi için:**
- Microsoft Learn: https://learn.microsoft.com/
- C# Documentation: https://docs.microsoft.com/dotnet/csharp/
- Ollama Docs: https://ollama.com/docs/

Başka bir terim açıklamak ister misiniz? 🚀
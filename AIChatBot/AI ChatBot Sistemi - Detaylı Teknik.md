# 📘 AI ChatBot Sistemi - Detaylı Teknik Dokümantasyon

## 📑 İçindekiler

1. [Proje Genel Bakış](#1-proje-genel-bakış)
2. [Mimari ve Teknolojiler](#2-mimari-ve-teknolojiler)
3. [Dosya Yapısı ve Açıklamalar](#3-dosya-yapısı-ve-açıklamalar)
4. [Modeller (Models)](#4-modeller-models)
5. [Repository Katmanı](#5-repository-katmanı)
6. [Servis Katmanı](#6-servis-katmanı)
7.  [Controller Katmanı](#7-controller-katmanı)
8. [Yapılandırma ve Başlatma](#8-yapılandırma-ve-başlatma)
9. [İstek-Yanıt Akışı](#9-istek-yanıt-akışı)
10. [Önemli Kavramlar](#10-önemli-kavramlar)

---

## 1. Proje Genel Bakış

### 🎯 Projenin Amacı
Bu proje, **Ollama** tabanlı yerel AI modeli ile çalışan, **RAG (Retrieval-Augmented Generation)** destekli, **SQL Server** veritabanı entegreli bir müşteri destek chatbot sistemidir.

### 🏗️ Temel Özellikler
- ✅ AI destekli sohbet (Ollama gemma2:2b)
- ✅ RAG sistemi (Veritabanından bilgi çekme)
- ✅ Session yönetimi (Konuşma geçmişi)
- ✅ Akıllı ürün arama (Fiyat + Kategori filtreli)
- ✅ Kampanya hesaplama
- ✅ Türkçe NLP (Stopwords, keyword extraction)

### 🛠️ Kullanılan Teknolojiler
- **Backend:** ASP.NET Core 10.0 (Web API)
- **AI Framework:** Microsoft.Extensions.AI 10.0. 1
- **LLM:** Ollama (gemma2:2b)
- **Veritabanı:** SQL Server (ADO.NET)
- **API Dokümantasyonu:** Swagger (Swashbuckle 7.2.0)

---

## 2. Mimari ve Teknolojiler

### 📐 Mimari Desen
```
┌─────────────────────────────────────────────────┐
│              Client (curl/Postman/UI)           │
└─────────────────┬───────────────────────────────┘
                  │ HTTP Request
                  ▼
┌─────────────────────────────────────────────────┐
│         Controllers (ChatController)            │
│  • SendMessage  • GetHistory  • SmartSearch     │
└─────────────────┬───────────────────────────────┘
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
┌──────────────────┐  ┌──────────────────┐
│  ChatService     │  │  RagService      │
│  • ProcessMessage│  │  • SearchDocs    │
│  • BuildPrompt   │  │  • ExtractKeywords│
└────────┬─────────┘  └────────┬─────────┘
         │                     │
         ▼                     ▼
┌─────────────────────────────────────────┐
│         Repository Layer                │
│  • ChatMemoryRepository (ADO.NET)       │
│  • KnowledgeBaseRepository (ADO.NET)    │
└────────┬────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│       SQL Server Database               │
│  • ChatSessions  • ChatMessages         │
│  • KnowledgeBase  • Products            │
└─────────────────────────────────────────┘
```

### 🧩 Katman Yapısı

| Katman | Sorumluluk | Dosyalar |
|--------|-----------|----------|
| **Controllers** | HTTP isteklerini karşılar, yanıt döner | ChatController. cs |
| **Services** | İş mantığı, AI entegrasyonu | ChatService.cs, RagService.cs, OllamaChatClient.cs |
| **Repository** | Veritabanı işlemleri | ChatMemoryRepository.cs, KnowledgeBaseRepository.cs |
| **Models** | Veri transfer objeleri (DTO) | ChatRequest.cs, ChatResponse.cs, Document.cs vb. |

---

## 3. Dosya Yapısı ve Açıklamalar

```
AIChatBot/
├── Controllers/
│   └── ChatController.cs           # API endpoint'leri
├── Models/
│   ├── ChatRequest.cs              # İstek modeli
│   ├── ChatResponse.cs             # Yanıt modeli
│   ├── Document.cs                 # RAG belge modeli
│   ├── OllamaSettings.cs           # Ollama konfigürasyonu
│   └── UserContext.cs              # Kullanıcı bilgisi
├── Repository/
│   ├── ChatMemoryRepository.cs     # Chat geçmişi DB işlemleri
│   ├── IChatMemoryRepository.cs    # Chat repository interface
│   ├── KnowledgeBaseRepository.cs  # Bilgi bankası DB işlemleri
│   └── IKnowledgeBaseRepository.cs # KB repository interface
├── Services/
│   ├── ChatService. cs              # Ana chat iş mantığı
│   ├── RagService.cs               # RAG arama mantığı
│   ├── OllamaChatClient.cs         # Ollama API client
│   ├── ConversationMemoryService.cs # Memory (kullanılmıyor)
│   └── MemoryService.cs            # Boş (kullanılmıyor)
├── Program.cs                      # Uygulama başlatma
└── appsettings.json                # Konfigürasyon
```

---

## 4.  Modeller (Models)

### 4.1 ChatRequest.cs

**Amaç:** Kullanıcının API'ye gönderdiği chat isteği. 

```csharp
public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;  // Oturum ID
    public string?  UserId { get; set; }                     // Kullanıcı ID (opsiyonel)
    public string Message { get; set; } = string.Empty;     // Kullanıcı mesajı
}
```

**Örnek JSON:**
```json
{
  "sessionId": "user123-session-001",
  "userId": "user123",
  "message": "Ürün fiyatları nedir?"
}
```

**Açıklama:**
- `SessionId`: Her konuşma için benzersiz ID.  Aynı session'daki mesajlar birlikte saklanır.
- `UserId`: Kullanıcıyı tanımlar (opsiyonel, girilmezse "anon" olur).
- `Message`: AI'ya sorulacak soru/mesaj.

---

### 4.2 ChatResponse.cs

**Amaç:** AI'nın kullanıcıya döndüğü yanıt.

```csharp
public class ChatResponse
{
    public string SessionId { get; set; } = string. Empty;   // Oturum ID
    public string Answer { get; set; } = string.Empty;       // AI cevabı
    public List<string> UsedTools { get; set; } = new();    // Kullanılan araçlar
    public bool Success { get; set; } = true;                // Başarı durumu
    public string? ErrorMessage { get; set; }                // Hata mesajı
}
```

**Örnek JSON:**
```json
{
  "sessionId": "user123-session-001",
  "answer": "Ürün A: 500 TL, Ürün B: 1500 TL.. .",
  "usedTools": [],
  "success": true,
  "errorMessage": null
}
```

**Açıklama:**
- `Answer`: LLM'nin ürettiği cevap metni.
- `UsedTools`: Function calling kullanılsaydı hangi fonksiyonlar çağrıldığını gösterir (şu an boş).
- `Success`: İşlem başarılıysa `true`, hata varsa `false`. 

---

### 4.3 Document.cs

**Amaç:** RAG sisteminde kullanılan belge modeli.

```csharp
public class Document
{
    public int Id { get; set; }                    // Belge ID
    public string Title { get; set; } = string.Empty;   // Başlık
    public string Content { get; set; } = string. Empty; // İçerik
    public string Category { get; set; } = string.Empty; // Kategori
}
```

**Örnek:**
```csharp
new Document {
    Id = 1,
    Title = "Kargo Bilgileri",
    Content = "100 TL üzeri kargo ücretsizdir.. .",
    Category = "Kargo"
}
```

**Kullanım:** 
- `KnowledgeBase` tablosundan çekilen veriler bu modele dönüştürülür.
- AI'ya context olarak verilir.

---

### 4.4 OllamaSettings.cs

**Amaç:** Ollama LLM yapılandırması.

```csharp
public class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama2";
    public double Temperature { get; set; } = 0.3;      // Yaratıcılık (0-1)
    public double TopP { get; set; } = 0.9;             // Nucleus sampling
    public double RepeatPenalty { get; set; } = 1.1;    // Tekrar cezası
    public int Timeout { get; set; } = 300;             // Timeout (saniye)
    public int RetryCount { get; set; } = 3;            // Yeniden deneme
}
```

**Açıklama:**
- `Endpoint`: Ollama API adresi.
- `Model`: Kullanılacak model adı (gemma2:2b). 
- `Temperature`: Düşük değer → deterministik, yüksek değer → yaratıcı.
- `TopP`: Nucleus sampling (genellikle 0.9). 
- `RepeatPenalty`: Aynı kelimeleri tekrar etmeyi engellemek için. 

---

### 4.5 UserContext. cs

**Amaç:** Kullanıcının kimlik ve yetki bilgisi (RBAC için).

```csharp
public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string UserName { get; set; } = "Guest";
}
```

**Kullanım:**
- Şu an sadece loglama için kullanılıyor.
- Gelecekte admin/user ayrımı için genişletilebilir.

---

### 4.6 SmartSearchRequest.cs (ChatController.cs içinde)

**Amaç:** Akıllı ürün arama endpoint'i için istek modeli.

```csharp
public class SmartSearchRequest
{
    public string Query { get; set; } = string.Empty;   // Arama metni
    public decimal?  MinPrice { get; set; }               // Min fiyat
    public decimal?  MaxPrice { get; set; }               // Max fiyat
    public string? Category { get; set; }                // Kategori
}
```

**Örnek:**
```json
{
  "query": "ürün",
  "minPrice": 500,
  "maxPrice": 1000,
  "category": "Bilgisayar"
}
```

---

## 5. Repository Katmanı

### 5.1 IChatMemoryRepository.cs

**Amaç:** Chat geçmişi için repository interface.

```csharp
public interface IChatMemoryRepository
{
    Task SaveMessageAsync(string sessionId, string userId, string userName, string role, string content);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId);
    Task ClearSessionAsync(string sessionId);
}
```

**Metodlar:**
1. **SaveMessageAsync**: Mesajı veritabanına kaydet. 
2. **GetHistoryAsync**: Session'a ait tüm mesajları getir. 
3. **ClearSessionAsync**: Session'ı sil. 

---

### 5.2 ChatMemoryRepository.cs

**Amaç:** Chat memory repository implementation (ADO.NET kullanarak).

#### **SaveMessageAsync**
```csharp
public async Task SaveMessageAsync(string sessionId, string userId, string userName, string role, string content)
{
    using (SqlConnection conn = new SqlConnection(_connectionString))
    {
        await conn.OpenAsync();

        // 1. Session güncelle/oluştur
        using (SqlCommand cmdSession = new SqlCommand("sp_UpsertChatSession", conn))
        {
            cmdSession.CommandType = CommandType.StoredProcedure;
            cmdSession.Parameters.Add(new SqlParameter("@SessionId", sessionId));
            cmdSession.Parameters.Add(new SqlParameter("@UserId", userId ??  "anonymous"));
            cmdSession.Parameters. Add(new SqlParameter("@UserName", userName ?? "Guest"));
            await cmdSession.ExecuteNonQueryAsync();
        }

        // 2. Mesajı kaydet
        using (SqlCommand cmdMessage = new SqlCommand("sp_SaveChatMessage", conn))
        {
            cmdMessage.CommandType = CommandType.StoredProcedure;
            cmdMessage.Parameters.Add(new SqlParameter("@SessionId", sessionId));
            cmdMessage.Parameters.Add(new SqlParameter("@Role", role));
            cmdMessage. Parameters.Add(new SqlParameter("@Content", content));
            await cmdMessage.ExecuteNonQueryAsync();
        }
    }
}
```

**Açıklama:**
1. `sp_UpsertChatSession`: Session yoksa oluşturur, varsa `LastActivityDate` günceller.
2.  `sp_SaveChatMessage`: Mesajı `ChatMessages` tablosuna ekler.

---

#### **GetHistoryAsync**
```csharp
public async Task<List<ChatMessage>> GetHistoryAsync(string sessionId)
{
    List<ChatMessage> messages = new List<ChatMessage>();

    using (SqlConnection conn = new SqlConnection(_connectionString))
    {
        using (SqlCommand cmd = new SqlCommand("sp_GetChatHistory", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters. Add(new SqlParameter("@SessionId", sessionId));

            await conn.OpenAsync();

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    string role = reader.GetString(reader.GetOrdinal("Role"));
                    string content = reader.GetString(reader. GetOrdinal("Content"));

                    ChatRole chatRole = role. ToLower() switch
                    {
                        "user" => ChatRole.User,
                        "assistant" => ChatRole.Assistant,
                        "system" => ChatRole.System,
                        _ => ChatRole.User
                    };

                    messages.Add(new ChatMessage(chatRole, content));
                }
            }
        }
    }

    return messages;
}
```

**Açıklama:**
- `sp_GetChatHistory` stored procedure'ünü çağırır. 
- `Role` string'ini `ChatRole` enum'una dönüştürür. 
- `ChatMessage` listesi döner (AI'ya context olarak verilecek).

---

### 5.3 IKnowledgeBaseRepository.cs

**Amaç:** Bilgi bankası için repository interface.

```csharp
public interface IKnowledgeBaseRepository
{
    Task<List<Document>> SearchDocuments(string query);
    Task<List<Document>> GetAllDocuments();
    Task<List<Document>> SmartProductSearch(string query, decimal?  minPrice, decimal? maxPrice, string? category);
}
```

---

### 5.4 KnowledgeBaseRepository.cs

#### **SearchDocuments** (RAG için basit arama)
```csharp
public async Task<List<Document>> SearchDocuments(string query)
{
    List<Document> documents = new List<Document>();

    using (SqlConnection conn = new SqlConnection(_connectionString))
    {
        using (SqlCommand cmd = new SqlCommand("sp_SearchKnowledgeBase", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@SearchQuery", SqlDbType.NVarChar, 200) { Value = query ??  "" });

            await conn.OpenAsync();

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    documents.Add(new Document
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("DocumentId")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Content = reader.GetString(reader.GetOrdinal("Content")),
                        Category = reader.IsDBNull(reader.GetOrdinal("Category")) 
                            ? "" 
                            : reader.GetString(reader.GetOrdinal("Category"))
                    });
                }
            }
        }
    }

    return documents;
}
```

**Açıklama:**
- `sp_SearchKnowledgeBase`: Title, Content, Tags alanlarında arama yapar. 
- `ViewCount` otomatik artar (SP içinde).

---

#### **SmartProductSearch** (Fiyat + Kategori filtreli arama)
```csharp
public async Task<List<Document>> SmartProductSearch(string query, decimal? minPrice, decimal? maxPrice, string? category)
{
    using (SqlCommand cmd = new SqlCommand("sp_SmartProductSearch", conn))
    {
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters. Add(new SqlParameter("@SearchQuery", SqlDbType.NVarChar, 200) { Value = query ?? "" });
        cmd.Parameters.Add(new SqlParameter("@MinPrice", SqlDbType.Decimal) { Value = (object)minPrice ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@MaxPrice", SqlDbType.Decimal) { Value = (object)maxPrice ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Category", SqlDbType.NVarChar, 100) { Value = (object)category ?? DBNull.Value });

        // ...  SQL execution
    }
}
```

**Açıklama:**
- `sp_SmartProductSearch`: Price ve Content içindeki kategori bilgisi ile filtreleme yapar.
- Null parametreler `DBNull.Value` olarak gönderilir.

---

## 6.  Servis Katmanı

### 6.1 ChatService. cs

**Amaç:** Ana chat iş mantığını yönetir.

#### **ProcessMessageAsync** (Ana metod)
```csharp
public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request, UserContext userContext)
{
    // 1. RAG - Belge arama
    var relevantDocs = await _rag.SearchDocumentsAsync(request.Message);
    var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);

    // 2. System prompt oluştur
    var systemPrompt = BuildSystemPrompt(userContext, ragContext);

    // 3.  Geçmişi al
    var messages = await _memoryRepository.GetHistoryAsync(request.SessionId);
    messages = messages.Where(m => m.Role != ChatRole.System).ToList();

    // 4. System prompt ekle
    messages.Insert(0, new ChatMessage(ChatRole. System, systemPrompt));

    // 5. Kullanıcı mesajını ekle
    messages.Add(new ChatMessage(ChatRole.User, request.Message));

    // 6. LLM'den cevap al
    var responseText = "";
    await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
    {
        responseText += update. Text;
    }

    // 7. Veritabanına kaydet
    await _memoryRepository.SaveMessageAsync(request.SessionId, userContext.UserId, userContext. UserName, "user", request.Message);
    await _memoryRepository.SaveMessageAsync(request.SessionId, userContext.UserId, userContext.UserName, "assistant", responseText);

    return new ChatResponse { SessionId = request.SessionId, Answer = responseText, Success = true };
}
```

**Akış:**
1. **RAG Arama**: Kullanıcı mesajına göre ilgili belgeleri bul.
2. **Context Hazırla**: Belgeleri prompt formatına çevir.
3. **Geçmiş Yükle**: Önceki konuşmayı getir (System mesajları hariç).
4. **System Prompt Ekle**: AI'ya talimatları ver.
5. **LLM Çağrısı**: Streaming yanıt al.
6. **Veritabanına Kaydet**: User ve assistant mesajlarını sakla.

---

#### **BuildSystemPrompt**
```csharp
private string BuildSystemPrompt(UserContext userContext, string ragContext)
{
    var prompt = @"Sen bir müşteri destek asistanısın. 

KURALLAR:
1.  SADECE bilgi bankasındaki bilgileri kullan
2. Bilmediğin şeyi ASLA uydurma
3. Türkçe konuş

⚠️ ÖNEMLİ FİYAT KURALI:
- Eğer kullanıcı 'kampanya', 'indirim', 'kış' kelimesini kullanıyorsa:
  → SADECE kampanya belgesindeki '→' işaretinden SONRA​KI fiyatı söyle
  → '960 TL' gibi indirimli fiyatı kullan
- Normal fiyat sorarsa normal belgedeki fiyatı söyle";

    if (! string.IsNullOrEmpty(ragContext))
    {
        prompt += $"\n\n{ragContext}\n\n";
        prompt += "SADECE yukarıdaki bilgileri kullan!  ";
    }

    return prompt;
}
```

**Açıklama:**
- **Hallusinasyon Önleme**: "Bilmediğini uydurma" kuralı. 
- **Kampanya Kuralı**: AI'nın doğru fiyatı seçmesini sağlar. 
- **RAG Context**: Bulunan belgeler system prompt'a eklenir.

---

### 6.2 RagService.cs

**Amaç:** RAG arama ve keyword extraction mantığı.

#### **SearchDocumentsAsync**
```csharp
public async Task<List<Document>> SearchDocumentsAsync(string query)
{
    // 1. Keyword'leri çıkar
    var keywords = ExtractKeywords(query);

    // 2. Her keyword için arama yap
    var allDocuments = new List<Document>();
    foreach (var keyword in keywords)
    {
        var docs = await _knowledgeBaseRepository.SearchDocuments(keyword);
        allDocuments.AddRange(docs);
    }

    // 3.  Duplicate'leri temizle
    var uniqueDocs = allDocuments
        .GroupBy(d => d.Id)
        .Select(g => g.First())
        .ToList();

    return uniqueDocs;
}
```

**Açıklama:**
- Sorguyu keyword'lere ayırır.
- Her keyword için veritabanında arama yapar.
- Aynı belge birden fazla keyword ile bulunmuşsa bir kez döner.

---

#### **ExtractKeywords** (Türkçe NLP)
```csharp
private List<string> ExtractKeywords(string query)
{
    var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bir", "ve", "veya", "ile", "için", "ne", "nedir", "nasıl",
        "mi", "mu", "mı", "mü", "da", "de", "ta", "te",
        "kaç", "hangi", "şu", "bu", "o"
    };

    var separators = new[] { " ", "? ", "!", ".", ",", ";", ":" };

    var words = query
        .ToLowerInvariant()
        .Split(separators, StringSplitOptions.RemoveEmptyEntries)
        .Where(w => w.Length > 2 && !stopwords.Contains(w))
        . Distinct()
        .ToList();

    return words. Any() ? words : new List<string> { query };
}
```

**Açıklama:**
- **Stopwords**: Gereksiz Türkçe kelimeleri (bir, ve, nedir vb.) filtreler.
- **Ayırma**: Boşluk ve noktalama işaretlerine göre böler.
- **Filtreleme**: 2 karakterden kısa veya stopword olan kelimeleri çıkarır.

---

#### **FormatDocumentsAsContext**
```csharp
public string FormatDocumentsAsContext(List<Document> documents)
{
    if (! documents.Any()) return "";

    return "BİLGİ BANKASI:\n" +
           string.Join("\n", documents.Select(d => $"• {d.Title}: {d.Content}"));
}
```

**Örnek Çıktı:**
```
BİLGİ BANKASI:
• Kargo Bilgileri: 100 TL üzeri ücretsiz kargo... 
• İade Politikası: 14 gün içinde iade...
```

---

### 6.3 OllamaChatClient.cs

**Amaç:** Ollama API ile iletişim. 

#### **GetResponseAsync** (Sync yanıt)
```csharp
public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
    IEnumerable<ChatMessage> chatMessages,
    ChatOptions?  options = null,
    CancellationToken cancellationToken = default)
{
    var request = BuildRequest(chatMessages, stream: false);

    var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
    response.EnsureSuccessStatusCode();

    var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);

    var assistantMessage = new ChatMessage(ChatRole. Assistant, ollamaResponse?. Message?. Content ?? "");

    return new Microsoft.Extensions.AI.ChatResponse(assistantMessage);
}
```

**Açıklama:**
- Ollama'nın `/api/chat` endpoint'ine POST request atar.
- `stream: false` → Tüm yanıtı bir seferde alır.
- `ChatResponse` döner.

---

#### **GetStreamingResponseAsync** (Streaming yanıt)
```csharp
public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(...)
{
    var request = BuildRequest(chatMessages, stream: true);
    
    using var response = await _httpClient. SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    using var stream = await response.Content. ReadAsStreamAsync(cancellationToken);
    using var reader = new StreamReader(stream);

    string? line;
    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
    {
        OllamaResponse? chunk = JsonSerializer.Deserialize<OllamaResponse>(line);

        if (chunk?. Message?.Content != null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.Message. Content);
        }
    }
}
```

**Açıklama:**
- `stream: true` → Yanıt satır satır gelir.
- `yield return` → Her chunk async olarak döner.
- UI'da kelime kelime yazdırmak için kullanılır.

---

## 7. Controller Katmanı

### ChatController.cs

#### **SendMessage** (Ana chat endpoint)
```csharp
[HttpPost("message")]
public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.SessionId))
        return BadRequest(new { error = "Mesaj ve SessionId zorunludur" });

    var userContext = new UserContext
    {
        UserId = request.UserId ??  "anon",
        UserName = "Ziyaretçi"
    };

    var response = await _chatService.ProcessMessageAsync(request, userContext);
    return response. Success ? Ok(response) : StatusCode(500, response);
}
```

**Kullanım:**
```bash
POST /api/Chat/message
{
  "sessionId": "test-001",
  "userId": "user1",
  "message": "Ürün fiyatları nedir?"
}
```

---

#### **SmartProductSearch** (Akıllı arama endpoint)
```csharp
[HttpPost("smart-search")]
public async Task<ActionResult> SmartProductSearch([FromBody] SmartSearchRequest request)
{
    var documents = await _knowledgeBaseRepository.SmartProductSearch(
        request.Query,
        request.MinPrice,
        request.MaxPrice,
        request.Category
    );

    return Ok(new
    {
        query = request.Query,
        filters = new { minPrice = request.MinPrice, maxPrice = request.MaxPrice, category = request.Category },
        resultCount = documents.Count,
        products = documents
    });
}
```

**Kullanım:**
```bash
POST /api/Chat/smart-search
{
  "query": "ürün",
  "minPrice": 500,
  "maxPrice": 1000,
  "category": "Bilgisayar"
}
```

---

## 8.  Yapılandırma ve Başlatma

### Program.cs

```csharp
// 1. Ollama ayarlarını oku
var ollamaSettings = builder.Configuration.GetSection("Ollama"). Get<OllamaSettings>() ?? new OllamaSettings();

// 2. Ollama client kaydı
var ollamaClient = new OllamaChatClient(ollamaSettings.Endpoint, ollamaSettings.Model, ollamaSettings);
builder.Services.AddSingleton<IChatClient>(ollamaClient);

// 3. Repository ve Servisler
builder.Services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
builder.Services.AddScoped<IChatMemoryRepository, ChatMemoryRepository>();
builder.Services.AddScoped<RagService>();
builder.Services. AddScoped<ChatService>();

// 4. Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
```

**Açıklama:**
- **Singleton:** OllamaChatClient (tüm request'ler aynı instance kullanır).
- **Scoped:** Repository ve Service'ler (her request için yeni instance). 

---

## 9. İstek-Yanıt Akışı

### Örnek Senaryo: "Ürün fiyatları nedir?"

```
1. USER → POST /api/Chat/message
   Body: { "sessionId": "test-001", "message": "Ürün fiyatları nedir?" }

2. ChatController. SendMessage()
   ↓
3. ChatService.ProcessMessageAsync()
   ├─ RagService.SearchDocumentsAsync("Ürün fiyatları nedir?")
   │  ├─ ExtractKeywords() → ["ürün", "fiyatları"]
   │  ├─ KnowledgeBaseRepository.SearchDocuments("ürün")  → SQL SP
   │  └─ KnowledgeBaseRepository.SearchDocuments("fiyatları") → SQL SP
   │      → Result: [Document{Id:1, Title:"Ürün Bilgileri", ... }]
   │
   ├─ FormatDocumentsAsContext() → "BİLGİ BANKASI:\n• Ürün Bilgileri: ..."
   │
   ├─ BuildSystemPrompt() → "Sen müşteri destek asistanısın.. .\nBİLGİ BANKASI:..."
   │
   ├─ ChatMemoryRepository.GetHistoryAsync("test-001") → []
   │
   ├─ messages. Add(SystemPrompt)
   ├─ messages.Add(UserMessage: "Ürün fiyatları nedir?")
   │
   ├─ OllamaChatClient.GetStreamingResponseAsync(messages)
   │  └─ HTTP POST → http://localhost:11434/api/chat
   │      Response: "Ürün A: 500 TL, Ürün B: 1500 TL..."
   │
   ├─ ChatMemoryRepository.SaveMessageAsync(user message)
   └─ ChatMemoryRepository.SaveMessageAsync(assistant message)

4. ChatController → HTTP 200 OK
   Body: { "sessionId": "test-001", "answer": "Ürün A: 500 TL.. .", "success": true }
```

---

## 10. Önemli Kavramlar

### 10.1 RAG (Retrieval-Augmented Generation)
**Ne:** LLM'e dışarıdan bilgi sağlayarak hallusinasyonu azaltma tekniği.

**Nasıl Çalışır:**
1.  Kullanıcı sorusu → Keyword extraction
2. Keyword'lerle veritabanında arama
3. Bulunan belgeler → System prompt'a eklenir
4. LLM sadece bu bilgilerle cevap üretir

**Örnek:**
```
User: "Kargo ücreti ne kadar?"
RAG Search: "kargo, ücreti" → KnowledgeBase
Result: "Kargo Bilgileri: 100 TL üzeri ücretsiz..."
Prompt: "BİLGİ BANKASI:\n• Kargo Bilgileri: 100 TL üzeri..."
LLM: "100 TL üzeri siparişlerde kargo ücretsizdir."
```

---

### 10.2 Session Yönetimi
**Ne:** Kullanıcıların konuşma geçmişini saklama. 

**Tablolar:**
- `ChatSessions`: Session metadata (userId, startDate, lastActivityDate)
- `ChatMessages`: Her mesaj (role: user/assistant, content)

**Akış:**
```sql
-- Session oluştur/güncelle
EXEC sp_UpsertChatSession @SessionId='test-001', @UserId='user1';

-- Mesaj kaydet
EXEC sp_SaveChatMessage @SessionId='test-001', @Role='user', @Content='Merhaba';
EXEC sp_SaveChatMessage @SessionId='test-001', @Role='assistant', @Content='Nasıl yardımcı olabilirim?';

-- Geçmişi getir
EXEC sp_GetChatHistory @SessionId='test-001';
```

---

### 10.3 Streaming vs Non-Streaming
**Non-Streaming:**
```csharp
var response = await _chatClient.GetResponseAsync(messages);
// Tüm cevap bir seferde gelir
```

**Streaming:**
```csharp
await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text); // Kelime kelime yazdır
}
```

**Avantaj:** Kullanıcı yanıtı daha hızlı görür (UX iyileşir).

---

### 10.4 Dependency Injection (DI)
**Neden Kullanılır? **
- Loose coupling (Katmanlar birbirine bağımlı değil)
- Test edilebilirlik (Mock repository inject edilebilir)
- Lifecycle yönetimi (Singleton, Scoped, Transient)

**Örnek:**
```csharp
// Program.cs
builder.Services.AddScoped<IChatMemoryRepository, ChatMemoryRepository>();

// ChatService.cs
public ChatService(IChatMemoryRepository memoryRepository) // DI ile inject edilir
{
    _memoryRepository = memoryRepository;
}
```

---

## 📊 Özet Tablo

| Bileşen | Sorumluluk | Bağımlılıklar |
|---------|-----------|---------------|
| **ChatController** | HTTP endpoint'leri | ChatService, RagService, KnowledgeBaseRepo |
| **ChatService** | Chat mantığı, LLM çağrısı | IChatClient, IChatMemoryRepo, RagService |
| **RagService** | Belge arama, keyword extraction | IKnowledgeBaseRepo |
| **OllamaChatClient** | Ollama API iletişimi | HttpClient |
| **ChatMemoryRepository** | Chat geçmişi DB işlemleri | SqlConnection |
| **KnowledgeBaseRepository** | Bilgi bankası DB işlemleri | SqlConnection |

---

## 🎓 Sonuç

Bu API şu özelliklere sahiptir:
1. ✅ **RAG Sistemi**: Veritabanından bilgi çekerek LLM'e context sağlar. 
2. ✅ **Session Yönetimi**: Konuşma geçmişini saklar.
3. ✅ **Akıllı Arama**: Fiyat + Kategori filtreli ürün arama.
4. ✅ **Hallusinasyon Önleme**: Sadece bilgi bankasındaki bilgileri kullanır.
5. ✅ **Türkçe NLP**: Stopwords temizleme, keyword extraction.
6. ✅ **ADO.NET**: Stored procedure kullanarak performanslı DB erişimi. 

**Veritabanı dökümanını da ister misiniz?** 🚀
# 🔧 AI ChatBot Projesi - İyileştirme Önerileri Rehberi

**Tarih:** 24 Aralık 2025  
**Hedef:** Proje geliştirme önerileri  
**Tahmini Süre:** 4-6 hafta (opsiyonel)

---

## 📋 İyileştirme Kategorileri

Bu dokümanda projeyi geliştirmek isteyenler için çeşitli öneriler bulunmaktadır:

| Kategori | Tahmini Süre | Açıklama |
|----------|--------------|----------|
| Güvenlik | 1-2 gün | Connection string, validation, rate limiting |
| Test & Performans | 1-2 hafta | Unit tests, caching, optimizasyon |
| Code Quality & Docs | 1 hafta | Refactoring, dokümantasyon |
| İleri Özellikler | 1-2 hafta | WebSocket, monitoring, advanced features |

---

## 🔒 Güvenlik İyileştirme Önerileri

### 1.1 Connection String Güvenliği

#### Adım 1: User Secrets Konfigürasyonu (Development)

```bash
cd /home/runner/work/AIChatBot/AIChatBot/AIChatBot

# User secrets initialize et
dotnet user-secrets init

# Connection string'i ekle
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=ABDURRAHMAN;Database=AIChatBotDb;Trusted_Connection=true;TrustServerCertificate=True;Encrypt=False;"

# Listeyi kontrol et
dotnet user-secrets list
```

#### Adım 2: appsettings.json'ı Güncelle

```json
// appsettings.json - Sensitive data kaldır
{
  "ConnectionStrings": {
    "DefaultConnection": "" // Boş bırak - user secrets'tan gelecek
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "llama3.1",
    "EmbedModel": "nomic-embed-text"
    // Diğer ayarlar kalabilir
  }
}
```

#### Adım 3: Production Ortamı için Environment Variables

```bash
# Linux/Docker
export ConnectionStrings__DefaultConnection="Server=prod-server;..."

# Windows
setx ConnectionStrings__DefaultConnection "Server=prod-server;..."

# Azure App Service
# Portal → Configuration → Application Settings
# Name: ConnectionStrings__DefaultConnection
# Value: Server=...
```

#### Adım 4: .gitignore Kontrolü

```gitignore
# .gitignore'a eklendiğinden emin ol
appsettings.Development.json
*.user
*.suo
secrets.json
```

**Test:**
```bash
dotnet run
# Log'larda connection string görünmemeli
```

---

### 1.2 Input Validation Güçlendirme

#### Adım 1: NuGet Package Ekle

```bash
dotnet add package FluentValidation.AspNetCore --version 11.3.0
```

#### Adım 2: Validator Sınıfları Oluştur

**Dosya: `Validators/ChatRequestValidator.cs`**
```csharp
using FluentValidation;
using AIChatBot.Models;

namespace AIChatBot.Validators
{
    public class ChatRequestValidator : AbstractValidator<ChatRequest>
    {
        public ChatRequestValidator()
        {
            RuleFor(x => x.SessionId)
                .NotEmpty().WithMessage("SessionId zorunludur")
                .Length(1, 100).WithMessage("SessionId 1-100 karakter arası olmalıdır")
                .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("SessionId sadece alfanumerik karakterler içerebilir");

            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Mesaj zorunludur")
                .Length(1, 2000).WithMessage("Mesaj 1-2000 karakter arası olmalıdır");

            RuleFor(x => x.UserId)
                .Length(1, 50).When(x => !string.IsNullOrEmpty(x.UserId))
                .WithMessage("UserId 1-50 karakter arası olmalıdır");

            RuleFor(x => x.Role)
                .Must(role => new[] { "Admin", "Customer", "Moderator" }.Contains(role))
                .WithMessage("Role sadece Admin, Customer veya Moderator olabilir");

            RuleFor(x => x.AllowedProductIds)
                .Must(ids => ids == null || ids.Count <= 100)
                .WithMessage("AllowedProductIds maksimum 100 ürün içerebilir");
        }
    }
}
```

#### Adım 3: Program.cs'de Validator Kaydı

```csharp
// Program.cs - Services bölümüne ekle
using FluentValidation;
using FluentValidation.AspNetCore;
using AIChatBot.Validators;

// ...

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();
```

#### Adım 4: Controller'da Model State Kontrolü

```csharp
// ChatController.cs - Zaten mevcut, sadece iyileştir
[HttpPost("message")]
public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
{
    // FluentValidation otomatik çalışacak
    if (!ModelState.IsValid)
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage);
        
        return BadRequest(new { errors });
    }

    // Devamı aynı...
}
```

**Test:**
```bash
# Test 1: Boş message
curl -X POST http://localhost:5223/api/Chat/message \
  -H "Content-Type: application/json" \
  -d '{"sessionId": "test", "message": ""}'

# Expected: 400 Bad Request

# Test 2: Invalid role
curl -X POST http://localhost:5223/api/Chat/message \
  -H "Content-Type: application/json" \
  -d '{"sessionId": "test", "message": "hello", "role": "InvalidRole"}'

# Expected: 400 Bad Request
```

---

### 1.3 Rate Limiting Ekleme

#### Adım 1: NuGet Package Ekle

```bash
dotnet add package AspNetCoreRateLimit --version 5.0.0
```

#### Adım 2: appsettings.json'a Rate Limit Konfigürasyonu Ekle

```json
// appsettings.json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      },
      {
        "Endpoint": "*",
        "Period": "1h",
        "Limit": 1000
      },
      {
        "Endpoint": "POST:/api/Chat/message",
        "Period": "1m",
        "Limit": 30
      }
    ]
  }
}
```

#### Adım 3: Program.cs'de Rate Limiting Kaydı

```csharp
// Program.cs
using AspNetCoreRateLimit;

// Services
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Middleware
var app = builder.Build();

app.UseIpRateLimiting();  // ⚠️ UseRouting()'den önce ekle

app.UseAuthorization();
app.MapControllers();
```

**Test:**
```bash
# Rate limit test
for i in {1..35}; do
  curl -X POST http://localhost:5223/api/Chat/message \
    -H "Content-Type: application/json" \
    -d '{"sessionId": "test", "message": "test"}'
  echo ""
done

# 30 istekten sonra: 429 Too Many Requests
```

---

### 1.4 HTTPS Enforcement (Production)

**Program.cs - Middleware ekle:**
```csharp
var app = builder.Build();

// ✅ Production'da HTTPS zorunlu
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();  // HTTP Strict Transport Security
}

app.UseSerilogRequestLogging();
// ...
```

---

### 1.5 Null Reference Warning'leri Düzelt

#### Fix 1: OllamaHealthCheck.cs (Line 70)

```csharp
// Önce:
data: new Dictionary<string, object>
{
    { "responseTime", response.Headers.Date }  // ⚠️ CS8604
}

// Sonra:
data: new Dictionary<string, object>
{
    { "responseTime", response.Headers.Date?.ToString() ?? "N/A" }
}
```

#### Fix 2: Program.cs (Line 357, 359)

```csharp
// Önce:
diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress);

// Sonra:
diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
```

**Test:**
```bash
dotnet build
# Warning sayısı azalmalı
```

---

## 🧪 Test ve Performans Önerileri

### 2.1 Unit Test Altyapısı Kurulumu

#### Adım 1: Test Projesi Oluştur

```bash
cd /home/runner/work/AIChatBot/AIChatBot

# xUnit test projesi
dotnet new xunit -n AIChatBot.Tests

# Referansları ekle
cd AIChatBot.Tests
dotnet add reference ../AIChatBot/AIChatBot.csproj

# Test package'ları ekle
dotnet add package Moq --version 4.20.70
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 8.0.0
dotnet add package xunit.runner.visualstudio --version 2.5.3

cd ..
```

#### Adım 2: Örnek Unit Test (ChatService)

**Dosya: `AIChatBot.Tests/Services/ChatServiceTests.cs`**
```csharp
using Xunit;
using Moq;
using FluentAssertions;
using AIChatBot.Services;
using AIChatBot.Models;
using AIChatBot.Repository.ChatMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AIChatBot.Tests.Services
{
    public class ChatServiceTests
    {
        private readonly Mock<IChatClient> _mockChatClient;
        private readonly Mock<IChatMemoryRepository> _mockMemoryRepo;
        private readonly Mock<RagService> _mockRagService;
        private readonly Mock<ILogger<ChatService>> _mockLogger;
        private readonly ChatService _chatService;

        public ChatServiceTests()
        {
            _mockChatClient = new Mock<IChatClient>();
            _mockMemoryRepo = new Mock<IChatMemoryRepository>();
            _mockRagService = new Mock<RagService>();
            _mockLogger = new Mock<ILogger<ChatService>>();

            _chatService = new ChatService(
                _mockChatClient.Object,
                _mockMemoryRepo.Object,
                _mockRagService.Object,
                new List<AITool>(),
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task ProcessMessageAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new ChatRequest
            {
                SessionId = "test-session",
                Message = "Hello",
                UserId = "user123",
                Role = "Customer"
            };

            var userContext = new UserContext
            {
                UserId = "user123",
                UserName = "Test User",
                Role = "Customer"
            };

            _mockMemoryRepo
                .Setup(x => x.GetHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ChatMessage>());

            _mockRagService
                .Setup(x => x.SemanticSearchAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Document>());

            // Mock streaming response
            var streamingResponse = AsyncEnumerable(
                new ChatResponseUpdate(ChatRole.Assistant, "Test response")
            );

            _mockChatClient
                .Setup(x => x.GetStreamingResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(streamingResponse);

            // Act
            var result = await _chatService.ProcessMessageAsync(request, userContext);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Answer.Should().NotBeEmpty();
            result.SessionId.Should().Be("test-session");
        }

        [Fact]
        public async Task ProcessMessageAsync_EmptyMessage_ReturnsError()
        {
            // Arrange
            var request = new ChatRequest
            {
                SessionId = "test-session",
                Message = "",
                UserId = "user123"
            };

            var userContext = new UserContext
            {
                UserId = "user123",
                UserName = "Test User"
            };

            // Act & Assert
            // Controller'da validation var, service'e ulaşmaz
            // Ama yine de test edelim
            request.Message.Should().BeEmpty();
        }

        // Helper method
        private async IAsyncEnumerable<T> AsyncEnumerable<T>(params T[] items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }
    }
}
```

#### Adım 3: Test Çalıştırma

```bash
cd AIChatBot.Tests
dotnet test

# Coverage raporu (optional)
dotnet add package coverlet.collector
dotnet test --collect:"XPlat Code Coverage"
```

---

### 2.2 Integration Test Örneği

**Dosya: `AIChatBot.Tests/Integration/ChatControllerIntegrationTests.cs`**
```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FluentAssertions;
using AIChatBot.Models;

namespace AIChatBot.Tests.Integration
{
    public class ChatControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ChatControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task SendMessage_ValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new ChatRequest
            {
                SessionId = "integration-test",
                Message = "Test message",
                UserId = "test-user",
                Role = "Customer"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Chat/message", request);

            // Assert
            response.Should().BeSuccessful();
            var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task GetHistory_ValidSession_ReturnsHistory()
        {
            // Arrange
            var sessionId = "test-session";

            // Act
            var response = await _client.GetAsync($"/api/Chat/history?sessionId={sessionId}");

            // Assert
            response.Should().BeSuccessful();
        }
    }
}
```

**Test Coverage Hedefi:** %70+

---

### 2.3 Performance Optimizations

#### 2.3.1 Memory Cache Ekleme

**Dosya: `Services/CachedRagService.cs`**
```csharp
using Microsoft.Extensions.Caching.Memory;

namespace AIChatBot.Services
{
    public class CachedRagService : RagService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedRagService> _logger;

        public CachedRagService(
            IKnowledgeBaseRepository knowledgeBaseRepository,
            ILogger<CachedRagService> logger,
            IMemoryCache cache,
            EmbeddingService? embeddingService = null)
            : base(knowledgeBaseRepository, logger, embeddingService)
        {
            _cache = cache;
            _logger = logger;
        }

        public override async Task<List<Document>> SemanticSearchAsync(string query, int topK = 5)
        {
            var cacheKey = $"semantic_search_{query}_{topK}";

            if (_cache.TryGetValue(cacheKey, out List<Document>? cachedResult))
            {
                _logger.LogDebug("[CACHE-HIT] Semantic search: {Query}", query);
                return cachedResult!;
            }

            _logger.LogDebug("[CACHE-MISS] Semantic search: {Query}", query);

            var result = await base.SemanticSearchAsync(query, topK);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetPriority(CacheItemPriority.Normal);

            _cache.Set(cacheKey, result, cacheOptions);

            return result;
        }
    }
}
```

**Program.cs'de:**
```csharp
// Memory cache ekle
builder.Services.AddMemoryCache();

// CachedRagService kullan
builder.Services.AddScoped<RagService, CachedRagService>();
```

#### 2.3.2 N+1 Query Fix (Keyword Search)

**KnowledgeBaseRepository.cs - Yeni metod ekle:**
```csharp
public async Task<List<Document>> SearchDocumentsBatch(List<string> keywords)
{
    var documents = new List<Document>();

    try
    {
        var keywordsParam = string.Join(",", keywords);
        
        _logger.LogInformation("[SEARCH-BATCH] Keywords: {Count}", keywords.Count);

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_SearchKnowledgeBaseBatch", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@SearchKeywords", SqlDbType.NVarChar, 1000) 
                    { Value = keywordsParam });

                await conn.OpenAsync();

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        documents.Add(ReadDocumentFromReader(reader));
                    }
                }
            }
        }

        // Duplicate'leri temizle
        documents = documents.GroupBy(d => d.Id).Select(g => g.First()).ToList();

        _logger.LogInformation("[SEARCH-BATCH] {Count} belge bulundu", documents.Count);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[SEARCH-BATCH-ERROR]");
        throw;
    }

    return documents;
}
```

**SQL Stored Procedure:**
```sql
CREATE PROCEDURE sp_SearchKnowledgeBaseBatch
    @SearchKeywords NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    -- String split (SQL Server 2016+)
    DECLARE @Keywords TABLE (Keyword NVARCHAR(100));
    
    INSERT INTO @Keywords (Keyword)
    SELECT value FROM STRING_SPLIT(@SearchKeywords, ',');

    -- Tek sorguda ara
    SELECT DISTINCT 
        kb.Id,
        kb.Title,
        kb.Content,
        kb.Category,
        kb.CreatedAt
    FROM KnowledgeBase kb
    INNER JOIN @Keywords k 
        ON kb.Title LIKE '%' + k.Keyword + '%' 
        OR kb.Content LIKE '%' + k.Keyword + '%'
    WHERE kb.IsActive = 1
    ORDER BY kb.CreatedAt DESC;
END
```

**RagService.cs - SearchDocumentsAsync güncelle:**
```csharp
public async Task<List<Document>> SearchDocumentsAsync(string query)
{
    _logger.LogInformation("[KEYWORD-SEARCH] Query: '{Query}'", query);

    var keywords = ExtractKeywords(query);

    _logger.LogDebug("[KEYWORD-SEARCH] Keywords: {Keywords}", string.Join(", ", keywords));

    // ✅ Tek sorguda tüm keyword'leri ara
    var documents = await _knowledgeBaseRepository.SearchDocumentsBatch(keywords);

    _logger.LogInformation("[KEYWORD-SEARCH] Toplam {Count} belge bulundu", documents.Count);

    return documents;
}
```

---

## 📝 Code Quality ve Dokümantasyon Önerileri

### 3.1 README.md Oluşturma

**Dosya: `README.md`**
```markdown
# 🤖 AI ChatBot Sistemi

Modern, production-ready AI destekli müşteri destek chatbot sistemi.

## ✨ Özellikler

- 🧠 Ollama tabanlı LLM entegrasyonu (llama3.1)
- 🔍 RAG (Retrieval-Augmented Generation) desteği
- 📊 Vector search (semantic search)
- 💾 SQL Server veritabanı
- 📝 Comprehensive logging (Serilog)
- 🏥 Health checks
- 🔧 Tool calling (Function calling)
- 🇹🇷 Türkçe dil desteği

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 SDK
- SQL Server 2019+ veya SQL Server Express
- Ollama (local LLM server)

### Installation

```bash
# 1. Clone repository
git clone https://github.com/AbdurrahmanKaraoglu/AIChatBot.git
cd AIChatBot

# 2. Setup user secrets (Development)
cd AIChatBot
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"

# 3. Restore packages
dotnet restore

# 4. Run database migrations (SQL scripts in /Database folder)
# Execute scripts in order:
# - 01_CreateTables.sql
# - 02_StoredProcedures.sql
# - 03_SeedData.sql

# 5. Start Ollama
ollama serve

# Pull required models
ollama pull llama3.1
ollama pull nomic-embed-text

# 6. Run application
dotnet run
```

### Access Points

- **API Base:** http://localhost:5223
- **Swagger UI:** http://localhost:5223/swagger
- **Health Check:** http://localhost:5223/health

## 📚 API Usage

### Send Message

```bash
curl -X POST http://localhost:5223/api/Chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "user-123-session",
    "message": "Merhaba, yardım edebilir misin?",
    "userId": "user-123",
    "role": "Customer"
  }'
```

### Get Chat History

```bash
curl http://localhost:5223/api/Chat/history?sessionId=user-123-session
```

## 🏗️ Architecture

```
┌─────────────┐
│  Frontend   │
└──────┬──────┘
       │ HTTP
┌──────▼──────┐
│ Controllers │
└──────┬──────┘
       │
┌──────▼──────┐
│  Services   │  ← ChatService, RagService
└──────┬──────┘
       │
┌──────▼──────┐
│ Repository  │  ← Database Layer
└──────┬──────┘
       │
┌──────▼──────┐
│ SQL Server  │
└─────────────┘
```

## 🧪 Testing

```bash
# Run unit tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 📖 Documentation

- [Detaylı Teknik Doküman](./AIChatBot/AI%20ChatBot%20Sistemi%20-%20Detaylı%20Teknik.md)
- [Veritabanı Dokümantasyonu](./AIChatBot/AI%20ChatBot%20Veritabanı%20-%20Detaylı%20Teknik%20Dokümantasyon.md)
- [Teknik Terimler Sözlüğü](./AIChatBot/AI%20ChatBot%20Sistemi%20-%20Teknik%20Terimler%20Sözlüğü.md)

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License.

## 👨‍💻 Author

**Abdurrahman Karaoğlu**

## 🙏 Acknowledgments

- Microsoft.Extensions.AI
- Ollama
- Serilog
```

---

### 3.2 CI/CD Pipeline (GitHub Actions)

**Dosya: `.github/workflows/build-and-test.yml`**
```yaml
name: Build and Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --configuration Release
    
    - name: Test
      run: dotnet test --no-build --configuration Release --verbosity normal
    
    - name: Code Coverage
      run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
    
    - name: Upload coverage to Codecov
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage/**/coverage.cobertura.xml
        flags: unittests
        name: codecov-umbrella

  security-scan:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Run security scan
      uses: securego/gosec@master
      with:
        args: ./...
```

---

### 3.3 Docker Support

**Dosya: `Dockerfile`**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5223
EXPOSE 7090

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AIChatBot/AIChatBot.csproj", "AIChatBot/"]
RUN dotnet restore "AIChatBot/AIChatBot.csproj"
COPY . .
WORKDIR "/src/AIChatBot"
RUN dotnet build "AIChatBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AIChatBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AIChatBot.dll"]
```

**Dosya: `docker-compose.yml`**
```yaml
version: '3.8'

services:
  aichatbot:
    build: .
    ports:
      - "5223:5223"
      - "7090:7090"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=AIChatBotDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;
      - Ollama__Endpoint=http://ollama:11434
    depends_on:
      - sqlserver
      - ollama

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Passw0rd
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama-data:/root/.ollama

volumes:
  sqlserver-data:
  ollama-data:
```

**Kullanım:**
```bash
# Build
docker-compose build

# Run
docker-compose up -d

# Logs
docker-compose logs -f

# Stop
docker-compose down
```

---

## 🚀 İleri Seviye Özellik Önerileri

### 4.1 WebSocket Support (Real-time Chat)

**NuGet Package:**
```bash
dotnet add package Microsoft.AspNetCore.SignalR
```

**Hub Sınıfı:**
```csharp
// Hubs/ChatHub.cs
using Microsoft.AspNetCore.SignalR;

namespace AIChatBot.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string sessionId, string message)
        {
            // Process message
            // Send response to all clients in session
            await Clients.Group(sessionId).SendAsync("ReceiveMessage", message);
        }

        public async Task JoinSession(string sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        }
    }
}
```

**Program.cs:**
```csharp
builder.Services.AddSignalR();

// ...

app.MapHub<ChatHub>("/chathub");
```

---

## 📊 İlerleme Takibi

### Örnek Haftalık Plan

**Hafta 1:**
- [ ] Güvenlik iyileştirmeleri
- [ ] Unit test altyapısı

**Hafta 2:**
- [ ] Integration tests
- [ ] Performance optimizations

**Hafta 3:**
- [ ] Code quality improvements
- [ ] Documentation (README, setup guide)

**Hafta 4:**
- [ ] CI/CD pipeline
- [ ] Docker support

**Hafta 5-6:**
- [ ] İleri seviye özellikler (opsiyonel)
- [ ] Final testing

---

## ✅ Checklist

### Development
- [ ] User secrets konfigürasyonu
- [ ] Input validation (FluentValidation)
- [ ] Rate limiting
- [ ] Null reference warnings fix
- [ ] Unit tests (%70+ coverage)
- [ ] Integration tests
- [ ] Performance optimizations (caching, N+1 fix)

### Documentation
- [ ] README.md
- [ ] API usage examples
- [ ] Setup guide
- [ ] Architecture diagrams

### DevOps
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Docker support
- [ ] docker-compose.yml
- [ ] Environment configuration

### Production Readiness
- [ ] HTTPS enforcement
- [ ] Security scan
- [ ] Load testing
- [ ] Monitoring setup
- [ ] Backup strategy

---

**Son Güncelleme:** 24 Aralık 2025

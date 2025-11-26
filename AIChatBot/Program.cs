using AIChatBot.Models;
using AIChatBot.Services;
using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Repository.ChatMemory;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration'dan Ollama ayarlarını oku
var ollamaSettings = builder.Configuration.GetSection("Ollama").Get<OllamaSettings>()
                     ?? new OllamaSettings();

builder.Services.AddSingleton(ollamaSettings);

// 2. Loglama
builder.Services.AddLogging(l => l.AddConsole());

// 3. Ollama Client Kaydı
try
{
    var ollamaClient = new OllamaChatClient(
        ollamaSettings.Endpoint,
        ollamaSettings.Model,
        ollamaSettings
    );

    builder.Services.AddSingleton<IChatClient>(ollamaClient);

    Console.WriteLine("========================================");
    Console.WriteLine("[INIT] ✅ Ollama Client Kaydedildi");
    Console.WriteLine($"  📍 Endpoint: {ollamaSettings.Endpoint}");
    Console.WriteLine($"  🤖 Model: {ollamaSettings.Model}");
    Console.WriteLine($"  🌡️ Temperature: {ollamaSettings.Temperature}");
    Console.WriteLine("========================================");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] ❌ Ollama hatası: {ex.Message}");
}

// ✅ 4. Tool Sınıfları (Eğer kullanılacaksa)
// builder.Services.AddScoped<GetProductInfoTool>();
// builder.Services.AddScoped<CalculateShippingTool>();
// builder.Services. AddScoped<SearchRAGTool>();

// ✅ 5. AITool Kaydı (YENİ - Microsoft.Extensions.AI 10.0)
// Şimdilik tool calling devre dışı (llama3.1 gerektirir)
var emptyTools = new List<AITool>(); // Boş tool listesi
builder.Services.AddSingleton<IEnumerable<AITool>>(emptyTools);

/* 
// Tool calling aktif etmek için (llama3.1 yüklendikten sonra):
builder.Services.AddSingleton<IEnumerable<AITool>>(sp =>
{
    var tools = new List<AITool>();
    
    // Tool 1: GetProductInfo
    var getProductInfo = AIFunctionFactory.Create(
        (int productId) =>
        {
            // Tool implementation
            return $"Ürün ID {productId} bilgileri... ";
        },
        name: "GetProductInfo",
        description: "Ürün bilgilerini getirir"
    );
    tools.Add(getProductInfo);
    
    // Tool 2: CalculateShipping
    var calculateShipping = AIFunctionFactory.Create(
        (decimal orderAmount) =>
        {
            return orderAmount >= 100
                ? "Kargo ücretsiz"
                : "Kargo: 30 TL";
        },
        name: "CalculateShipping",
        description: "Kargo ücretini hesaplar"
    );
    tools.Add(calculateShipping);
    
    return tools;
});
*/

// 6. Repository ve Servisler (ADO.NET Tabanlı)
builder.Services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
builder.Services.AddScoped<IChatMemoryRepository, ChatMemoryRepository>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<ChatService>();

// 7. Controllers ve Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 8. Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// 9. Başlatma mesajları
Console.WriteLine("========================================");
Console.WriteLine("✅ AI ChatBot API Hazır!");
Console.WriteLine("========================================");
Console.WriteLine($"🌐 HTTP:    http://localhost:5223");
Console.WriteLine($"🔒 HTTPS:   https://localhost:7090");
Console.WriteLine($"📚 Swagger: http://localhost:5223/swagger");
Console.WriteLine("========================================");

app.Run();
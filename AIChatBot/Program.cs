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

// 4. ✅ Repository ve Servisler (ADO.NET Tabanlı)
builder.Services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
builder.Services.AddScoped<IChatMemoryRepository, ChatMemoryRepository>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<ChatService>();

// 5. Controllers ve Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 6. Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// 7. Başlatma mesajları
Console.WriteLine("========================================");
Console.WriteLine("✅ AI ChatBot API Hazır!");
Console.WriteLine("========================================");
Console.WriteLine($"🌐 HTTP:    http://localhost:5223");
Console.WriteLine($"🔒 HTTPS:   https://localhost:7090");
Console.WriteLine($"📚 Swagger: http://localhost:5223/swagger");
Console.WriteLine("========================================");

app.Run();
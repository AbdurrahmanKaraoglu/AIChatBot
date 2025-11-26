using AIChatBot.Services;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// 1. Loglama
builder.Services.AddLogging(l => l.AddConsole());

// 2. Ollama Client Kaydı
// Not: Ollama'nın bilgisayarınızda "ollama run llama2" komutu ile çalıştığından emin olun.
try
{
    var ollamaClient = new OllamaChatClient("http://localhost:11434", "llama2"); // Model adını gerekirse değiştirin
    builder.Services.AddSingleton<IChatClient>(ollamaClient);
    Console.WriteLine("[INIT] Ollama client kaydedildi.");
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Ollama hatası: {ex.Message}");
}

// 3. Servisler
builder.Services.AddSingleton<ConversationMemoryService>(); // Memory singleton olmalı ki veriler silinmesin
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<ChatService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

Console.WriteLine("API Hazır: http://localhost:5000/swagger");

app.Run();
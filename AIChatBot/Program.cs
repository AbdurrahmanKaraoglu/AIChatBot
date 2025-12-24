using AIChatBot.Models;
using AIChatBot.Repository.ChatMemory;
using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Services;
using AIChatBot.Tools;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using Serilog;
using Serilog.Events;
using System.Data;

// =============================================
// Serilog Yapılandırması
// =============================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level: u3}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp: yyyy-MM-dd HH: mm:ss. fff zzz} [{Level: u3}] {Message:lj}{NewLine}{Exception}",
        shared: true
    )
    .CreateLogger();

try
{
    Log.Information("========================================");
    Log.Information("🚀 AI ChatBot API Başlatılıyor.. .");
    Log.Information("========================================");

    var builder = WebApplication.CreateBuilder(args);

    // =============================================
    // Serilog Entegrasyonu
    // =============================================
    builder.Host.UseSerilog();

    // =============================================
    // 1. Ollama Settings
    // =============================================
    var ollamaSettings = builder.Configuration.GetSection("Ollama").Get<OllamaSettings>()
                         ?? new OllamaSettings();

    builder.Services.AddSingleton(ollamaSettings);

    // =============================================
    // 2. Ollama Client Kaydı
    // =============================================
    try
    {
        var ollamaClient = new OllamaChatClient(
            ollamaSettings.Endpoint,
            ollamaSettings.Model,
            ollamaSettings
        );

        builder.Services.AddSingleton<IChatClient>(ollamaClient);

        Log.Information("========================================");
        Log.Information("[INIT] ✅ Ollama Client Kaydedildi");
        Log.Information("  📍 Endpoint: {Endpoint}", ollamaSettings.Endpoint);
        Log.Information("  🤖 Model: {Model}", ollamaSettings.Model);
        Log.Information("  🌡️ Temperature: {Temperature}", ollamaSettings.Temperature);
        Log.Information("========================================");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[INIT] ❌ Ollama Client kayıt hatası");
    }

    // =============================================
    // 3. Repository ve Servisler
    // =============================================
    builder.Services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
    builder.Services.AddScoped<IChatMemoryRepository, ChatMemoryRepository>();
    builder.Services.AddScoped<EmbeddingService>();
    builder.Services.AddScoped<RagService>();
    builder.Services.AddScoped<ChatService>();
    builder.Services.AddScoped<EmbeddingMigrationService>();
    builder.Services.AddScoped<ToolFactory>(); // ✅ YENİ:  Factory ekle


    Log.Debug("[INIT] Repository ve servisler kaydedildi");

    // =============================================
    // 4. AITool'ları Dictionary ile Kaydet - FACTORY PATTERN
    // =============================================

    // ✅ Dictionary oluştur (boş)
    var toolsDictionary = new Dictionary<string, AITool>();

    builder.Services.AddSingleton<Dictionary<string, AITool>>(sp =>
    {
        var dict = new Dictionary<string, AITool>();

        // Tool'ları buraya eklemeyin - runtime'da eklenecek
        Log.Information("[INIT] Tool dictionary hazırlandı (factory pattern)");

        return dict;
    });

    // ✅ Tool Factory Servisi Kaydet
    builder.Services.AddScoped<ToolFactory>();

    // ✅ IEnumerable<AITool> - Runtime'da oluşturulacak
    builder.Services.AddScoped<IEnumerable<AITool>>(sp =>
    {
        var factory = sp.GetRequiredService<ToolFactory>();
        return factory.CreateTools();
    });

    Log.Debug("[INIT] Tool factory kaydedildi");

    // =============================================
    // 5. Health Checks Kaydı
    // =============================================
    builder.Services.AddHealthChecks()
        .AddCheck<AIChatBot.HealthChecks.OllamaHealthCheck>(
            "ollama",
            tags: new[] { "ai", "llm" }
        )
        .AddCheck<AIChatBot.HealthChecks.EmbeddingHealthCheck>(
            "embedding",
            tags: new[] { "ai", "rag" }
        )
        .AddSqlServer(
            connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "sqlserver",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: new[] { "db", "sql" }
        );

    Log.Debug("[INIT] Health checks kaydedildi");

    // =============================================
    // 6. Controllers ve Swagger
    // =============================================
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    Log.Debug("[INIT] Controllers ve Swagger kaydedildi");

    // =============================================
    // 7. Application Build
    // =============================================
    var app = builder.Build();

    // =============================================
    // 8. Serilog HTTP Request Logging
    // =============================================
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400) return LogEventLevel.Warning;
            if (elapsed > 5000) return LogEventLevel.Warning;
            return LogEventLevel.Information;
        };
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
            diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress);
        };
    });

    // =============================================
    // 9. Middleware Pipeline
    // =============================================
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();

    // =============================================
    // 10. Health Check Endpoints
    // =============================================
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data,
                    tags = e.Value.Tags
                })
            }, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await context.Response.WriteAsync(result);
        }
    });

    app.MapHealthChecks("/health/ready");

    // =============================================
    // 11. Controllers
    // =============================================
    app.MapControllers();

    // =============================================
    // 12. Başlatma Mesajları
    // =============================================
    Log.Information("========================================");
    Log.Information("✅ AI ChatBot API Hazır!");
    Log.Information("========================================");
    Log.Information("🌐 HTTP:    http://localhost:5223");
    Log.Information("🔒 HTTPS:   https://localhost:7090");
    Log.Information("📚 Swagger: http://localhost:5223/swagger");
    Log.Information("📁 Logs:    {LogPath}", Path.Combine(Directory.GetCurrentDirectory(), "Logs"));
    Log.Information("🏥 Health:  https://localhost:7090/health");
    Log.Information("🔧 Tools:   https://localhost:7090/api/ToolTest/health");
    Log.Information("========================================");

    // =============================================
    // 13. Application Run
    // =============================================
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Uygulama başlatılamadı!");
}
finally
{
    Log.Information("🛑 Uygulama kapatılıyor...");
    Log.CloseAndFlush();
}
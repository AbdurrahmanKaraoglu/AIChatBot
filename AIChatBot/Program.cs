using AIChatBot.Models;
using AIChatBot.Services;
using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Repository.ChatMemory;
using Microsoft.Extensions.AI;
using Serilog;
using Serilog.Events;
using Microsoft.Data.SqlClient;
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
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss. fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
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

    Log.Debug("[INIT] Repository ve servisler kaydedildi");

    // =============================================
    // 4. AITool'ları Factory Pattern ile Kaydet
    // =============================================
    builder.Services.AddSingleton<IEnumerable<AITool>>(sp =>
    {
        var tools = new List<AITool>();

        try
        {
            Log.Information("[TOOLS] 🔧 Tool kaydı başlatılıyor (Factory Pattern)...");

            // ========== Tool 1: GetProductInfo ==========
            try
            {
                Log.Debug("[TOOLS] GetProductInfo factory oluşturuluyor.. .");

                var getProductInfoFunc = AIFunctionFactory.Create(
                    async (int productId) =>
                    {
                        // Her çağrıda yeni scope oluştur
                        using var scope = sp.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseRepository>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                        logger.LogInformation("[TOOL] GetProductInfo called: ProductId={ProductId}", productId);

                        try
                        {
                            var products = await repo.SearchDocuments(productId.ToString());

                            if (!products.Any())
                            {
                                logger.LogWarning("[TOOL] ProductId {ProductId} bulunamadı", productId);
                                return $"❌ Ürün ID {productId} veritabanında bulunamadı. ";
                            }

                            var product = products.First();
                            logger.LogInformation("[TOOL] ProductId {ProductId} bilgisi döndürüldü", productId);

                            return $"✅ Ürün Bilgisi:\n📦 {product.Title}\n📝 {product.Content}";
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[TOOL-ERROR] ProductId:{ProductId} getirme hatası", productId);
                            return $"❌ Ürün bilgisi alınırken hata oluştu: {ex.Message}";
                        }
                    },
                    name: "GetProductInfo",
                    description: "Ürün ID'sine göre detaylı ürün bilgilerini getirir"
                );

                tools.Add(getProductInfoFunc);
                Log.Information("[TOOLS] ✅ GetProductInfo kaydedildi (Factory)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TOOLS] ❌ GetProductInfo kayıt hatası: {Message}", ex.Message);
            }

            // ========== Tool 2: CalculateShipping ==========
            try
            {
                Log.Debug("[TOOLS] CalculateShipping factory oluşturuluyor...");

                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

                var calculateShippingFunc = AIFunctionFactory.Create(
                    async (decimal orderAmount) =>
                    {
                        using var scope = sp.CreateScope();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                        logger.LogInformation("[TOOL] CalculateShipping called: Amount={Amount}", orderAmount);

                        try
                        {
                            using (var conn = new SqlConnection(connectionString))
                            {
                                using (var cmd = new SqlCommand("sp_CalculateShipping", conn))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;
                                    cmd.Parameters.Add(new SqlParameter("@OrderAmount", orderAmount));

                                    await conn.OpenAsync();

                                    using (var reader = await cmd.ExecuteReaderAsync())
                                    {
                                        if (await reader.ReadAsync())
                                        {
                                            decimal cost = reader.GetDecimal(reader.GetOrdinal("ShippingCost"));
                                            int minDays = reader.GetInt32(reader.GetOrdinal("DeliveryDaysMin"));
                                            int maxDays = reader.GetInt32(reader.GetOrdinal("DeliveryDaysMax"));

                                            logger.LogInformation(
                                                "[TOOL] Shipping calculated: Amount={Amount}, Cost={Cost}, Delivery={MinDays}-{MaxDays} days",
                                                orderAmount, cost, minDays, maxDays
                                            );

                                            if (cost == 0)
                                                return $"✅ Kargo ücretsiz!  🎉\n📦 Teslimat süresi: {minDays}-{maxDays} iş günü. ";
                                            else
                                                return $"✅ Kargo ücreti: {cost} TL\n📦 Teslimat süresi: {minDays}-{maxDays} iş günü.";
                                        }
                                    }
                                }
                            }

                            logger.LogWarning("[TOOL] Kargo kuralı bulunamadı: Amount={Amount}", orderAmount);
                            return "❌ Kargo bilgisi bulunamadı.";
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[TOOL-ERROR] Kargo hesaplama hatası: Amount={Amount}", orderAmount);
                            return $"❌ Kargo hesaplama hatası: {ex.Message}";
                        }
                    },
                    name: "CalculateShipping",
                    description: "Sipariş tutarına göre kargo ücretini hesaplar"
                );

                tools.Add(calculateShippingFunc);
                Log.Information("[TOOLS] ✅ CalculateShipping kaydedildi (Factory)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TOOLS] ❌ CalculateShipping kayıt hatası: {Message}", ex.Message);
            }

            // ========== Tool 3: SearchRAG ==========
            try
            {
                Log.Debug("[TOOLS] SearchRAG factory oluşturuluyor...");

                var searchRagFunc = AIFunctionFactory.Create(
                    async (string query, int topK = 3) =>
                    {
                        using var scope = sp.CreateScope();
                        var ragService = scope.ServiceProvider.GetRequiredService<RagService>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                        logger.LogInformation("[TOOL] SearchRAG called: Query='{Query}', TopK={TopK}", query, topK);

                        try
                        {
                            var results = await ragService.SemanticSearchAsync(query, topK);

                            if (!results.Any())
                            {
                                logger.LogWarning("[TOOL] SearchRAG: No results for query '{Query}'", query);
                                return "❌ İlgili bilgi bulunamadı.";
                            }

                            logger.LogInformation("[TOOL] SearchRAG: {Count} results found", results.Count);

                            var response = "✅ Bulunan Bilgiler:\n\n";
                            int index = 1;

                            foreach (var doc in results)
                            {
                                var preview = doc.Content.Length > 100
                                    ? doc.Content.Substring(0, 100) + "..."
                                    : doc.Content;

                                response += $"{index}. 📄 **{doc.Title}**\n   {preview}\n\n";
                                index++;
                            }

                            return response;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[TOOL-ERROR] SearchRAG hatası: Query='{Query}'", query);
                            return $"❌ Arama hatası: {ex.Message}";
                        }
                    },
                    name: "SearchRAG",
                    description: "Bilgi bankasında semantic search yapar"
                );

                tools.Add(searchRagFunc);
                Log.Information("[TOOLS] ✅ SearchRAG kaydedildi (Factory)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TOOLS] ❌ SearchRAG kayıt hatası: {Message}", ex.Message);
            }

            // ========== Özet ==========
            Log.Information("========================================");
            Log.Information("[TOOLS] ✅ Toplam {ToolCount} Tool Kaydedildi (Factory Pattern)", tools.Count);

            if (tools.Count > 0)
            {
                Log.Information("  🔧 GetProductInfo");
                Log.Information("  🔧 CalculateShipping");
                Log.Information("  🔧 SearchRAG");
            }
            else
            {
                Log.Warning("  ⚠️ Hiç tool kaydedilemedi!");
            }

            Log.Information("========================================");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOOLS] ❌ Kritik hata: Tool kayıt süreci başarısız");
        }

        return tools;
    });

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
    // 6.  Controllers ve Swagger
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
    // 9.  Middleware Pipeline
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
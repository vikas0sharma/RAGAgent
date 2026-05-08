using Google.GenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using PersonalAssistant.Agents;
using PersonalAssistant.Data;
using PersonalAssistant.Models;
using PersonalAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

// Database - EF Core 10 with SQL Server 2025 vector support
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Google Gemini configuration
var geminiApiKey = builder.Configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey is required.");
var geminiChatModel = builder.Configuration["Gemini:ChatModel"] ?? "gemini-2.5-flash";
var geminiEmbeddingModel = builder.Configuration["Gemini:EmbeddingModel"] ?? "text-embedding-004";

// Google GenAI client
var genAiClient = new Client(apiKey: geminiApiKey);

builder.Services.AddChatClient(genAiClient.AsIChatClient(geminiChatModel));

builder.Services.AddSingleton(genAiClient);
builder.Services.Configure<GeminiEmbeddingOptions>(options => options.Model = geminiEmbeddingModel);

// Application services
builder.Services.AddHttpClient<UrlExtractor>();
builder.Services.AddSingleton<PdfExtractor>();
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<RagChatAgent>();

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Personal Assistant API");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

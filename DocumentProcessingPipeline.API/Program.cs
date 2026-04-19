using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Application.OCR;
using DocumentProcessingPipeline.Application.Services;
using DocumentProcessingPipeline.Infrastructure.Context;
using DocumentProcessingPipeline.Infrastructure.Kafka;
using DocumentProcessingPipeline.Infrastructure.Repositories;
using DocumentProcessingPipeline.Infrastructure.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Core services
    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<OcrService>();
    builder.Services.AddScoped<ITagDetectionService, TagDetectionService>();

    // Database
    builder.Services.AddSingleton<MongoDbContext>(sp =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<MongoDbContext>>();

        var connectionString = configuration["MongoDb:ConnectionString"];
        var dbName = configuration["MongoDb:Database"];

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dbName))
        {
            throw new InvalidOperationException(
                "MongoDB configuration is missing in appsettings.json");
        }

        return new MongoDbContext(connectionString, dbName, logger);
    });

    // Kafka Event Bus
    var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

    builder.Services.AddSingleton<IEventProducer>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<KafkaEventProducer>>();
        return new KafkaEventProducer(kafkaBootstrapServers, logger);
    });

    builder.Services.AddSingleton<IEventConsumer>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<KafkaEventConsumer>>();
        return new KafkaEventConsumer(kafkaBootstrapServers, logger);
    });

    // Background worker
    builder.Services.AddHostedService<DocumentProcessingWorker>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
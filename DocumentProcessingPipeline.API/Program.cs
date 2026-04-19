using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Application.OCR;
using DocumentProcessingPipeline.Application.Services;
using DocumentProcessingPipeline.Infrastructure.Context;
using DocumentProcessingPipeline.Infrastructure.Repositories;
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

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Register services
    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<OcrService>();
    builder.Services.AddScoped<ITagDetectionService, TagDetectionService>();

    // Register MongoDbContext with proper error handling
    builder.Services.AddSingleton<MongoDbContext>(sp =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<MongoDbContext>>();

        var connectionString = configuration["MongoDb:ConnectionString"];
        var dbName = configuration["MongoDb:Database"];

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dbName))
        {
            throw new InvalidOperationException(
                "MongoDB connection string and database name must be configured in appsettings.json");
        }

        return new MongoDbContext(connectionString, dbName, logger);
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
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
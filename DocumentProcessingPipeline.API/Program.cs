using DocumentProcessingPipeline.Core.Interfaces;
using DocumentProcessingPipeline.Infrastructure.Persistence;
using DocumentProcessingPipeline.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

builder.Services.AddSingleton<MongoDbContext>(sp =>
{
    var connectionString = builder.Configuration["MongoDb:ConnectionString"];
    var dbName = builder.Configuration["MongoDb:Database"];

    return new MongoDbContext(connectionString, dbName);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using LadleMetallurgyService.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Steel Mill Ladle Metallurgy Service",
        Version = "v1.0",
        Description = "Secondary refining service for rebar chemistry optimization and quality control"
    });
});

// Add Azure Cosmos DB
var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDB") ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
builder.Services.AddSingleton(serviceProvider =>
{
    return new CosmosClient(cosmosConnectionString, new CosmosClientOptions
    {
        ApplicationName = "SteelMillLadleMetallurgy",
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
});

// Add custom services
builder.Services.AddScoped<LadleMetallurgyDataService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCosmosDb(cosmosConnectionString, "SteelMillRebarDB");

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ladle Metallurgy Service v1.0");
        c.RoutePrefix = string.Empty;
    });
}

app.UseRouting();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Initialize database and containers
try
{
    var cosmosClient = app.Services.GetRequiredService<CosmosClient>();
    var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync("SteelMillRebarDB");
    var database = databaseResponse.Database;

    // Create containers with appropriate partition keys
    await database.CreateContainerIfNotExistsAsync("LadleStatus", "/ladleId");
    await database.CreateContainerIfNotExistsAsync("LadleMetallurgyTelemetry", "/ladleId");
    await database.CreateContainerIfNotExistsAsync("LadleMetallurgyAlerts", "/ladleId");
    await database.CreateContainerIfNotExistsAsync("LadleMetallurgyPerformance", "/ladleId");
    await database.CreateContainerIfNotExistsAsync("TreatmentRecipes", "/id");

    app.Logger.LogInformation("Cosmos DB containers initialized successfully");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to initialize Cosmos DB containers");
}

app.Logger.LogInformation("🔥 Steel Mill Ladle Metallurgy Service started successfully");
app.Logger.LogInformation("🧪 Secondary refining for rebar chemistry optimization");
app.Logger.LogInformation("📊 Real-time composition monitoring and quality control");

app.Run();
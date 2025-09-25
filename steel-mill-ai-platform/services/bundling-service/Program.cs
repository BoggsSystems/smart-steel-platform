using Azure.Messaging.EventHubs.Consumer;
using PackagingService.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AzureAd:Authority"];
        options.Audience = builder.Configuration["AzureAd:Audience"];
    });

builder.Services.AddAuthorization();

// Cosmos DB
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var connectionString = builder.Configuration["CosmosDb:ConnectionString"];
    return new CosmosClient(connectionString);
});

// Services
builder.Services.AddSingleton<IPackagingDataService, PackagingDataService>();

// Azure clients
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddEventHubConsumerClient(
        EventHubConsumerClient.DefaultConsumerGroupName,
        builder.Configuration["EventHub:ConnectionString"],
        builder.Configuration["EventHub:EventHubName"]);
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCosmosDb(
        builder.Configuration["CosmosDb:ConnectionString"]!,
        database: builder.Configuration["CosmosDb:DatabaseName"]!,
        name: "cosmosdb",
        failureStatus: HealthStatus.Degraded)
    .AddAzureEventHubs(
        builder.Configuration["EventHub:ConnectionString"]!,
        builder.Configuration["EventHub:EventHubName"]!,
        name: "eventhub");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
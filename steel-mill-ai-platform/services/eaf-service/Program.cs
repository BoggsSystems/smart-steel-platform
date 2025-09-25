using Azure.Messaging.EventHubs.Consumer;
using FurnaceService.Services;
using Microsoft.Azure.Cosmos;
using SteelMillShared.Extensions;
using SteelMillShared.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add shared Steel Mill services
builder.Services.AddSteelMillAuthentication(builder.Configuration);
builder.Services.AddSteelMillHealthChecks(builder.Configuration);
builder.Services.AddSteelMillTelemetry(builder.Configuration);

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var connectionString = builder.Configuration["CosmosDb:ConnectionString"];
    return new CosmosClient(connectionString);
});

builder.Services.AddSingleton<IEventProcessorService, EventProcessorService>();
builder.Services.AddSingleton<IFurnaceDataService, FurnaceDataService>();
builder.Services.AddHostedService<EventHubConsumerService>();

builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddEventHubConsumerClient(
        EventHubConsumerClient.DefaultConsumerGroupName,
        builder.Configuration["EventHub:ConnectionString"],
        builder.Configuration["EventHub:EventHubName"]);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.Run();
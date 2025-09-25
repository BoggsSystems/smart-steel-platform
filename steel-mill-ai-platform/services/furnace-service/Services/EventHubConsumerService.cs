using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;

namespace FurnaceService.Services;

public class EventHubConsumerService : BackgroundService
{
    private readonly EventProcessorClient _processorClient;
    private readonly IEventProcessorService _eventProcessor;
    private readonly ILogger<EventHubConsumerService> _logger;

    public EventHubConsumerService(
        IConfiguration configuration,
        IEventProcessorService eventProcessor,
        ILogger<EventHubConsumerService> logger)
    {
        _eventProcessor = eventProcessor;
        _logger = logger;

        var connectionString = configuration["EventHub:ConnectionString"];
        var eventHubName = configuration["EventHub:EventHubName"];
        var storageConnectionString = configuration["Storage:ConnectionString"];
        var blobContainerName = configuration["Storage:CheckpointContainer"];

        var storageClient = new BlobContainerClient(storageConnectionString, blobContainerName);
        
        _processorClient = new EventProcessorClient(
            storageClient,
            EventHubConsumerClient.DefaultConsumerGroupName,
            connectionString,
            eventHubName);

        _processorClient.ProcessEventAsync += ProcessEventHandler;
        _processorClient.ProcessErrorAsync += ProcessErrorHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _processorClient.StartProcessingAsync(stoppingToken);
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
        
        await _processorClient.StopProcessingAsync();
    }

    private async Task ProcessEventHandler(ProcessEventArgs args)
    {
        if (args.CancellationToken.IsCancellationRequested)
            return;

        try
        {
            await _eventProcessor.ProcessEventAsync(args.Data);
            await args.UpdateCheckpointAsync(args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event");
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, $"Error in event processing: {args.PartitionId}");
        return Task.CompletedTask;
    }
}
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Text.Json;

namespace SteelMillShared.Services;

public interface IDataArchivalService
{
    Task ArchiveOldTelemetryAsync(DateTime olderThan);
    Task ArchiveOldAlertsAsync(DateTime olderThan);
    Task CompressAndBackupDataAsync(string containerName, DateTime date);
    Task<DataArchivalStatus> GetArchivalStatusAsync();
}

public class DataArchivalService : IDataArchivalService
{
    private readonly CosmosClient _cosmosClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataArchivalService> _logger;

    public DataArchivalService(
        CosmosClient cosmosClient,
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<DataArchivalService> logger)
    {
        _cosmosClient = cosmosClient;
        _blobServiceClient = blobServiceClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ArchiveOldTelemetryAsync(DateTime olderThan)
    {
        _logger.LogInformation("Starting telemetry archival for data older than {Date}", olderThan);

        var database = _cosmosClient.GetDatabase(_configuration["CosmosDb:DatabaseName"]);
        var telemetryContainer = database.GetContainer(_configuration["CosmosDb:TelemetryContainer"]);
        var archiveContainer = _blobServiceClient.GetBlobContainerClient("telemetry-archive");

        await archiveContainer.CreateIfNotExistsAsync();

        // Query old telemetry data
        var queryText = "SELECT * FROM c WHERE c.Timestamp < @olderThan";
        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@olderThan", olderThan);

        var batchCount = 0;
        var totalRecords = 0;
        var currentBatch = new List<dynamic>();

        using var iterator = telemetryContainer.GetItemQueryIterator<dynamic>(queryDefinition);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            
            foreach (var item in response)
            {
                currentBatch.Add(item);
                totalRecords++;

                // Archive in batches of 1000
                if (currentBatch.Count >= 1000)
                {
                    await ArchiveBatchToBlob(currentBatch, archiveContainer, "telemetry", batchCount);
                    await DeleteBatchFromCosmos(currentBatch, telemetryContainer);
                    
                    currentBatch.Clear();
                    batchCount++;
                }
            }
        }

        // Archive remaining records
        if (currentBatch.Count > 0)
        {
            await ArchiveBatchToBlob(currentBatch, archiveContainer, "telemetry", batchCount);
            await DeleteBatchFromCosmos(currentBatch, telemetryContainer);
        }

        _logger.LogInformation(
            "Completed telemetry archival: {TotalRecords} records in {BatchCount} batches",
            totalRecords, batchCount + 1);
    }

    public async Task ArchiveOldAlertsAsync(DateTime olderThan)
    {
        _logger.LogInformation("Starting alerts archival for data older than {Date}", olderThan);

        var database = _cosmosClient.GetDatabase(_configuration["CosmosDb:DatabaseName"]);
        var alertsContainer = database.GetContainer(_configuration["CosmosDb:AlertsContainer"]);
        var archiveContainer = _blobServiceClient.GetBlobContainerClient("alerts-archive");

        await archiveContainer.CreateIfNotExistsAsync();

        // Query resolved alerts older than specified date
        var queryText = "SELECT * FROM c WHERE c.CreatedAt < @olderThan AND c.IsResolved = true";
        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@olderThan", olderThan);

        var batchCount = 0;
        var totalRecords = 0;
        var currentBatch = new List<dynamic>();

        using var iterator = alertsContainer.GetItemQueryIterator<dynamic>(queryDefinition);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            
            foreach (var item in response)
            {
                currentBatch.Add(item);
                totalRecords++;

                if (currentBatch.Count >= 500) // Smaller batches for alerts
                {
                    await ArchiveBatchToBlob(currentBatch, archiveContainer, "alerts", batchCount);
                    await DeleteBatchFromCosmos(currentBatch, alertsContainer);
                    
                    currentBatch.Clear();
                    batchCount++;
                }
            }
        }

        if (currentBatch.Count > 0)
        {
            await ArchiveBatchToBlob(currentBatch, archiveContainer, "alerts", batchCount);
            await DeleteBatchFromCosmos(currentBatch, alertsContainer);
        }

        _logger.LogInformation(
            "Completed alerts archival: {TotalRecords} records in {BatchCount} batches",
            totalRecords, batchCount + 1);
    }

    public async Task CompressAndBackupDataAsync(string containerName, DateTime date)
    {
        _logger.LogInformation("Starting compressed backup for {Container} on {Date}", containerName, date);

        var sourceContainer = _blobServiceClient.GetBlobContainerClient(containerName);
        var backupContainer = _blobServiceClient.GetBlobContainerClient($"{containerName}-backup");
        
        await backupContainer.CreateIfNotExistsAsync();

        var datePrefix = date.ToString("yyyy/MM/dd");
        var backupFileName = $"backup-{containerName}-{date:yyyyMMdd}.json.gz";

        // Get all blobs for the specified date
        var blobs = new List<dynamic>();
        
        await foreach (var blobItem in sourceContainer.GetBlobsAsync(prefix: datePrefix))
        {
            var blobClient = sourceContainer.GetBlobClient(blobItem.Name);
            var content = await blobClient.DownloadContentAsync();
            
            var blobData = JsonSerializer.Deserialize<dynamic>(content.Value.Content);
            blobs.Add(blobData);
        }

        if (blobs.Count > 0)
        {
            // Compress and upload backup
            var compressedData = await CompressDataAsync(blobs);
            var backupBlobClient = backupContainer.GetBlobClient(backupFileName);
            
            await backupBlobClient.UploadAsync(
                new MemoryStream(compressedData),
                overwrite: true);

            _logger.LogInformation(
                "Created compressed backup: {FileName} with {RecordCount} records",
                backupFileName, blobs.Count);
        }
    }

    public async Task<DataArchivalStatus> GetArchivalStatusAsync()
    {
        var status = new DataArchivalStatus
        {
            LastArchivalRun = await GetLastArchivalRunTimeAsync(),
            TelemetryRecordsCount = await GetRecordCountAsync("TelemetryContainer"),
            AlertsRecordsCount = await GetRecordCountAsync("AlertsContainer"),
            ArchiveContainerSizeBytes = await GetContainerSizeAsync("telemetry-archive"),
            BackupContainerSizeBytes = await GetContainerSizeAsync("telemetry-archive-backup")
        };

        // Determine if archival is needed (more than 1M records or data older than 90 days)
        var oldestRecord = await GetOldestRecordDateAsync();
        status.ArchivalRecommended = status.TelemetryRecordsCount > 1_000_000 || 
                                   (oldestRecord.HasValue && oldestRecord < DateTime.UtcNow.AddDays(-90));

        return status;
    }

    private async Task ArchiveBatchToBlob(
        List<dynamic> batch,
        BlobContainerClient container,
        string prefix,
        int batchNumber)
    {
        var fileName = $"{prefix}/batch-{batchNumber:D6}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        var json = JsonSerializer.Serialize(batch, new JsonSerializerOptions { WriteIndented = false });
        
        var blobClient = container.GetBlobClient(fileName);
        await blobClient.UploadAsync(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
            overwrite: true);

        _logger.LogDebug("Archived batch {BatchNumber} to {FileName}", batchNumber, fileName);
    }

    private async Task DeleteBatchFromCosmos(List<dynamic> batch, Container container)
    {
        var deleteTasks = new List<Task>();

        foreach (dynamic item in batch)
        {
            string id = item.id?.ToString() ?? item.Id?.ToString() ?? "";
            string partitionKey = GetPartitionKeyValue(item);

            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(partitionKey))
            {
                deleteTasks.Add(container.DeleteItemAsync<dynamic>(id, new PartitionKey(partitionKey)));
            }
        }

        await Task.WhenAll(deleteTasks);
        _logger.LogDebug("Deleted {Count} items from Cosmos DB", batch.Count);
    }

    private string GetPartitionKeyValue(dynamic item)
    {
        // Try common partition key field names
        return item.DeviceId?.ToString() ??
               item.deviceId?.ToString() ??
               item.MillId?.ToString() ??
               item.FurnaceId?.ToString() ?? "";
    }

    private async Task<byte[]> CompressDataAsync(List<dynamic> data)
    {
        var json = JsonSerializer.Serialize(data);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        
        using var output = new MemoryStream();
        using var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal);
        
        await gzip.WriteAsync(bytes);
        await gzip.FlushAsync();
        
        return output.ToArray();
    }

    private async Task<DateTime?> GetLastArchivalRunTimeAsync()
    {
        try
        {
            var metadataContainer = _blobServiceClient.GetBlobContainerClient("archival-metadata");
            var blobClient = metadataContainer.GetBlobClient("last-run.json");
            
            if (await blobClient.ExistsAsync())
            {
                var content = await blobClient.DownloadContentAsync();
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(content.Value.Content);
                
                if (metadata.TryGetValue("lastRun", out var lastRunObj))
                {
                    return DateTime.Parse(lastRunObj.ToString()!);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve last archival run time");
        }

        return null;
    }

    private async Task<long> GetRecordCountAsync(string containerName)
    {
        try
        {
            var database = _cosmosClient.GetDatabase(_configuration["CosmosDb:DatabaseName"]);
            var container = database.GetContainer(containerName);
            
            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            using var iterator = container.GetItemQueryIterator<int>(query);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting record count for {Container}", containerName);
        }

        return 0;
    }

    private async Task<long> GetContainerSizeAsync(string containerName)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            long totalSize = 0;

            await foreach (var blobItem in container.GetBlobsAsync())
            {
                totalSize += blobItem.Properties.ContentLength ?? 0;
            }

            return totalSize;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating container size for {Container}", containerName);
            return 0;
        }
    }

    private async Task<DateTime?> GetOldestRecordDateAsync()
    {
        try
        {
            var database = _cosmosClient.GetDatabase(_configuration["CosmosDb:DatabaseName"]);
            var container = database.GetContainer(_configuration["CosmosDb:TelemetryContainer"]);
            
            var query = new QueryDefinition("SELECT TOP 1 c.Timestamp FROM c ORDER BY c.Timestamp ASC");
            using var iterator = container.GetItemQueryIterator<dynamic>(query);
            
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var oldest = response.FirstOrDefault();
                
                if (oldest?.Timestamp != null)
                {
                    return DateTime.Parse(oldest.Timestamp.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting oldest record date");
        }

        return null;
    }
}

public class DataArchivalStatus
{
    public DateTime? LastArchivalRun { get; set; }
    public long TelemetryRecordsCount { get; set; }
    public long AlertsRecordsCount { get; set; }
    public long ArchiveContainerSizeBytes { get; set; }
    public long BackupContainerSizeBytes { get; set; }
    public bool ArchivalRecommended { get; set; }
    public string? RecommendationReason { get; set; }
}
# Philiprehberger.BatchProcessor

[![CI](https://github.com/philiprehberger/dotnet-batch-processor/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-batch-processor/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.BatchProcessor.svg)](https://www.nuget.org/packages/Philiprehberger.BatchProcessor)
[![GitHub release](https://img.shields.io/github/v/release/philiprehberger/dotnet-batch-processor)](https://github.com/philiprehberger/dotnet-batch-processor/releases)
[![Last updated](https://img.shields.io/github/last-commit/philiprehberger/dotnet-batch-processor)](https://github.com/philiprehberger/dotnet-batch-processor/commits/main)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-batch-processor)](LICENSE)
[![Bug Reports](https://img.shields.io/github/issues/philiprehberger/dotnet-batch-processor/bug)](https://github.com/philiprehberger/dotnet-batch-processor/issues?q=is%3Aissue+is%3Aopen+label%3Abug)
[![Feature Requests](https://img.shields.io/github/issues/philiprehberger/dotnet-batch-processor/enhancement)](https://github.com/philiprehberger/dotnet-batch-processor/issues?q=is%3Aissue+is%3Aopen+label%3Aenhancement)
[![Sponsor](https://img.shields.io/badge/sponsor-GitHub%20Sponsors-ec6cb9)](https://github.com/sponsors/philiprehberger)

Process large collections in configurable batches with progress reporting, error handling, and async execution.

## Installation

```bash
dotnet add package Philiprehberger.BatchProcessor
```

## Usage

```csharp
using Philiprehberger.BatchProcessor;

var items = Enumerable.Range(1, 1000).ToList();

var result = await BatchProcessor.Process(items, batchSize: 100, async batch =>
{
    await ProcessItemsAsync(batch);
});

Console.WriteLine($"Processed {result.SuccessCount} items in {result.TotalDuration.TotalSeconds}s");
```

### Progress Reporting

```csharp
using Philiprehberger.BatchProcessor;

var items = Enumerable.Range(1, 500).ToList();

var result = await BatchProcessor.Process(items, batchSize: 50, async batch =>
{
    await SendToApiAsync(batch);
}, new BatchOptions
{
    OnProgress = progress =>
    {
        Console.WriteLine($"Progress: {progress.Percent:F1}% ({progress.ProcessedCount}/{progress.TotalCount})");
    }
});
```

### Parallel Execution with Error Handling

```csharp
using Philiprehberger.BatchProcessor;

var items = Enumerable.Range(1, 1000).ToList();

var result = await BatchProcessor.Process(items, batchSize: 50, async batch =>
{
    await SendToApiAsync(batch);
}, new BatchOptions
{
    MaxDegreeOfParallelism = 4,
    OnBatchError = BatchErrorHandling.Skip,
    RetryCount = 2
});

Console.WriteLine($"Success: {result.SuccessCount}, Failures: {result.FailureCount}");
Console.WriteLine($"Errors: {result.Errors.Count}");
```

### Per-Batch Timeout

```csharp
using Philiprehberger.BatchProcessor;

var items = Enumerable.Range(1, 1000).ToList();

var result = await BatchProcessor.Process(items, batchSize: 100, async batch =>
{
    await SendToSlowServiceAsync(batch);
}, new BatchOptions
{
    BatchTimeout = TimeSpan.FromSeconds(30),
    OnBatchError = BatchErrorHandling.Skip
});
```

### Per-Item Error Tracking

```csharp
using Philiprehberger.BatchProcessor;

var items = Enumerable.Range(1, 100).ToList();

var result = await BatchProcessor.ProcessAsync(items, batchSize: 10, async batch =>
{
    await ProcessBatchAsync(batch);
});

Console.WriteLine($"Succeeded: {result.SucceededCount}, Failed: {result.FailedCount}");

foreach (var failure in result.Failures)
{
    Console.WriteLine($"Item {failure.Item} failed: {failure.Exception?.Message}");
}
```

### Batch Completed Callback

```csharp
using Philiprehberger.BatchProcessor;

var items = Enumerable.Range(1, 500).ToList();

var result = await BatchProcessor.Process(items, batchSize: 50, async batch =>
{
    await ProcessBatchAsync(batch);
}, new BatchOptions
{
    OnBatchCompleted = e =>
    {
        Console.WriteLine($"Batch {e.BatchIndex}: {e.SuccessCount} ok, {e.FailureCount} failed in {e.Elapsed.TotalMilliseconds}ms");
    }
});
```

### Cancellation Token

```csharp
using Philiprehberger.BatchProcessor;

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var items = Enumerable.Range(1, 10000).ToList();

var result = await BatchProcessor.Process(items, batchSize: 100, async batch =>
{
    await ProcessBatchAsync(batch);
}, cancellationToken: cts.Token);
```

## API

### BatchProcessor

| Method | Description |
|--------|-------------|
| `Process<T>(items, batchSize, processor, options?, cancellationToken?)` | Process items in batches asynchronously. Returns a `BatchResult`. |
| `ProcessAsync<T>(items, batchSize, processor, options?, cancellationToken?)` | Process items in batches with per-item error tracking. Returns a `BatchResult<T>`. |

### BatchOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxDegreeOfParallelism` | `int` | `1` | Maximum number of batches to process concurrently. |
| `OnProgress` | `Action<BatchProgress>?` | `null` | Callback invoked after each batch completes. |
| `OnBatchError` | `BatchErrorHandling` | `Abort` | Error handling strategy: `Abort` or `Skip`. |
| `RetryCount` | `int` | `0` | Number of times to retry a failed batch. |
| `BatchTimeout` | `TimeSpan?` | `null` | Timeout per batch. Throws `TimeoutException` if exceeded. |
| `OnBatchCompleted` | `Action<BatchCompletedEventArgs>?` | `null` | Callback with batch index, item count, elapsed time, and success/failure counts. |

### BatchProgress

| Property | Type | Description |
|----------|------|-------------|
| `ProcessedCount` | `int` | Number of items processed so far. |
| `TotalCount` | `int` | Total number of items. |
| `CurrentBatch` | `int` | Current batch number (1-based). |
| `TotalBatches` | `int` | Total number of batches. |
| `Percent` | `double` | Completion percentage (0-100). |

### BatchResult

| Property | Type | Description |
|----------|------|-------------|
| `SuccessCount` | `int` | Number of successfully processed items. |
| `FailureCount` | `int` | Number of items in failed batches. |
| `TotalDuration` | `TimeSpan` | Total processing duration. |
| `Errors` | `IReadOnlyList<BatchError>` | List of batch errors. |

### BatchResult\<T\>

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `IReadOnlyList<BatchItemResult<T>>` | Per-item results for every processed item. |
| `SucceededCount` | `int` | Number of items that succeeded. |
| `FailedCount` | `int` | Number of items that failed. |
| `Failures` | `IReadOnlyList<BatchItemResult<T>>` | Per-item results for failed items only. |
| `TotalDuration` | `TimeSpan` | Total processing duration. |
| `Errors` | `IReadOnlyList<BatchError>` | List of batch-level errors. |

### BatchItemResult\<T\>

| Property | Type | Description |
|----------|------|-------------|
| `Item` | `T` | The item that was processed. |
| `Success` | `bool` | Whether the item was processed successfully. |
| `Exception` | `Exception?` | The exception that occurred, or null if successful. |

### BatchCompletedEventArgs

| Property | Type | Description |
|----------|------|-------------|
| `BatchIndex` | `int` | Zero-based index of the completed batch. |
| `ItemCount` | `int` | Number of items in the batch. |
| `Elapsed` | `TimeSpan` | Time spent processing the batch. |
| `SuccessCount` | `int` | Number of items that succeeded in this batch. |
| `FailureCount` | `int` | Number of items that failed in this batch. |

### BatchError

| Property | Type | Description |
|----------|------|-------------|
| `BatchIndex` | `int` | Zero-based index of the failed batch. |
| `Exception` | `Exception` | The exception that occurred. |

## Development

```bash
dotnet build src/Philiprehberger.BatchProcessor.csproj --configuration Release
```

## Support

If you find this package useful, consider giving it a star on GitHub — it helps motivate continued maintenance and development.

[![LinkedIn](https://img.shields.io/badge/Philip%20Rehberger-LinkedIn-0A66C2?logo=linkedin)](https://www.linkedin.com/in/philiprehberger)
[![More packages](https://img.shields.io/badge/more-open%20source%20packages-blue)](https://philiprehberger.com/open-source-packages)

## License

[MIT](LICENSE)

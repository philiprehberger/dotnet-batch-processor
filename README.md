# Philiprehberger.BatchProcessor

[![CI](https://github.com/philiprehberger/dotnet-batch-processor/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-batch-processor/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.BatchProcessor.svg)](https://www.nuget.org/packages/Philiprehberger.BatchProcessor)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-batch-processor)](LICENSE)
[![Sponsor](https://img.shields.io/badge/sponsor-GitHub%20Sponsors-ec6cb9)](https://github.com/sponsors/philiprehberger)

Process large collections in configurable batches with progress reporting, error handling, and async execution.

## Installation

```bash
dotnet add package Philiprehberger.BatchProcessor
```

## Usage

### Basic Batch Processing

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

## API

### BatchProcessor

| Method | Description |
|--------|-------------|
| `Process<T>(items, batchSize, processor, options?)` | Process items in batches asynchronously. |

### BatchOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxDegreeOfParallelism` | `int` | `1` | Maximum number of batches to process concurrently. |
| `OnProgress` | `Action<BatchProgress>?` | `null` | Callback invoked after each batch completes. |
| `OnBatchError` | `BatchErrorHandling` | `Abort` | Error handling strategy: `Abort` or `Skip`. |
| `RetryCount` | `int` | `0` | Number of times to retry a failed batch. |

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

### BatchError

| Property | Type | Description |
|----------|------|-------------|
| `BatchIndex` | `int` | Zero-based index of the failed batch. |
| `Exception` | `Exception` | The exception that occurred. |

## Development

```bash
dotnet build src/Philiprehberger.BatchProcessor.csproj --configuration Release
```

## License

[MIT](LICENSE)

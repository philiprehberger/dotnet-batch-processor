namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Configuration options for batch processing.
/// </summary>
/// <param name="MaxDegreeOfParallelism">
/// Maximum number of batches to process concurrently. Defaults to 1 (sequential).
/// </param>
/// <param name="OnProgress">
/// Optional callback invoked after each batch completes with progress information.
/// </param>
/// <param name="OnBatchError">
/// Error handling strategy for failed batches. Defaults to <see cref="BatchErrorHandling.Abort"/>.
/// </param>
/// <param name="RetryCount">
/// Number of times to retry a failed batch before reporting it as an error. Defaults to 0.
/// </param>
public sealed record BatchOptions(
    int MaxDegreeOfParallelism = 1,
    Action<BatchProgress>? OnProgress = null,
    BatchErrorHandling OnBatchError = BatchErrorHandling.Abort,
    int RetryCount = 0);

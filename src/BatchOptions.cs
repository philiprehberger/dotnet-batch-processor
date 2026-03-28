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
/// <param name="BatchTimeout">
/// Optional timeout applied to each individual batch. When exceeded, a <see cref="TimeoutException"/> is thrown for that batch.
/// </param>
/// <param name="OnBatchCompleted">
/// Optional callback invoked after each batch completes with batch-level summary information including
/// index, item count, elapsed time, and success/failure counts.
/// </param>
public sealed record BatchOptions(
    int MaxDegreeOfParallelism = 1,
    Action<BatchProgress>? OnProgress = null,
    BatchErrorHandling OnBatchError = BatchErrorHandling.Abort,
    int RetryCount = 0,
    TimeSpan? BatchTimeout = null,
    Action<BatchCompletedEventArgs>? OnBatchCompleted = null);

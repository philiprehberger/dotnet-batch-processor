namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Represents the result of a batch processing operation.
/// </summary>
/// <param name="SuccessCount">The number of items in successfully processed batches.</param>
/// <param name="FailureCount">The number of items in failed batches.</param>
/// <param name="TotalDuration">The total duration of the processing operation.</param>
/// <param name="Errors">The list of errors that occurred during processing.</param>
public sealed record BatchResult(
    int SuccessCount,
    int FailureCount,
    TimeSpan TotalDuration,
    IReadOnlyList<BatchError> Errors);

/// <summary>
/// Represents the result of a batch processing operation with per-item error tracking.
/// </summary>
/// <typeparam name="T">The type of items that were processed.</typeparam>
/// <param name="Items">The per-item results for every item in the batch operation.</param>
/// <param name="SucceededCount">The total number of items that were processed successfully.</param>
/// <param name="FailedCount">The total number of items that failed processing.</param>
/// <param name="Failures">The list of per-item results for items that failed processing.</param>
/// <param name="TotalDuration">The total duration of the processing operation.</param>
/// <param name="Errors">The list of batch-level errors that occurred during processing.</param>
public sealed record BatchResult<T>(
    IReadOnlyList<BatchItemResult<T>> Items,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<BatchItemResult<T>> Failures,
    TimeSpan TotalDuration,
    IReadOnlyList<BatchError> Errors);

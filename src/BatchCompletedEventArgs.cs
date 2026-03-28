namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Contains information about a completed batch, provided to the <see cref="BatchOptions.OnBatchCompleted"/> callback.
/// </summary>
/// <param name="BatchIndex">The zero-based index of the completed batch.</param>
/// <param name="ItemCount">The number of items in the completed batch.</param>
/// <param name="Elapsed">The time elapsed processing this batch.</param>
/// <param name="SuccessCount">The number of items that succeeded in this batch.</param>
/// <param name="FailureCount">The number of items that failed in this batch.</param>
public sealed record BatchCompletedEventArgs(
    int BatchIndex,
    int ItemCount,
    TimeSpan Elapsed,
    int SuccessCount,
    int FailureCount);

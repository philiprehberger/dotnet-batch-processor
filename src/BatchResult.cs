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

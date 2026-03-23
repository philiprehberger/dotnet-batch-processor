namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Represents the current progress of a batch processing operation.
/// </summary>
/// <param name="ProcessedCount">The total number of items processed so far.</param>
/// <param name="TotalCount">The total number of items to process.</param>
/// <param name="CurrentBatch">The current batch number (1-based).</param>
/// <param name="TotalBatches">The total number of batches.</param>
/// <param name="Percent">The completion percentage (0-100).</param>
public sealed record BatchProgress(
    int ProcessedCount,
    int TotalCount,
    int CurrentBatch,
    int TotalBatches,
    double Percent);

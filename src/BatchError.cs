namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Represents an error that occurred during batch processing.
/// </summary>
/// <param name="BatchIndex">The zero-based index of the batch that failed.</param>
/// <param name="Exception">The exception that caused the batch to fail.</param>
public sealed record BatchError(
    int BatchIndex,
    Exception Exception);

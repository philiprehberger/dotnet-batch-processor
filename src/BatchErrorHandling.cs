namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Specifies how errors during batch processing should be handled.
/// </summary>
public enum BatchErrorHandling
{
    /// <summary>
    /// Abort processing immediately when a batch fails.
    /// </summary>
    Abort,

    /// <summary>
    /// Skip the failed batch and continue processing remaining batches.
    /// </summary>
    Skip
}

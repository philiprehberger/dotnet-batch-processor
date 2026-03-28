namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Represents the result of processing a single item in a batch.
/// </summary>
/// <typeparam name="T">The type of the item that was processed.</typeparam>
/// <param name="Item">The item that was processed.</param>
/// <param name="Success">Whether the item was processed successfully.</param>
/// <param name="Exception">The exception that occurred during processing, or null if successful.</param>
public sealed record BatchItemResult<T>(
    T Item,
    bool Success,
    Exception? Exception);

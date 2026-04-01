namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Configuration options for adaptive batch sizing that adjusts batch sizes
/// based on measured throughput.
/// </summary>
/// <param name="MinBatchSize">
/// The minimum batch size. Must be at least 1. Defaults to 1.
/// </param>
/// <param name="MaxBatchSize">
/// The maximum batch size. Must be greater than or equal to <see cref="MinBatchSize"/>. Defaults to 1000.
/// </param>
/// <param name="TargetThroughput">
/// The target throughput in items per second. The batch size will be adjusted
/// to try to meet this target. Must be greater than zero. Defaults to 100.
/// </param>
public sealed record AdaptiveBatchOptions(
    int MinBatchSize = 1,
    int MaxBatchSize = 1000,
    double TargetThroughput = 100.0);

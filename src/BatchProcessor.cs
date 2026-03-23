using System.Diagnostics;

namespace Philiprehberger.BatchProcessor;

/// <summary>
/// Processes large collections in configurable batches with progress reporting,
/// error handling, and async execution support.
/// </summary>
public static class BatchProcessor
{
    /// <summary>
    /// Processes items in batches asynchronously with configurable parallelism, progress reporting,
    /// and error handling.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="items">The collection of items to process.</param>
    /// <param name="batchSize">The number of items per batch. Must be greater than zero.</param>
    /// <param name="processor">
    /// An async function that processes a batch of items. Receives a read-only list of items in the batch.
    /// </param>
    /// <param name="options">Optional configuration for parallelism, progress, and error handling.</param>
    /// <returns>A <see cref="BatchResult"/> summarizing the processing outcome.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or <paramref name="processor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
    public static async Task<BatchResult> Process<T>(
        IEnumerable<T> items,
        int batchSize,
        Func<IReadOnlyList<T>, Task> processor,
        BatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(processor);

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be at least 1.");
        }

        options ??= new BatchOptions();
        var stopwatch = Stopwatch.StartNew();

        var materializedItems = items as IReadOnlyList<T> ?? items.ToList();
        var totalCount = materializedItems.Count;

        if (totalCount == 0)
        {
            stopwatch.Stop();
            return new BatchResult(0, 0, stopwatch.Elapsed, []);
        }

        var batches = CreateBatches(materializedItems, batchSize);
        var totalBatches = batches.Count;

        var errors = new List<BatchError>();
        var successCount = 0;
        var failureCount = 0;
        var processedCount = 0;
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
        var tasks = new List<Task>();
        var aborted = false;

        for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            if (aborted)
            {
                break;
            }

            var currentBatch = batches[batchIndex];
            var currentIndex = batchIndex;

            await semaphore.WaitAsync().ConfigureAwait(false);

            if (aborted)
            {
                semaphore.Release();
                break;
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    await ProcessBatchWithRetry(currentBatch, processor, options.RetryCount)
                        .ConfigureAwait(false);

                    lock (lockObj)
                    {
                        successCount += currentBatch.Count;
                        processedCount += currentBatch.Count;
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        errors.Add(new BatchError(currentIndex, ex));
                        failureCount += currentBatch.Count;
                        processedCount += currentBatch.Count;

                        if (options.OnBatchError == BatchErrorHandling.Abort)
                        {
                            aborted = true;
                        }
                    }
                }
                finally
                {
                    lock (lockObj)
                    {
                        var percent = totalCount > 0
                            ? (double)processedCount / totalCount * 100.0
                            : 100.0;

                        options.OnProgress?.Invoke(new BatchProgress(
                            processedCount,
                            totalCount,
                            currentIndex + 1,
                            totalBatches,
                            percent));
                    }

                    semaphore.Release();
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        stopwatch.Stop();

        return new BatchResult(successCount, failureCount, stopwatch.Elapsed, errors);
    }

    private static async Task ProcessBatchWithRetry<T>(
        IReadOnlyList<T> batch,
        Func<IReadOnlyList<T>, Task> processor,
        int retryCount)
    {
        var attempts = 0;

        while (true)
        {
            try
            {
                await processor(batch).ConfigureAwait(false);
                return;
            }
            catch when (attempts < retryCount)
            {
                attempts++;
            }
        }
    }

    private static List<IReadOnlyList<T>> CreateBatches<T>(IReadOnlyList<T> items, int batchSize)
    {
        var batches = new List<IReadOnlyList<T>>();
        var count = items.Count;

        for (var i = 0; i < count; i += batchSize)
        {
            var size = Math.Min(batchSize, count - i);
            var batch = new T[size];

            for (var j = 0; j < size; j++)
            {
                batch[j] = items[i + j];
            }

            batches.Add(batch);
        }

        return batches;
    }
}

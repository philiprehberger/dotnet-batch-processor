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
    /// <param name="cancellationToken">Optional cancellation token to cancel the entire processing pipeline.</param>
    /// <returns>A <see cref="BatchResult"/> summarizing the processing outcome.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or <paramref name="processor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static async Task<BatchResult> Process<T>(
        IEnumerable<T> items,
        int batchSize,
        Func<IReadOnlyList<T>, Task> processor,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
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

        if (options.AdaptiveBatching is not null)
        {
            ValidateAdaptiveBatchOptions(options.AdaptiveBatching);
            var clampedSize = Math.Clamp(batchSize, options.AdaptiveBatching.MinBatchSize, options.AdaptiveBatching.MaxBatchSize);

            return await ProcessAdaptive(
                materializedItems, clampedSize, processor, options, cancellationToken, stopwatch).ConfigureAwait(false);
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
            cancellationToken.ThrowIfCancellationRequested();

            if (aborted)
            {
                break;
            }

            if (batchIndex < options.ResumeFromBatch)
            {
                continue;
            }

            var currentBatch = batches[batchIndex];
            var currentIndex = batchIndex;

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (aborted)
            {
                semaphore.Release();
                break;
            }

            var task = Task.Run(async () =>
            {
                var batchStopwatch = Stopwatch.StartNew();
                var batchSuccessCount = 0;
                var batchFailureCount = 0;

                try
                {
                    await ProcessBatchWithRetry(currentBatch, processor, options.RetryCount, options.BatchTimeout, cancellationToken)
                        .ConfigureAwait(false);

                    batchSuccessCount = currentBatch.Count;

                    lock (lockObj)
                    {
                        successCount += currentBatch.Count;
                        processedCount += currentBatch.Count;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    batchFailureCount = currentBatch.Count;

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
                    batchStopwatch.Stop();

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

                        options.OnBatchCompleted?.Invoke(new BatchCompletedEventArgs(
                            currentIndex,
                            currentBatch.Count,
                            batchStopwatch.Elapsed,
                            batchSuccessCount,
                            batchFailureCount));

                        options.CheckpointCallback?.Invoke(currentIndex);
                    }

                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        stopwatch.Stop();

        return new BatchResult(successCount, failureCount, stopwatch.Elapsed, errors);
    }

    /// <summary>
    /// Processes items in batches asynchronously with per-item error tracking.
    /// Returns a <see cref="BatchResult{T}"/> containing individual results for each item.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="items">The collection of items to process.</param>
    /// <param name="batchSize">The number of items per batch. Must be greater than zero.</param>
    /// <param name="processor">
    /// An async function that processes a batch of items. Receives a read-only list of items in the batch.
    /// </param>
    /// <param name="options">Optional configuration for parallelism, progress, and error handling.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the entire processing pipeline.</param>
    /// <returns>A <see cref="BatchResult{T}"/> with per-item results and summary counts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or <paramref name="processor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static async Task<BatchResult<T>> ProcessAsync<T>(
        IEnumerable<T> items,
        int batchSize,
        Func<IReadOnlyList<T>, Task> processor,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
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
            return new BatchResult<T>([], 0, 0, [], stopwatch.Elapsed, []);
        }

        var batches = CreateBatches(materializedItems, batchSize);
        var totalBatches = batches.Count;

        var errors = new List<BatchError>();
        var allItemResults = new List<BatchItemResult<T>>();
        var processedCount = 0;
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
        var tasks = new List<Task>();
        var aborted = false;

        for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (aborted)
            {
                break;
            }

            if (batchIndex < options.ResumeFromBatch)
            {
                continue;
            }

            var currentBatch = batches[batchIndex];
            var currentIndex = batchIndex;

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (aborted)
            {
                semaphore.Release();
                break;
            }

            var task = Task.Run(async () =>
            {
                var batchStopwatch = Stopwatch.StartNew();
                var batchSuccessCount = 0;
                var batchFailureCount = 0;

                try
                {
                    await ProcessBatchWithRetry(currentBatch, processor, options.RetryCount, options.BatchTimeout, cancellationToken)
                        .ConfigureAwait(false);

                    batchSuccessCount = currentBatch.Count;

                    lock (lockObj)
                    {
                        foreach (var item in currentBatch)
                        {
                            allItemResults.Add(new BatchItemResult<T>(item, true, null));
                        }

                        processedCount += currentBatch.Count;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    batchFailureCount = currentBatch.Count;

                    lock (lockObj)
                    {
                        errors.Add(new BatchError(currentIndex, ex));

                        foreach (var item in currentBatch)
                        {
                            allItemResults.Add(new BatchItemResult<T>(item, false, ex));
                        }

                        processedCount += currentBatch.Count;

                        if (options.OnBatchError == BatchErrorHandling.Abort)
                        {
                            aborted = true;
                        }
                    }
                }
                finally
                {
                    batchStopwatch.Stop();

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

                        options.OnBatchCompleted?.Invoke(new BatchCompletedEventArgs(
                            currentIndex,
                            currentBatch.Count,
                            batchStopwatch.Elapsed,
                            batchSuccessCount,
                            batchFailureCount));

                        options.CheckpointCallback?.Invoke(currentIndex);
                    }

                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        stopwatch.Stop();

        var succeededCount = allItemResults.Count(r => r.Success);
        var failedCount = allItemResults.Count(r => !r.Success);
        var failures = allItemResults.Where(r => !r.Success).ToList();

        return new BatchResult<T>(allItemResults, succeededCount, failedCount, failures, stopwatch.Elapsed, errors);
    }

    /// <summary>
    /// Processes items from an async stream in batches asynchronously with configurable parallelism,
    /// progress reporting, and error handling. Items are consumed from the stream incrementally
    /// without materializing the full collection.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="source">The async enumerable source of items to process.</param>
    /// <param name="batchSize">The number of items per batch. Must be greater than zero.</param>
    /// <param name="processor">
    /// An async function that processes a batch of items. Receives a read-only list of items in the batch.
    /// </param>
    /// <param name="options">Optional configuration for parallelism, progress, and error handling.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the entire processing pipeline.</param>
    /// <returns>A <see cref="BatchResult"/> summarizing the processing outcome.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="processor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static async Task<BatchResult> ProcessStreamAsync<T>(
        IAsyncEnumerable<T> source,
        int batchSize,
        Func<IReadOnlyList<T>, Task> processor,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(processor);

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be at least 1.");
        }

        options ??= new BatchOptions();
        var stopwatch = Stopwatch.StartNew();

        var errors = new List<BatchError>();
        var counters = new int[3]; // [0]=success, [1]=failure, [2]=processed
        var batchIndex = 0;
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
        var tasks = new List<Task>();
        var abortedFlag = new[] { false };

        var currentBatch = new List<T>(batchSize);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (abortedFlag[0])
            {
                break;
            }

            currentBatch.Add(item);

            if (currentBatch.Count >= batchSize)
            {
                var batch = currentBatch.ToArray();
                currentBatch = new List<T>(batchSize);
                var currentIndex = batchIndex++;

                if (currentIndex < options.ResumeFromBatch)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (abortedFlag[0])
                {
                    semaphore.Release();
                    break;
                }

                tasks.Add(RunStreamBatch(
                    batch, currentIndex, processor, options, semaphore,
                    counters, errors, lockObj, abortedFlag, cancellationToken));
            }
        }

        if (currentBatch.Count > 0 && !abortedFlag[0])
        {
            var batch = currentBatch.ToArray();
            var currentIndex = batchIndex++;

            if (currentIndex >= options.ResumeFromBatch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (!abortedFlag[0])
                {
                    tasks.Add(RunStreamBatch(
                        batch, currentIndex, processor, options, semaphore,
                        counters, errors, lockObj, abortedFlag, cancellationToken));
                }
                else
                {
                    semaphore.Release();
                }
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        stopwatch.Stop();

        return new BatchResult(counters[0], counters[1], stopwatch.Elapsed, errors);
    }

    private static Task RunStreamBatch<T>(
        IReadOnlyList<T> batch,
        int batchIndex,
        Func<IReadOnlyList<T>, Task> processor,
        BatchOptions options,
        SemaphoreSlim semaphore,
        int[] counters,
        List<BatchError> errors,
        object lockObj,
        bool[] abortedFlag,
        CancellationToken cancellationToken)
    {
        var task = Task.Run(async () =>
        {
            var batchStopwatch = Stopwatch.StartNew();
            var batchSuccessCount = 0;
            var batchFailureCount = 0;

            try
            {
                await ProcessBatchWithRetry(batch, processor, options.RetryCount, options.BatchTimeout, cancellationToken)
                    .ConfigureAwait(false);

                batchSuccessCount = batch.Count;

                lock (lockObj)
                {
                    counters[0] += batch.Count;
                    counters[2] += batch.Count;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                batchFailureCount = batch.Count;

                lock (lockObj)
                {
                    errors.Add(new BatchError(batchIndex, ex));
                    counters[1] += batch.Count;
                    counters[2] += batch.Count;

                    if (options.OnBatchError == BatchErrorHandling.Abort)
                    {
                        abortedFlag[0] = true;
                    }
                }
            }
            finally
            {
                batchStopwatch.Stop();

                lock (lockObj)
                {
                    options.OnBatchCompleted?.Invoke(new BatchCompletedEventArgs(
                        batchIndex,
                        batch.Count,
                        batchStopwatch.Elapsed,
                        batchSuccessCount,
                        batchFailureCount));

                    options.CheckpointCallback?.Invoke(batchIndex);
                }

                semaphore.Release();
            }
        }, cancellationToken);

        return task;
    }

    private static async Task<BatchResult> ProcessAdaptive<T>(
        IReadOnlyList<T> items,
        int initialBatchSize,
        Func<IReadOnlyList<T>, Task> processor,
        BatchOptions options,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        var adaptiveOptions = options.AdaptiveBatching!;
        var errors = new List<BatchError>();
        var successCount = 0;
        var failureCount = 0;
        var processedCount = 0;
        var totalCount = items.Count;
        var currentBatchSize = initialBatchSize;
        var batchIndex = 0;
        var offset = 0;
        var aborted = false;

        while (offset < totalCount && !aborted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var size = Math.Min(currentBatchSize, totalCount - offset);
            var batch = new T[size];

            for (var j = 0; j < size; j++)
            {
                batch[j] = items[offset + j];
            }

            offset += size;

            if (batchIndex < options.ResumeFromBatch)
            {
                batchIndex++;
                continue;
            }

            var batchStopwatch = Stopwatch.StartNew();
            var batchSuccessCount = 0;
            var batchFailureCount = 0;

            try
            {
                await ProcessBatchWithRetry(batch, processor, options.RetryCount, options.BatchTimeout, cancellationToken)
                    .ConfigureAwait(false);

                batchSuccessCount = batch.Length;
                successCount += batch.Length;
                processedCount += batch.Length;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                batchFailureCount = batch.Length;
                errors.Add(new BatchError(batchIndex, ex));
                failureCount += batch.Length;
                processedCount += batch.Length;

                if (options.OnBatchError == BatchErrorHandling.Abort)
                {
                    aborted = true;
                }
            }
            finally
            {
                batchStopwatch.Stop();

                var percent = totalCount > 0
                    ? (double)processedCount / totalCount * 100.0
                    : 100.0;

                options.OnProgress?.Invoke(new BatchProgress(
                    processedCount,
                    totalCount,
                    batchIndex + 1,
                    0,
                    percent));

                options.OnBatchCompleted?.Invoke(new BatchCompletedEventArgs(
                    batchIndex,
                    batch.Length,
                    batchStopwatch.Elapsed,
                    batchSuccessCount,
                    batchFailureCount));

                options.CheckpointCallback?.Invoke(batchIndex);

                var elapsedSeconds = batchStopwatch.Elapsed.TotalSeconds;

                if (elapsedSeconds > 0)
                {
                    var throughput = batch.Length / elapsedSeconds;
                    var ratio = adaptiveOptions.TargetThroughput / throughput;
                    var newSize = (int)(currentBatchSize * ratio);
                    currentBatchSize = Math.Clamp(newSize, adaptiveOptions.MinBatchSize, adaptiveOptions.MaxBatchSize);
                }
            }

            batchIndex++;
        }

        stopwatch.Stop();
        return new BatchResult(successCount, failureCount, stopwatch.Elapsed, errors);
    }

    private static async Task ProcessBatchWithRetry<T>(
        IReadOnlyList<T> batch,
        Func<IReadOnlyList<T>, Task> processor,
        int retryCount,
        TimeSpan? batchTimeout,
        CancellationToken cancellationToken)
    {
        var attempts = 0;

        while (true)
        {
            try
            {
                if (batchTimeout.HasValue)
                {
                    using var timeoutCts = new CancellationTokenSource(batchTimeout.Value);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                    var processorTask = processor(batch);
                    var completedTask = await Task.WhenAny(processorTask, Task.Delay(Timeout.Infinite, linkedCts.Token))
                        .ConfigureAwait(false);

                    if (timeoutCts.IsCancellationRequested && !processorTask.IsCompleted)
                    {
                        throw new TimeoutException($"Batch processing exceeded the configured timeout of {batchTimeout.Value}.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await processorTask.ConfigureAwait(false);
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await processor(batch).ConfigureAwait(false);
                }

                return;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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

    private static void ValidateAdaptiveBatchOptions(AdaptiveBatchOptions options)
    {
        if (options.MinBatchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MinBatchSize, "MinBatchSize must be at least 1.");
        }

        if (options.MaxBatchSize < options.MinBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxBatchSize, "MaxBatchSize must be greater than or equal to MinBatchSize.");
        }

        if (options.TargetThroughput <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TargetThroughput, "TargetThroughput must be greater than zero.");
        }
    }
}

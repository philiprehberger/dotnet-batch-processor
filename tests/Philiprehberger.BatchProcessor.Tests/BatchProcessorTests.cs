using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class BatchProcessorTests
{
    [Fact]
    public async Task Process_ValidItems_ProcessesAllItems()
    {
        var items = Enumerable.Range(1, 10).ToList();
        var processed = new List<int>();

        var result = await BatchProcessor.Process(
            items,
            batchSize: 3,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            });

        Assert.Equal(10, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Process_EmptyCollection_ReturnsZeroCounts()
    {
        var result = await BatchProcessor.Process<int>(
            Array.Empty<int>(),
            batchSize: 5,
            processor: _ => Task.CompletedTask);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task Process_NullItems_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            BatchProcessor.Process<int>(
                null!,
                batchSize: 5,
                processor: _ => Task.CompletedTask));
    }

    [Fact]
    public async Task Process_BatchSizeLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BatchProcessor.Process(
                new[] { 1, 2, 3 },
                batchSize: 0,
                processor: _ => Task.CompletedTask));
    }

    [Fact]
    public async Task Process_ErrorHandlingAbort_StopsOnFirstError()
    {
        var items = Enumerable.Range(1, 10).ToList();
        var options = new BatchOptions(OnBatchError: BatchErrorHandling.Abort);

        var result = await BatchProcessor.Process(
            items,
            batchSize: 2,
            processor: _ => throw new InvalidOperationException("test error"),
            options: options);

        Assert.True(result.Errors.Count >= 1);
        Assert.True(result.FailureCount > 0);
    }

    [Fact]
    public async Task Process_ErrorHandlingSkip_ContinuesProcessing()
    {
        var callCount = 0;
        var items = Enumerable.Range(1, 6).ToList();
        var options = new BatchOptions(OnBatchError: BatchErrorHandling.Skip);

        var result = await BatchProcessor.Process(
            items,
            batchSize: 2,
            processor: batch =>
            {
                var current = Interlocked.Increment(ref callCount);
                if (current == 1) throw new InvalidOperationException("fail first batch");
                return Task.CompletedTask;
            },
            options: options);

        Assert.True(result.SuccessCount > 0);
        Assert.True(result.FailureCount > 0);
    }

    [Fact]
    public async Task Process_ProgressCallback_IsInvoked()
    {
        var progressUpdates = new List<BatchProgress>();
        var options = new BatchOptions(OnProgress: p =>
        {
            lock (progressUpdates) progressUpdates.Add(p);
        });

        await BatchProcessor.Process(
            Enumerable.Range(1, 4).ToList(),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(2, progressUpdates.Count);
    }
}

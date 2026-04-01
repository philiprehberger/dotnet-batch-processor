using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class ProcessStreamAsyncTests
{
    [Fact]
    public async Task ProcessStreamAsync_ValidSource_ProcessesAllItems()
    {
        var processed = new List<int>();

        var result = await BatchProcessor.ProcessStreamAsync(
            GenerateItems(10),
            batchSize: 3,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            });

        Assert.Equal(10, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Errors);
        Assert.Equal(10, processed.Count);
    }

    [Fact]
    public async Task ProcessStreamAsync_EmptySource_ReturnsZeroCounts()
    {
        var result = await BatchProcessor.ProcessStreamAsync(
            GenerateItems(0),
            batchSize: 5,
            processor: _ => Task.CompletedTask);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ProcessStreamAsync_NullSource_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            BatchProcessor.ProcessStreamAsync<int>(
                null!,
                batchSize: 5,
                processor: _ => Task.CompletedTask));
    }

    [Fact]
    public async Task ProcessStreamAsync_BatchSizeLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BatchProcessor.ProcessStreamAsync(
                GenerateItems(3),
                batchSize: 0,
                processor: _ => Task.CompletedTask));
    }

    [Fact]
    public async Task ProcessStreamAsync_ErrorHandlingSkip_ContinuesProcessing()
    {
        var callCount = 0;
        var options = new BatchOptions(OnBatchError: BatchErrorHandling.Skip);

        var result = await BatchProcessor.ProcessStreamAsync(
            GenerateItems(6),
            batchSize: 2,
            processor: _ =>
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
    public async Task ProcessStreamAsync_BatchCompletedCallback_IsInvoked()
    {
        var completedEvents = new List<BatchCompletedEventArgs>();
        var options = new BatchOptions(
            OnBatchCompleted: e =>
            {
                lock (completedEvents) completedEvents.Add(e);
            });

        await BatchProcessor.ProcessStreamAsync(
            GenerateItems(6),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, completedEvents.Count);
    }

    [Fact]
    public async Task ProcessStreamAsync_PartialLastBatch_IsProcessed()
    {
        var processed = new List<int>();

        var result = await BatchProcessor.ProcessStreamAsync(
            GenerateItems(7),
            batchSize: 3,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            });

        Assert.Equal(7, result.SuccessCount);
        Assert.Equal(7, processed.Count);
    }

    [Fact]
    public async Task ProcessStreamAsync_CancellationToken_ThrowsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            BatchProcessor.ProcessStreamAsync(
                GenerateItems(10),
                batchSize: 5,
                processor: _ => Task.CompletedTask,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ProcessStreamAsync_ResumeFromBatch_SkipsEarlierBatches()
    {
        var processed = new List<int>();
        var options = new BatchOptions(ResumeFromBatch: 2);

        var result = await BatchProcessor.ProcessStreamAsync(
            GenerateItems(10),
            batchSize: 2,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            },
            options: options);

        Assert.Equal(6, result.SuccessCount);
        Assert.Equal(6, processed.Count);
    }

    private static async IAsyncEnumerable<int> GenerateItems(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

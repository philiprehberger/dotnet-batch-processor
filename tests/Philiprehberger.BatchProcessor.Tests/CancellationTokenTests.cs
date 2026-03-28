using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class CancellationTokenTests
{
    [Fact]
    public async Task Process_CancellationToken_ThrowsWhenCancelledBeforeStart()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            BatchProcessor.Process(
                Enumerable.Range(1, 10).ToList(),
                batchSize: 5,
                processor: _ => Task.CompletedTask,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Process_CancellationToken_ThrowsWhenCancelledDuringProcessing()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();

        var task = BatchProcessor.Process(
            Enumerable.Range(1, 100).ToList(),
            batchSize: 10,
            processor: async _ =>
            {
                started.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
            },
            cancellationToken: cts.Token);

        await started.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Process_CancellationToken_DefaultDoesNotCancel()
    {
        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 5).ToList(),
            batchSize: 5,
            processor: _ => Task.CompletedTask,
            cancellationToken: default);

        Assert.Equal(5, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task ProcessAsync_CancellationToken_ThrowsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            BatchProcessor.ProcessAsync(
                Enumerable.Range(1, 10).ToList(),
                batchSize: 5,
                processor: _ => Task.CompletedTask,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ProcessAsync_CancellationToken_DefaultDoesNotCancel()
    {
        var result = await BatchProcessor.ProcessAsync(
            Enumerable.Range(1, 5).ToList(),
            batchSize: 5,
            processor: _ => Task.CompletedTask,
            cancellationToken: default);

        Assert.Equal(5, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
    }
}

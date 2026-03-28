using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class BatchTimeoutTests
{
    [Fact]
    public async Task Process_BatchTimeout_ThrowsTimeoutExceptionWhenExceeded()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var options = new BatchOptions(
            BatchTimeout: TimeSpan.FromMilliseconds(50),
            OnBatchError: BatchErrorHandling.Skip);

        var result = await BatchProcessor.Process(
            items,
            batchSize: 5,
            processor: async _ => await Task.Delay(TimeSpan.FromSeconds(5)),
            options: options);

        Assert.True(result.FailureCount > 0);
        Assert.True(result.Errors.Count > 0);
        Assert.IsType<TimeoutException>(result.Errors[0].Exception);
    }

    [Fact]
    public async Task Process_BatchTimeout_SucceedsWhenWithinTimeout()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var options = new BatchOptions(BatchTimeout: TimeSpan.FromSeconds(10));

        var result = await BatchProcessor.Process(
            items,
            batchSize: 5,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(5, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Process_BatchTimeout_NullMeansNoTimeout()
    {
        var items = Enumerable.Range(1, 3).ToList();
        var options = new BatchOptions(BatchTimeout: null);

        var result = await BatchProcessor.Process(
            items,
            batchSize: 3,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task ProcessAsync_BatchTimeout_ThrowsTimeoutExceptionWhenExceeded()
    {
        var items = Enumerable.Range(1, 5).ToList();
        var options = new BatchOptions(
            BatchTimeout: TimeSpan.FromMilliseconds(50),
            OnBatchError: BatchErrorHandling.Skip);

        var result = await BatchProcessor.ProcessAsync(
            items,
            batchSize: 5,
            processor: async _ => await Task.Delay(TimeSpan.FromSeconds(5)),
            options: options);

        Assert.True(result.FailedCount > 0);
        Assert.True(result.Errors.Count > 0);
        Assert.IsType<TimeoutException>(result.Errors[0].Exception);
    }
}

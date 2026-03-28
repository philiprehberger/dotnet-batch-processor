using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class BatchItemResultTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsPerItemResults()
    {
        var items = Enumerable.Range(1, 6).ToList();

        var result = await BatchProcessor.ProcessAsync(
            items,
            batchSize: 3,
            processor: _ => Task.CompletedTask);

        Assert.Equal(6, result.Items.Count);
        Assert.All(result.Items, r => Assert.True(r.Success));
        Assert.All(result.Items, r => Assert.Null(r.Exception));
        Assert.Equal(6, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task ProcessAsync_TracksFailedItems()
    {
        var items = Enumerable.Range(1, 6).ToList();
        var callCount = 0;
        var options = new BatchOptions(OnBatchError: BatchErrorHandling.Skip);

        var result = await BatchProcessor.ProcessAsync(
            items,
            batchSize: 3,
            processor: _ =>
            {
                var current = Interlocked.Increment(ref callCount);
                if (current == 1) throw new InvalidOperationException("batch 1 failed");
                return Task.CompletedTask;
            },
            options: options);

        Assert.Equal(6, result.Items.Count);
        Assert.Equal(3, result.SucceededCount);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(3, result.Failures.Count);
        Assert.All(result.Failures, r => Assert.False(r.Success));
        Assert.All(result.Failures, r => Assert.NotNull(r.Exception));
    }

    [Fact]
    public async Task ProcessAsync_EmptyCollection_ReturnsEmptyResults()
    {
        var result = await BatchProcessor.ProcessAsync<int>(
            Array.Empty<int>(),
            batchSize: 5,
            processor: _ => Task.CompletedTask);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void BatchItemResult_Properties_ReturnCorrectValues()
    {
        var ex = new InvalidOperationException("fail");
        var successResult = new BatchItemResult<int>(42, true, null);
        var failureResult = new BatchItemResult<int>(99, false, ex);

        Assert.Equal(42, successResult.Item);
        Assert.True(successResult.Success);
        Assert.Null(successResult.Exception);

        Assert.Equal(99, failureResult.Item);
        Assert.False(failureResult.Success);
        Assert.Same(ex, failureResult.Exception);
    }

    [Fact]
    public void BatchResultGeneric_Properties_ReturnCorrectValues()
    {
        var items = new List<BatchItemResult<int>>
        {
            new(1, true, null),
            new(2, false, new Exception("fail"))
        };
        var failures = items.Where(i => !i.Success).ToList();
        var errors = new List<BatchError> { new(0, new Exception("batch fail")) };

        var result = new BatchResult<int>(items, 1, 1, failures, TimeSpan.FromSeconds(2), errors);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Failures);
        Assert.Single(result.Errors);
    }
}

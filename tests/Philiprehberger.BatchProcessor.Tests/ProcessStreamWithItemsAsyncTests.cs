using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class ProcessStreamWithItemsAsyncTests
{
    [Fact]
    public async Task ProcessStreamWithItemsAsync_AllSucceed_ReportsEveryItemAsSuccess()
    {
        var result = await BatchProcessor.ProcessStreamWithItemsAsync(
            GenerateItems(7),
            batchSize: 3,
            processor: _ => Task.CompletedTask);

        Assert.Equal(7, result.Items.Count);
        Assert.Equal(7, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Failures);
        Assert.All(result.Items, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task ProcessStreamWithItemsAsync_BatchFails_TracksItemFailures()
    {
        var result = await BatchProcessor.ProcessStreamWithItemsAsync(
            GenerateItems(6),
            batchSize: 3,
            processor: batch =>
            {
                if (batch.Contains(4))
                {
                    throw new InvalidOperationException("boom");
                }
                return Task.CompletedTask;
            },
            new BatchOptions { OnBatchError = BatchErrorHandling.Skip });

        Assert.Equal(6, result.Items.Count);
        Assert.Equal(3, result.SucceededCount);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(3, result.Failures.Count);
        Assert.All(result.Failures, f => Assert.IsType<InvalidOperationException>(f.Exception));
    }

    [Fact]
    public async Task ProcessStreamWithItemsAsync_NegativeResumeFromBatch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BatchProcessor.ProcessStreamWithItemsAsync(
                GenerateItems(3),
                batchSize: 1,
                processor: _ => Task.CompletedTask,
                new BatchOptions { ResumeFromBatch = -1 }));
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

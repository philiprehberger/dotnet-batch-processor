using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class CheckpointResumeTests
{
    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Process_CheckpointCallback_InvokedForEachBatch()
    {
        var checkpoints = new List<int>();
        var options = new BatchOptions(
            CheckpointCallback: index =>
            {
                lock (checkpoints) checkpoints.Add(index);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 6).ToList(),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, checkpoints.Count);
        var sorted = checkpoints.OrderBy(i => i).ToList();
        Assert.Equal(new[] { 0, 1, 2 }, sorted);
    }

    [Fact]
    public async Task Process_ResumeFromBatch_SkipsEarlierBatches()
    {
        var processed = new List<int>();
        var options = new BatchOptions(ResumeFromBatch: 2);

        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 10).ToList(),
            batchSize: 2,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            },
            options: options);

        Assert.Equal(6, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(6, processed.Count);
        Assert.DoesNotContain(1, processed);
        Assert.DoesNotContain(2, processed);
        Assert.DoesNotContain(3, processed);
        Assert.DoesNotContain(4, processed);
    }

    [Fact]
    public async Task Process_ResumeFromBatch_ZeroProcessesAll()
    {
        var options = new BatchOptions(ResumeFromBatch: 0);

        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 6).ToList(),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(6, result.SuccessCount);
    }

    [Fact]
    public async Task Process_ResumeFromBatch_BeyondTotalBatches_ProcessesNothing()
    {
        var options = new BatchOptions(ResumeFromBatch: 100);

        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 6).ToList(),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task ProcessAsync_ResumeFromBatch_SkipsEarlierBatches()
    {
        var options = new BatchOptions(ResumeFromBatch: 1);

        var result = await BatchProcessor.ProcessAsync(
            Enumerable.Range(1, 6).ToList(),
            batchSize: 3,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task ProcessAsync_CheckpointCallback_InvokedForEachBatch()
    {
        var checkpoints = new List<int>();
        var options = new BatchOptions(
            CheckpointCallback: index =>
            {
                lock (checkpoints) checkpoints.Add(index);
            });

        await BatchProcessor.ProcessAsync(
            Enumerable.Range(1, 6).ToList(),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, checkpoints.Count);
    }

    [Fact]
    public async Task ProcessStream_CheckpointAndResume_WorkTogether()
    {
        var checkpoints = new List<int>();
        var items = Enumerable.Range(1, 10).ToList();

        var options = new BatchOptions(
            CheckpointCallback: index =>
            {
                lock (checkpoints) checkpoints.Add(index);
            });

        var result = await BatchProcessor.ProcessStreamAsync(
            ToAsync(items),
            batchSize: 2,
            processor: batch => Task.CompletedTask,
            options: options);

        Assert.True(result.SuccessCount > 0);
        Assert.NotEmpty(checkpoints);
    }
}

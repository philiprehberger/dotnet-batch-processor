using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class AdaptiveBatchTests
{
    [Fact]
    public async Task Process_AdaptiveBatching_ProcessesAllItems()
    {
        var processed = new List<int>();
        var options = new BatchOptions(
            AdaptiveBatching: new AdaptiveBatchOptions(
                MinBatchSize: 1,
                MaxBatchSize: 100,
                TargetThroughput: 1000));

        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 20).ToList(),
            batchSize: 5,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            },
            options: options);

        Assert.Equal(20, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(20, processed.Count);
    }

    [Fact]
    public async Task Process_AdaptiveBatching_RespectsMinBatchSize()
    {
        var batchSizes = new List<int>();
        var options = new BatchOptions(
            AdaptiveBatching: new AdaptiveBatchOptions(
                MinBatchSize: 3,
                MaxBatchSize: 100,
                TargetThroughput: 0.01),
            OnBatchCompleted: e =>
            {
                lock (batchSizes) batchSizes.Add(e.ItemCount);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 20).ToList(),
            batchSize: 10,
            processor: async batch =>
            {
                await Task.Delay(1);
            },
            options: options);

        foreach (var size in batchSizes.SkipLast(1))
        {
            Assert.True(size >= 3, $"Batch size {size} is less than MinBatchSize 3");
        }
    }

    [Fact]
    public async Task Process_AdaptiveBatching_RespectsMaxBatchSize()
    {
        var batchSizes = new List<int>();
        var options = new BatchOptions(
            AdaptiveBatching: new AdaptiveBatchOptions(
                MinBatchSize: 1,
                MaxBatchSize: 8,
                TargetThroughput: 1_000_000),
            OnBatchCompleted: e =>
            {
                lock (batchSizes) batchSizes.Add(e.ItemCount);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 50).ToList(),
            batchSize: 5,
            processor: async batch =>
            {
                await Task.Delay(10);
            },
            options: options);

        foreach (var size in batchSizes)
        {
            Assert.True(size <= 8, $"Batch size {size} exceeds MaxBatchSize 8");
        }
    }

    [Fact]
    public async Task Process_AdaptiveBatching_CheckpointCallbackInvoked()
    {
        var checkpoints = new List<int>();
        var options = new BatchOptions(
            AdaptiveBatching: new AdaptiveBatchOptions(
                MinBatchSize: 2,
                MaxBatchSize: 10,
                TargetThroughput: 100),
            CheckpointCallback: index =>
            {
                lock (checkpoints) checkpoints.Add(index);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 10).ToList(),
            batchSize: 5,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.True(checkpoints.Count > 0);
    }

    [Fact]
    public async Task Process_AdaptiveBatching_ErrorHandlingAbort_StopsOnFirstError()
    {
        var options = new BatchOptions(
            OnBatchError: BatchErrorHandling.Abort,
            AdaptiveBatching: new AdaptiveBatchOptions(
                MinBatchSize: 2,
                MaxBatchSize: 10,
                TargetThroughput: 100));

        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 20).ToList(),
            batchSize: 5,
            processor: _ => throw new InvalidOperationException("fail"),
            options: options);

        Assert.True(result.Errors.Count >= 1);
        Assert.True(result.FailureCount > 0);
    }

    [Fact]
    public async Task Process_AdaptiveBatching_ResumeFromBatch_SkipsEarlierBatches()
    {
        var processed = new List<int>();
        var options = new BatchOptions(
            ResumeFromBatch: 1,
            AdaptiveBatching: new AdaptiveBatchOptions(
                MinBatchSize: 5,
                MaxBatchSize: 5,
                TargetThroughput: 100));

        var result = await BatchProcessor.Process(
            Enumerable.Range(1, 10).ToList(),
            batchSize: 5,
            processor: batch =>
            {
                lock (processed) processed.AddRange(batch);
                return Task.CompletedTask;
            },
            options: options);

        Assert.Equal(5, result.SuccessCount);
        Assert.Equal(5, processed.Count);
    }

    [Fact]
    public void AdaptiveBatchOptions_Defaults_AreCorrect()
    {
        var options = new AdaptiveBatchOptions();

        Assert.Equal(1, options.MinBatchSize);
        Assert.Equal(1000, options.MaxBatchSize);
        Assert.Equal(100.0, options.TargetThroughput);
    }

    [Fact]
    public async Task Process_AdaptiveBatching_EmptyCollection_ReturnsZeroCounts()
    {
        var options = new BatchOptions(
            AdaptiveBatching: new AdaptiveBatchOptions());

        var result = await BatchProcessor.Process<int>(
            Array.Empty<int>(),
            batchSize: 5,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }
}

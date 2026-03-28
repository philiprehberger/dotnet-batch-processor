using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class BatchCompletedCallbackTests
{
    [Fact]
    public async Task Process_OnBatchCompleted_InvokedForEachBatch()
    {
        var completedEvents = new List<BatchCompletedEventArgs>();
        var options = new BatchOptions(
            OnBatchCompleted: e =>
            {
                lock (completedEvents) completedEvents.Add(e);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 6).ToList(),
            batchSize: 2,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, completedEvents.Count);
        Assert.All(completedEvents, e => Assert.Equal(2, e.ItemCount));
        Assert.All(completedEvents, e => Assert.Equal(2, e.SuccessCount));
        Assert.All(completedEvents, e => Assert.Equal(0, e.FailureCount));
        Assert.All(completedEvents, e => Assert.True(e.Elapsed > TimeSpan.Zero));
    }

    [Fact]
    public async Task Process_OnBatchCompleted_ReportsFailureCounts()
    {
        var completedEvents = new List<BatchCompletedEventArgs>();
        var options = new BatchOptions(
            OnBatchError: BatchErrorHandling.Skip,
            OnBatchCompleted: e =>
            {
                lock (completedEvents) completedEvents.Add(e);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 4).ToList(),
            batchSize: 4,
            processor: _ => throw new InvalidOperationException("fail"),
            options: options);

        Assert.Single(completedEvents);
        Assert.Equal(4, completedEvents[0].ItemCount);
        Assert.Equal(0, completedEvents[0].SuccessCount);
        Assert.Equal(4, completedEvents[0].FailureCount);
    }

    [Fact]
    public async Task Process_OnBatchCompleted_IncludesBatchIndex()
    {
        var completedEvents = new List<BatchCompletedEventArgs>();
        var options = new BatchOptions(
            OnBatchCompleted: e =>
            {
                lock (completedEvents) completedEvents.Add(e);
            });

        await BatchProcessor.Process(
            Enumerable.Range(1, 9).ToList(),
            batchSize: 3,
            processor: _ => Task.CompletedTask,
            options: options);

        Assert.Equal(3, completedEvents.Count);
        var indices = completedEvents.Select(e => e.BatchIndex).OrderBy(i => i).ToList();
        Assert.Equal(new[] { 0, 1, 2 }, indices);
    }

    [Fact]
    public void BatchCompletedEventArgs_Properties_ReturnCorrectValues()
    {
        var args = new BatchCompletedEventArgs(2, 10, TimeSpan.FromMilliseconds(500), 8, 2);

        Assert.Equal(2, args.BatchIndex);
        Assert.Equal(10, args.ItemCount);
        Assert.Equal(TimeSpan.FromMilliseconds(500), args.Elapsed);
        Assert.Equal(8, args.SuccessCount);
        Assert.Equal(2, args.FailureCount);
    }
}

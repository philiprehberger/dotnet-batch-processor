using Xunit;

namespace Philiprehberger.BatchProcessor.Tests;

public class BatchResultTests
{
    [Fact]
    public void BatchResult_Properties_ReturnCorrectValues()
    {
        var errors = new List<BatchError> { new(0, new Exception("test")) };
        var result = new BatchResult(10, 2, TimeSpan.FromSeconds(5), errors);

        Assert.Equal(10, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Equal(TimeSpan.FromSeconds(5), result.TotalDuration);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void BatchError_Properties_ReturnCorrectValues()
    {
        var ex = new InvalidOperationException("test");
        var error = new BatchError(3, ex);

        Assert.Equal(3, error.BatchIndex);
        Assert.Same(ex, error.Exception);
    }

    [Fact]
    public void BatchProgress_Properties_ReturnCorrectValues()
    {
        var progress = new BatchProgress(50, 100, 3, 5, 50.0);

        Assert.Equal(50, progress.ProcessedCount);
        Assert.Equal(100, progress.TotalCount);
        Assert.Equal(3, progress.CurrentBatch);
        Assert.Equal(5, progress.TotalBatches);
        Assert.Equal(50.0, progress.Percent);
    }

    [Fact]
    public void BatchOptions_Defaults_AreCorrect()
    {
        var options = new BatchOptions();

        Assert.Equal(1, options.MaxDegreeOfParallelism);
        Assert.Null(options.OnProgress);
        Assert.Equal(BatchErrorHandling.Abort, options.OnBatchError);
        Assert.Equal(0, options.RetryCount);
        Assert.Null(options.BatchTimeout);
        Assert.Null(options.OnBatchCompleted);
    }
}

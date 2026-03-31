# Changelog

## 0.2.1 (2026-03-31)

- Standardize README to 3-badge format with emoji Support section
- Update CI actions to v5 for Node.js 24 compatibility

## 0.2.0 (2026-03-28)

- Add per-batch timeout via `BatchTimeout` option with `TimeoutException` on expiry
- Add per-item error tracking with `BatchResult<T>`, `BatchItemResult<T>`, and `ProcessAsync` method
- Add `OnBatchCompleted` callback with batch index, item count, elapsed time, and success/failure counts
- Add cancellation token support on `Process` and `ProcessAsync` methods
- Add `BatchCompletedEventArgs` record for batch completion details
- Add GitHub issue templates, dependabot config, and PR template
- Add missing README badges (GitHub release, Last updated, Bug Reports, Feature Requests)
- Add Support section to README

## 0.1.3 (2026-03-26)

- Add Sponsor badge and fix License link format in README

## 0.1.2 (2026-03-24)

- Add unit tests
- Add test step to CI workflow

## 0.1.1 (2026-03-24)

- Expand README usage section with feature subsections

## 0.1.0 (2026-03-22)

- Initial release
- Batch processing with configurable batch sizes
- Async execution with parallelism control
- Progress reporting via callback
- Error handling with abort or skip strategies
- Retry support for failed batches

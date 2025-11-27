/*
 * Copyright 2023 G.W. Lucas
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Tinfour.Core.Common;

/// <summary>
///     Provides an interface for monitoring progress and supporting cancellation
///     during long-running operations.
/// </summary>
public interface IMonitorWithCancellation
{
    /// <summary>
    ///     Gets the reporting interval in percent for progress updates.
    /// </summary>
    /// <returns>The interval percentage for progress reporting</returns>
    int GetReportingIntervalInPercent();

    /// <summary>
    ///     Checks if the operation has been cancelled.
    /// </summary>
    /// <returns>True if cancelled, false otherwise</returns>
    bool IsCancelled();

    /// <summary>
    ///     Reports progress as a percentage complete.
    /// </summary>
    /// <param name="percentComplete">The percentage complete (0-100)</param>
    void ReportProgress(int percentComplete);
}

/// <summary>
///     A simple implementation of IMonitorWithCancellation that provides
///     basic progress reporting and cancellation support.
/// </summary>
public class SimpleProgressMonitor : IMonitorWithCancellation
{
    private readonly int _reportingInterval;

    private volatile bool _cancelled;

    private int _lastReportedProgress = -1;

    /// <summary>
    ///     Initializes a new progress monitor.
    /// </summary>
    /// <param name="reportingIntervalPercent">The interval for progress reporting (1-100)</param>
    public SimpleProgressMonitor(int reportingIntervalPercent = 5)
    {
        if (reportingIntervalPercent < 1 || reportingIntervalPercent > 100)
            throw new ArgumentOutOfRangeException(
                nameof(reportingIntervalPercent),
                "Reporting interval must be between 1 and 100 percent");

        _reportingInterval = reportingIntervalPercent;
    }

    /// <summary>
    ///     Event raised when operation is cancelled.
    /// </summary>
    public event Action? Cancelled;

    /// <summary>
    ///     Event raised when progress is reported.
    /// </summary>
    public event Action<int>? ProgressReported;

    /// <summary>
    ///     Cancels the monitored operation.
    /// </summary>
    public void Cancel()
    {
        if (!_cancelled)
        {
            _cancelled = true;
            Cancelled?.Invoke();
        }
    }

    /// <summary>
    ///     Gets the reporting interval in percent for progress updates.
    /// </summary>
    /// <returns>The interval percentage for progress reporting</returns>
    public int GetReportingIntervalInPercent()
    {
        return _reportingInterval;
    }

    /// <summary>
    ///     Checks if the operation has been cancelled.
    /// </summary>
    /// <returns>True if cancelled, false otherwise</returns>
    public bool IsCancelled()
    {
        return _cancelled;
    }

    /// <summary>
    ///     Reports progress as a percentage complete.
    /// </summary>
    /// <param name="percentComplete">The percentage complete (0-100)</param>
    public void ReportProgress(int percentComplete)
    {
        if (percentComplete < 0) percentComplete = 0;
        if (percentComplete > 100) percentComplete = 100;

        // Only report if we've crossed a reporting threshold
        if (percentComplete - _lastReportedProgress >= _reportingInterval || percentComplete == 0
            || percentComplete == 100)
        {
            _lastReportedProgress = percentComplete;
            ProgressReported?.Invoke(percentComplete);
        }
    }

    /// <summary>
    ///     Resets the monitor for reuse.
    /// </summary>
    public void Reset()
    {
        _cancelled = false;
        _lastReportedProgress = -1;
    }
}
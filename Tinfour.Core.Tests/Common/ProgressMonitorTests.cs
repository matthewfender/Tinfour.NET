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

namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;

using Xunit;

public class ProgressMonitorTests
{
    [Fact]
    public void Cancel_MultipleTimes_ShouldOnlyFireEventOnce()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor();
        var eventFireCount = 0;
        monitor.Cancelled += () => eventFireCount++;

        // Act
        monitor.Cancel();
        monitor.Cancel();
        monitor.Cancel();

        // Assert
        Assert.True(monitor.IsCancelled());
        Assert.Equal(1, eventFireCount);
    }

    [Fact]
    public void Cancel_ShouldSetCancelledFlag()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor();
        var cancelledEventFired = false;
        monitor.Cancelled += () => cancelledEventFired = true;

        // Act
        monitor.Cancel();

        // Assert
        Assert.True(monitor.IsCancelled());
        Assert.True(cancelledEventFired);
    }

    [Fact]
    public void Constructor_WithInvalidInterval_ShouldThrow()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimpleProgressMonitor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimpleProgressMonitor(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimpleProgressMonitor(-5));
    }

    [Fact]
    public void Constructor_WithValidInterval_ShouldInitialize()
    {
        // Arrange & Act
        var monitor = new SimpleProgressMonitor(10);

        // Assert
        Assert.Equal(10, monitor.GetReportingIntervalInPercent());
        Assert.False(monitor.IsCancelled());
    }

    [Fact]
    public void DefaultConstructor_ShouldUseDefaultInterval()
    {
        // Arrange & Act
        var monitor = new SimpleProgressMonitor();

        // Assert
        Assert.Equal(5, monitor.GetReportingIntervalInPercent()); // Default is 5%
    }

    [Fact]
    public void ProgressReporting_WithLargeInterval_ShouldReportLessFrequently()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(50); // 50% interval
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        // Act
        for (var i = 0; i <= 100; i += 10) monitor.ReportProgress(i);

        // Assert
        Assert.True(reportedValues.Count <= 4); // Should report infrequently
        Assert.Contains(0, reportedValues); // Always reports
        Assert.Contains(100, reportedValues); // Always reports
    }

    [Fact]
    public void ProgressReporting_WithSmallInterval_ShouldReportMoreFrequently()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(1); // 1% interval
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        // Act
        for (var i = 0; i <= 10; i++) monitor.ReportProgress(i);

        // Assert
        Assert.True(reportedValues.Count > 5); // Should report frequently
        Assert.Contains(0, reportedValues);
        Assert.Contains(10, reportedValues);
    }

    [Fact]
    public void ReportProgress_AtBoundaries_ShouldAlwaysReport()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(25); // Large interval
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        // Act
        monitor.ReportProgress(0); // Always reports
        monitor.ReportProgress(10); // Shouldn't report (< 25)
        monitor.ReportProgress(100); // Always reports

        // Assert
        Assert.Equal(2, reportedValues.Count);
        Assert.Contains(0, reportedValues);
        Assert.Contains(100, reportedValues);
        Assert.DoesNotContain(10, reportedValues);
    }

    [Fact]
    public void ReportProgress_OutOfRange_ShouldClamp()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(10);
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        // Act
        monitor.ReportProgress(-10); // Should clamp to 0
        monitor.ReportProgress(150); // Should clamp to 100

        // Assert
        Assert.Contains(0, reportedValues);
        Assert.Contains(100, reportedValues);
    }

    [Fact]
    public void ReportProgress_ShouldTriggerEvent()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(10);
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        // Act
        monitor.ReportProgress(0);
        monitor.ReportProgress(15);
        monitor.ReportProgress(25);
        monitor.ReportProgress(100);

        // Assert
        Assert.Contains(0, reportedValues);
        Assert.Contains(15, reportedValues);
        Assert.Contains(25, reportedValues);
        Assert.Contains(100, reportedValues);
    }

    [Fact]
    public void ReportProgress_WithinInterval_ShouldNotTriggerEvent()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(10);
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        // Act
        monitor.ReportProgress(0); // Should report (0 always reports)
        monitor.ReportProgress(5); // Should not report (within interval)
        monitor.ReportProgress(8); // Should not report (within interval)
        monitor.ReportProgress(12); // Should report (crosses threshold)

        // Assert
        Assert.Equal(2, reportedValues.Count);
        Assert.Contains(0, reportedValues);
        Assert.Contains(12, reportedValues);
    }

    [Fact]
    public void Reset_ShouldClearStateAndProgress()
    {
        // Arrange
        var monitor = new SimpleProgressMonitor(10);
        var reportedValues = new List<int>();
        monitor.ProgressReported += (int progress) => reportedValues.Add(progress);

        monitor.ReportProgress(50);
        monitor.Cancel();

        // Act
        monitor.Reset();

        // Assert
        Assert.False(monitor.IsCancelled());

        // Should report again after reset even if same value
        monitor.ReportProgress(0);
        Assert.Contains(0, reportedValues);
    }
}
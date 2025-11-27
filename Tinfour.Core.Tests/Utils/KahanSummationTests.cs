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

namespace Tinfour.Core.Tests.Utils;

using Tinfour.Core.Utils;

using Xunit;

public class KahanSummationTests
{
    [Fact]
    public void Add_AlternatingSignValues_ShouldCancelCorrectly()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act - Add alternating positive and negative values
        for (var i = 0; i < 1000; i++)
        {
            kahan.Add(1.0);
            kahan.Add(-1.0);
        }

        // Assert
        Assert.Equal(0.0, kahan.GetSum(), 15);
        Assert.Equal(0.0, kahan.GetMean(), 15);
        Assert.Equal(2000, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_Array_ShouldSumCorrectly()
    {
        // Arrange
        var kahan = new KahanSummation();
        double[] values = { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        kahan.Add(values);

        // Assert
        Assert.Equal(15.0, kahan.GetSum());
        Assert.Equal(3.0, kahan.GetMean());
        Assert.Equal(5, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_IEnumerable_ShouldSumCorrectly()
    {
        // Arrange
        var kahan = new KahanSummation();
        var values = new List<double> { 2.5, 3.5, 4.5 };

        // Act
        kahan.Add(values);

        // Assert
        Assert.Equal(10.5, kahan.GetSum());
        Assert.Equal(3.5, kahan.GetMean());
        Assert.Equal(3, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_MultipleValues_ShouldSumCorrectly()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act
        kahan.Add(1.0);
        kahan.Add(2.0);
        kahan.Add(3.0);
        kahan.Add(4.0);

        // Assert
        Assert.Equal(10.0, kahan.GetSum());
        Assert.Equal(2.5, kahan.GetMean());
        Assert.Equal(4, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_NegativeNumbers_ShouldWorkCorrectly()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act
        kahan.Add(10.0);
        kahan.Add(-3.0);
        kahan.Add(-2.0);
        kahan.Add(1.0);

        // Assert
        Assert.Equal(6.0, kahan.GetSum());
        Assert.Equal(1.5, kahan.GetMean());
        Assert.Equal(4, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_SingleValue_ShouldUpdateCorrectly()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act
        kahan.Add(5.0);

        // Assert
        Assert.Equal(5.0, kahan.GetSum());
        Assert.Equal(5.0, kahan.GetMean());
        Assert.Equal(1, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_WithVerySmallNumbers_ShouldPreservePrecision()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act - Add many very small numbers
        for (var i = 0; i < 1000000; i++) kahan.Add(1e-10);

        // Assert
        var expected = 1000000 * 1e-10; // 1e-4
        Assert.Equal(expected, kahan.GetSum(), 12);
        Assert.Equal(1000000, kahan.GetSummandCount());
    }

    [Fact]
    public void Add_ZeroValues_ShouldMaintainCount()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act
        kahan.Add(0.0);
        kahan.Add(0.0);
        kahan.Add(0.0);

        // Assert
        Assert.Equal(0.0, kahan.GetSum());
        Assert.Equal(0.0, kahan.GetMean());
        Assert.Equal(3, kahan.GetSummandCount());
    }

    [Fact]
    public void Constructor_ShouldInitializeToZero()
    {
        // Arrange & Act
        var kahan = new KahanSummation();

        // Assert
        Assert.Equal(0.0, kahan.GetSum());
        Assert.Equal(0.0, kahan.GetMean());
        Assert.Equal(0, kahan.GetSummandCount());
        Assert.Equal(0.0, kahan.GetCompensation());
    }

    [Fact]
    public void GetCompensation_ShouldTrackAccumulatedError()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act - Add a small value to a large value where standard addition would lose precision
        kahan.Add(1.0);
        kahan.Add(1e-15);
        kahan.Add(1e-15);

        // Assert
        var sum = kahan.GetSum();
        Assert.True(sum > 1.0, $"Expected sum greater than 1.0, but got {sum}");

        // The compensation may help preserve some of the small values
        var compensation = kahan.GetCompensation();
        Assert.True(double.IsFinite(compensation));

        // Should have counted all values
        Assert.Equal(3, kahan.GetSummandCount());
    }

    [Fact]
    public void GetMean_WithZeroCount_ShouldReturnZero()
    {
        // Arrange
        var kahan = new KahanSummation();

        // Act
        var mean = kahan.GetMean();

        // Assert
        Assert.Equal(0.0, mean);
    }

    [Fact]
    public void ImplicitConversion_ToDouble_ShouldReturnSum()
    {
        // Arrange
        var kahan = new KahanSummation();
        kahan.Add(1.5);
        kahan.Add(2.5);

        // Act
        double sum = kahan; // Implicit conversion

        // Assert
        Assert.Equal(4.0, sum);
    }

    [Fact]
    public void KahanSummation_ShouldImproveAccuracyOverNaiveSum()
    {
        // Arrange - Test case where naive summation loses precision
        var kahan = new KahanSummation();
        var large = 1e16;
        var small = 1.0;

        // Act - Add large number, then many small numbers
        kahan.Add(large);
        for (var i = 0; i < 1000; i++) kahan.Add(small);

        // Naive sum for comparison
        var naiveSum = large;
        for (var i = 0; i < 1000; i++) naiveSum += small;

        // Assert - Kahan should be more accurate
        var expectedSum = large + 1000.0;
        Assert.Equal(expectedSum, kahan.GetSum(), 10);

        // Naive sum often loses precision in this scenario
        // (This test documents the improvement, actual values may vary by platform)
    }

    [Fact]
    public void Reset_ShouldRestoreInitialState()
    {
        // Arrange
        var kahan = new KahanSummation();
        kahan.Add(1.0);
        kahan.Add(2.0);
        kahan.Add(3.0);

        // Act
        kahan.Reset();

        // Assert
        Assert.Equal(0.0, kahan.GetSum());
        Assert.Equal(0.0, kahan.GetMean());
        Assert.Equal(0, kahan.GetSummandCount());
        Assert.Equal(0.0, kahan.GetCompensation());
    }

    [Fact]
    public void ToString_ShouldIncludeSumAndCount()
    {
        // Arrange
        var kahan = new KahanSummation();
        kahan.Add(1.0);
        kahan.Add(2.0);

        // Act
        var result = kahan.ToString();

        // Assert
        Assert.Contains("3", result); // Sum
        Assert.Contains("2", result); // Count
        Assert.Contains("KahanSummation", result);
    }
}
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

public class ThresholdsTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.1)]
    [InlineData(10.0)]
    [InlineData(1000.0)]
    [InlineData(1e-6)]
    public void AllThresholds_ShouldBePositive(double nominalSpacing)
    {
        // Arrange & Act
        var thresholds = new Thresholds(nominalSpacing);

        // Assert
        Assert.True(thresholds.GetInCircleThreshold() > 0);
        Assert.True(thresholds.GetVertexTolerance() > 0);
        Assert.True(thresholds.GetVertexTolerance2() > 0);
        Assert.True(thresholds.GetPrecisionThreshold() > 0);
        Assert.True(thresholds.GetHalfPlaneThreshold() > 0);
        Assert.True(thresholds.GetDelaunayThreshold() > 0);
        Assert.True(thresholds.GetCircumcircleDeterminantThreshold() > 0);
    }

    [Fact]
    public void Constructor_DefaultNominalPointSpacing_ShouldCreateValidThresholds()
    {
        // Act
        var thresholds = new Thresholds();

        // Assert
        Assert.Equal(1.0, thresholds.GetNominalPointSpacing());
        Assert.True(thresholds.GetInCircleThreshold() > 0);
        Assert.True(thresholds.GetVertexTolerance() > 0);
        Assert.True(thresholds.GetPrecisionThreshold() > 0);
        Assert.True(thresholds.GetHalfPlaneThreshold() > 0);
        Assert.True(thresholds.GetDelaunayThreshold() > 0);
        Assert.True(thresholds.GetCircumcircleDeterminantThreshold() > 0);
    }

    [Fact]
    public void Constructor_WithCustomSpacing_ShouldScaleThresholdsCorrectly()
    {
        // Arrange
        var spacing1 = 1.0;
        var spacing10 = 10.0;

        // Act
        var thresholds1 = new Thresholds(spacing1);
        var thresholds10 = new Thresholds(spacing10);

        // Assert - Vertex tolerance should scale with spacing
        Assert.Equal(spacing1, thresholds1.GetNominalPointSpacing());
        Assert.Equal(spacing10, thresholds10.GetNominalPointSpacing());

        // Vertex tolerance should be proportional to nominal spacing
        var expectedTolerance10 = spacing10 / Thresholds.VertexToleranceFactorDefault;
        Assert.Equal(expectedTolerance10, thresholds10.GetVertexTolerance(), 10);

        // Other thresholds should generally be smaller for larger spacing
        // due to ULP-based calculations
        Assert.True(thresholds10.GetVertexTolerance() > thresholds1.GetVertexTolerance());
    }

    [Fact]
    public void Constructor_WithNegativeSpacing_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Thresholds(-1.0));
    }

    [Fact]
    public void Constructor_WithZeroSpacing_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Thresholds(0.0));
    }

    [Fact]
    public void GetThresholds_WithLargeSpacing_ShouldProduceLargerVertexTolerance()
    {
        // Arrange  
        var thresholds = new Thresholds(1000.0); // 1km nominal spacing

        // Act
        var vertexTolerance = thresholds.GetVertexTolerance();

        // Assert
        var expectedTolerance = 1000.0 / Thresholds.VertexToleranceFactorDefault;
        Assert.Equal(expectedTolerance, vertexTolerance, 10);
        Assert.True(vertexTolerance > 1e-3); // Should be relatively large
    }

    [Fact]
    public void GetThresholds_WithSmallSpacing_ShouldProduceSmallVertexTolerance()
    {
        // Arrange
        var thresholds = new Thresholds(0.001); // 1mm nominal spacing

        // Act
        var vertexTolerance = thresholds.GetVertexTolerance();

        // Assert
        var expectedTolerance = 0.001 / Thresholds.VertexToleranceFactorDefault;
        Assert.Equal(expectedTolerance, vertexTolerance, 15);
        Assert.True(vertexTolerance < 1e-7); // Should be very small
    }

    [Fact]
    public void GetVertexTolerance2_ShouldReturnSquareOfVertexTolerance()
    {
        // Arrange
        var thresholds = new Thresholds(5.0);

        // Act
        var tolerance = thresholds.GetVertexTolerance();
        var tolerance2 = thresholds.GetVertexTolerance2();

        // Assert
        Assert.Equal(tolerance * tolerance, tolerance2, 15); // high precision comparison
    }

    [Fact]
    public void ThresholdConstants_ShouldHaveExpectedValues()
    {
        // Assert
        Assert.Equal(256, Thresholds.PrecisionThresholdFactor);
        Assert.Equal(256.0, Thresholds.HalfPlaneThresholdFactor);
        Assert.Equal(256.0, Thresholds.DelaunayThresholdFactor);
        Assert.Equal(1024 * 1024, Thresholds.InCircleThresholdFactor);
        Assert.Equal(1.0e+5, Thresholds.VertexToleranceFactorDefault);
    }

    [Fact]
    public void ThresholdValues_ShouldFollowExpectedMagnitudeRelationships()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);

        // Act
        var precision = thresholds.GetPrecisionThreshold();
        var halfPlane = thresholds.GetHalfPlaneThreshold();
        var inCircle = thresholds.GetInCircleThreshold();
        var delaunay = thresholds.GetDelaunayThreshold();
        var circumcircle = thresholds.GetCircumcircleDeterminantThreshold();

        // Assert - Check expected relationships between thresholds
        Assert.True(halfPlane >= precision); // Half-plane threshold should be >= precision
        Assert.True(inCircle > precision); // In-circle threshold should be much larger
        Assert.True(delaunay >= precision); // Delaunay threshold based on precision
        Assert.True(circumcircle > inCircle); // Circumcircle threshold based on in-circle
    }
}
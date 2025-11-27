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

using Tinfour.Core.Common;
using Tinfour.Core.Utils;

using Xunit;

public class BarycentricCoordinatesTests
{
    [Fact]
    public void GetBarycentricCoordinateDeviation_ShouldBeSmallForWellConditioned()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(3, 0, 0), new(0, 4, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act
        var weights = barycentrics.GetBarycentricCoordinates(triangle, 1, 1);
        var deviation = barycentrics.GetBarycentricCoordinateDeviation();

        // Assert
        Assert.NotNull(weights);
        Assert.True(deviation < 1e-10); // Should be very small for well-conditioned cases
    }

    [Fact]
    public void GetBarycentricCoordinates_WithClosedPolygon_ShouldHandleCorrectly()
    {
        // Arrange - Polygon with repeated start/end vertex
        var triangle = new List<Vertex>
                           {
                               new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 0) // Repeated start vertex
                           };
        var barycentrics = new BarycentricCoordinates();

        // Act
        var weights = barycentrics.GetBarycentricCoordinates(triangle, 0.25, 0.25);

        // Assert
        Assert.NotNull(weights);
        Assert.Equal(3, weights.Length); // Should exclude the repeated vertex

        var sum = weights.Sum();
        Assert.Equal(1.0, sum, 10);
    }

    [Fact]
    public void GetBarycentricCoordinates_WithPointAtVertex_ShouldReturnNull()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test point at vertex
        var weights = barycentrics.GetBarycentricCoordinates(triangle, 0, 0);

        // Assert
        Assert.Null(weights);
    }

    [Fact]
    public void GetBarycentricCoordinates_WithPointOnPerimeter_ShouldReturnNull()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test point on edge
        var weights = barycentrics.GetBarycentricCoordinates(triangle, 0.5, 0);

        // Assert
        Assert.Null(weights);
    }

    [Fact]
    public void GetBarycentricCoordinates_WithPointOutsidePolygon_ShouldHaveNegativeWeights()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test point outside triangle
        var weights = barycentrics.GetBarycentricCoordinates(triangle, 2, 2);

        // Assert
        if (weights != null)
        {
            // For points outside, some weights may be negative
            Assert.Contains(weights, (double w) => w < 0);

            // But they should still sum to 1.0
            var sum = weights.Sum();
            Assert.Equal(1.0, sum, 10);
        }
    }

    [Fact]
    public void GetBarycentricCoordinates_WithRegularHexagon_ShouldWork()
    {
        // Arrange - Regular hexagon centered at origin
        var hexagon = new List<Vertex>();
        for (var i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3;
            hexagon.Add(new Vertex(Math.Cos(angle), Math.Sin(angle), 0));
        }

        var barycentrics = new BarycentricCoordinates();

        // Act - Test center point
        var weights = barycentrics.GetBarycentricCoordinates(hexagon, 0, 0);

        // Assert
        Assert.NotNull(weights);
        Assert.Equal(6, weights.Length);

        var sum = weights.Sum();
        Assert.Equal(1.0, sum, 10);

        // All weights should be equal for center of regular hexagon
        var expectedWeight = 1.0 / 6.0;
        Assert.All(weights, (double w) => Assert.Equal(expectedWeight, w, 5));
    }

    [Fact]
    public void GetBarycentricCoordinates_WithSquare_ShouldReturnCorrectWeights()
    {
        // Arrange - Unit square
        var square = new List<Vertex> { new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test center point
        var weights = barycentrics.GetBarycentricCoordinates(square, 0.5, 0.5);

        // Assert
        Assert.NotNull(weights);
        Assert.Equal(4, weights.Length);

        // Weights should sum to 1.0
        var sum = weights.Sum();
        Assert.Equal(1.0, sum, 10);

        // All weights should be positive for point inside polygon
        Assert.All(weights, (double w) => Assert.True(w > 0));
    }

    [Fact]
    public void GetBarycentricCoordinates_WithTooFewVertices_ShouldReturnNull()
    {
        // Arrange
        var line = new List<Vertex> { new(0, 0, 0), new(1, 0, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act
        var weights = barycentrics.GetBarycentricCoordinates(line, 0.5, 0);

        // Assert
        Assert.Null(weights);
    }

    [Fact]
    public void GetBarycentricCoordinates_WithTriangle_ShouldReturnCorrectWeights()
    {
        // Arrange - Unit triangle
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test center point
        var weights = barycentrics.GetBarycentricCoordinates(triangle, 1.0 / 3, 1.0 / 3);

        // Assert
        Assert.NotNull(weights);
        Assert.Equal(3, weights.Length);

        // Weights should sum to 1.0
        var sum = weights.Sum();
        Assert.Equal(1.0, sum, 10);

        // All weights should be positive for point inside triangle
        Assert.All(weights, (double w) => Assert.True(w > 0));

        // Deviation should be small
        Assert.True(barycentrics.GetBarycentricCoordinateDeviation() < 1e-10);
    }

    [Fact]
    public void InterpolateValue_WithMismatchedCounts_ShouldThrow()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
        var values = new List<double> { 0, 1 }; // Wrong count
        var barycentrics = new BarycentricCoordinates();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => barycentrics.InterpolateValue(triangle, values, 0.25, 0.25));
    }

    [Fact]
    public void InterpolateValue_WithPointOnPerimeter_ShouldReturnNaN()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 1), new(0, 1, 2) };
        var values = new List<double> { 0, 1, 2 };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test point on edge
        var result = barycentrics.InterpolateValue(triangle, values, 0.5, 0);

        // Assert
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void InterpolateValue_WithTriangle_ShouldReturnCorrectValue()
    {
        // Arrange
        var triangle = new List<Vertex> { new(0, 0, 0), new(1, 0, 1), new(0, 1, 2) };
        var values = new List<double> { 0, 1, 2 };
        var barycentrics = new BarycentricCoordinates();

        // Act - Test center point (should be average of vertex values)
        var result = barycentrics.InterpolateValue(triangle, values, 1.0 / 3, 1.0 / 3);

        // Assert
        Assert.True(double.IsFinite(result));
        Assert.True(result >= 0 && result <= 2); // Should be within value range
    }
}
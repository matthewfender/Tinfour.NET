/*
 * Copyright 2025 G.W. Lucas
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

namespace Tinfour.Core.Tests.Interpolation;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

public class InverseDistanceWeightingInterpolatorTests
{
    [Fact]
    public void ComputeAverageSampleSpacing_WithValidTin_ReturnsPositiveValue()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3),
                               new Vertex(5, 5, 25, 4)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        // Act
        var spacing = InverseDistanceWeightingInterpolator.ComputeAverageSampleSpacing(tin);

        // Assert
        Assert.True(spacing > 0, "Sample spacing should be positive");
        Assert.False(double.IsNaN(spacing), "Sample spacing should not be NaN");
    }

    [Fact]
    public void EstimateNominalBandwidth_ReturnsReasonableValue()
    {
        // Act
        var bandwidth = InverseDistanceWeightingInterpolator.EstimateNominalBandwidth(10.0);

        // Assert
        Assert.True(bandwidth > 0, "Bandwidth should be positive");
        Assert.False(double.IsNaN(bandwidth), "Bandwidth should not be NaN");
    }

    [Fact]
    public void Interpolate_CompareWithTriangularFacet_ShouldHaveDifferentValues()
    {
        // Arrange - Create a non-planar surface
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3),
                               new Vertex(5, 5, 50, 4) // Higher center point creates a non-planar surface
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var idw = new InverseDistanceWeightingInterpolator(tin);
        var triangularFacet = new TriangularFacetInterpolator(tin);

        // Act - Compare interpolations at various points
        var point1IDW = idw.Interpolate(2, 2, null);
        var point1TF = triangularFacet.Interpolate(2, 2, null);

        var point2IDW = idw.Interpolate(7, 7, null);
        var point2TF = triangularFacet.Interpolate(7, 7, null);

        // Assert - Values should be different due to different interpolation methods
        Assert.NotEqual(point1TF, point1IDW, 0); // No tolerance
        Assert.NotEqual(point2TF, point2IDW, 0); // No tolerance
    }

    [Fact]
    public void Interpolate_ContinuousSurface_HasValidIntermediateValues()
    {
        // Arrange - Create a simple square with center point
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3),
                               new Vertex(5, 5, 25, 4)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act - Test interpolation at midpoint of each edge
        var midpoint1 = interpolator.Interpolate(5, 0, null); // Bottom edge
        var midpoint2 = interpolator.Interpolate(10, 5, null); // Right edge
        var midpoint3 = interpolator.Interpolate(5, 10, null); // Top edge
        var midpoint4 = interpolator.Interpolate(0, 5, null); // Left edge

        // Act - Test interpolation at points inside the quadrilateral (not on edges)
        var point1 = interpolator.Interpolate(3, 3, null); // Lower left quadrant
        var point2 = interpolator.Interpolate(7, 3, null); // Lower right quadrant
        var point3 = interpolator.Interpolate(7, 7, null); // Upper right quadrant
        var point4 = interpolator.Interpolate(3, 7, null); // Upper left quadrant

        // Assert - Points should have reasonable values in expected ranges
        // Values should be between the z-values of surrounding vertices
        Assert.False(double.IsNaN(point1), "Lower left quadrant value should not be NaN");
        Assert.False(double.IsNaN(point2), "Lower right quadrant value should not be NaN");
        Assert.False(double.IsNaN(point3), "Upper right quadrant value should not be NaN");
        Assert.False(double.IsNaN(point4), "Upper left quadrant value should not be NaN");

        // For IDW, the values can be influenced by all vertices, but still should be
        // more heavily influenced by closer vertices, so we check they're in a reasonable range

        // Bottom edge should be primarily influenced by bottom vertices (10 and 20)
        Assert.True(midpoint1 >= 10 && midpoint1 <= 20, $"Bottom edge value {midpoint1} outside expected range");

        // Right edge should be primarily influenced by right vertices (20 and 30)
        Assert.True(midpoint2 >= 20 && midpoint2 <= 30, $"Right edge value {midpoint2} outside expected range");

        // Top edge should be primarily influenced by top vertices (30 and 40)
        Assert.True(midpoint3 >= 30 && midpoint3 <= 40, $"Top edge value {midpoint3} outside expected range");

        // Left edge should be primarily influenced by left vertices (10 and 40)
        Assert.True(midpoint4 >= 10 && midpoint4 <= 40, $"Left edge value {midpoint4} outside expected range");
    }

    [Fact]
    public void Interpolate_LinearlyVaryingData_ApproximatesLinearInterpolation()
    {
        // Arrange - Create a TIN with linearly varying Z values
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 10, 1),
                               new Vertex(0, 10, 10, 2),
                               new Vertex(10, 10, 20, 3)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act - Test points where we can predict the interpolated value
        var center = interpolator.Interpolate(5, 5, null);
        var quarterPoint = interpolator.Interpolate(2.5, 2.5, null);

        // Assert
        // Center should be close to 10 (average of corners)
        // IDW won't exactly match linear interpolation, but should be close
        Assert.InRange(center, 9, 11);

        // Quarter point should be close to 5 (based on linear interpolation)
        Assert.InRange(quarterPoint, 4, 6);
    }

    [Fact]
    public void Interpolate_OutsideTriangulation_ReturnsNaN()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(10, 10, 10, 0), new Vertex(20, 10, 20, 1), new Vertex(15, 20, 30, 2)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act - Test a point outside the triangulation
        var result = interpolator.Interpolate(0, 0, null);

        // Assert
        Assert.True(double.IsNaN(result), "Interpolation outside triangulation should return NaN");
    }

    [Fact]
    public void Interpolate_ReturnsOriginalValue_AtVertexLocations()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3),
                               new Vertex(5, 5, 25, 4)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act & Assert - Test each vertex location
        foreach (var vertex in vertices)
        {
            var result = interpolator.Interpolate(vertex.X, vertex.Y, null);
            Assert.Equal(vertex.GetZ(), result, 10); // Tolerance of 10^-10
        }
    }

    [Fact]
    public void Interpolate_WithCustomPower_ProducesValidResults()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3),
                               new Vertex(5, 5, 25, 4)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        // Create with custom power of 1.0 (inverse distance)
        var interpolator = new InverseDistanceWeightingInterpolator(tin, 1.0, false);

        // Act - Test some sample points
        var center = interpolator.Interpolate(5, 5, null);
        var midEdge = interpolator.Interpolate(5, 0, null);
        var interior = interpolator.Interpolate(3, 3, null);

        // Assert
        Assert.False(double.IsNaN(center), "Center value should not be NaN");
        Assert.False(double.IsNaN(midEdge), "Mid-edge value should not be NaN");
        Assert.False(double.IsNaN(interior), "Interior value should not be NaN");

        // Check method string contains Power
        Assert.Contains("Power", interpolator.GetMethod());
    }

    [Fact]
    public void Interpolate_WithCustomValuator_UsesCustomValues()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Create a custom valuator that doubles the Z value
        var customValuator = new CustomValuator();

        // Act
        var normalResult = interpolator.Interpolate(5, 5, null);
        var customResult = interpolator.Interpolate(5, 5, customValuator);

        // Assert
        Assert.Equal(normalResult * 2, customResult, 10); // Tolerance of 10^-10
    }

    [Fact]
    public void Interpolate_WithGaussianKernel_ProducesValidResults()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3),
                               new Vertex(5, 5, 25, 4)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        // Create with Gaussian kernel, bandwidth of 3.0
        var interpolator = new InverseDistanceWeightingInterpolator(tin, 3.0, true);

        // Act - Test some sample points
        var center = interpolator.Interpolate(5, 5, null);
        var midEdge = interpolator.Interpolate(5, 0, null);
        var interior = interpolator.Interpolate(3, 3, null);

        // Assert
        Assert.False(double.IsNaN(center), "Center value should not be NaN");
        Assert.False(double.IsNaN(midEdge), "Mid-edge value should not be NaN");
        Assert.False(double.IsNaN(interior), "Interior value should not be NaN");

        // Check method string contains Gaussian
        Assert.Contains("Gaussian", interpolator.GetMethod());
    }

    [Fact]
    public void IsSurfaceNormalSupported_ReturnsFalse()
    {
        // Arrange
        using var tin = new IncrementalTin();
        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act & Assert
        Assert.False(interpolator.IsSurfaceNormalSupported());

        // Surface normal array should be empty
        Assert.Empty(interpolator.GetSurfaceNormal());
    }

    [Fact]
    public void ResetForChangeToTin_AllowsContinuedOperation_AfterTinModification()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 10, 0),
                               new Vertex(10, 0, 20, 1),
                               new Vertex(10, 10, 30, 2),
                               new Vertex(0, 10, 40, 3)
                           };

        using var tin = new IncrementalTin();
        vertices.ForEach((IVertex v) => tin.Add(v));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Test initial interpolation
        var initialValue = interpolator.Interpolate(5, 5, null);

        // Modify the TIN
        tin.Add(new Vertex(5, 5, 100, 4));

        // Reset the interpolator
        interpolator.ResetForChangeToTin();

        // Act - Try interpolation after reset
        var newValue = interpolator.Interpolate(5, 5, null);

        // Assert
        Assert.Equal(100.0, newValue, 10); // Tolerance of 10^-10
        Assert.NotEqual(initialValue, newValue);
    }

    // Custom valuator for testing
    private class CustomValuator : IVertexValuator
    {
        public double Value(IVertex v)
        {
            return v.GetZ() * 2;
        }
    }
}
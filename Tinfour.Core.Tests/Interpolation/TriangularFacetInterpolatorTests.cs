/*
 * Copyright 2023 G.W. Lucas.
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

public class TriangularFacetInterpolatorTests
{
    [Fact]
    public void Constructor_WithNullTin_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TriangularFacetInterpolator(null!));
    }

    [Fact]
    public void Constructor_WithValidTin_ShouldCreateInterpolator()
    {
        // Arrange
        using var tin = new IncrementalTin();

        // Act
        var interpolator = new TriangularFacetInterpolator(tin);

        // Assert
        Assert.NotNull(interpolator);
        Assert.Equal("Triangular Facet", interpolator.GetMethod());
        Assert.True(interpolator.IsSurfaceNormalSupported());
    }

    [Fact]
    public void GetSurfaceNormal_AfterInterpolation_ShouldReturnValidNormal()
    {
        // Arrange
        using var tin = new IncrementalTin();

        // Create a triangle in the XY plane (z=0)
        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(1, 0, 0, 2);
        var v3 = new Vertex(0, 1, 0, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);

        var interpolator = new TriangularFacetInterpolator(tin);

        // Act
        interpolator.Interpolate(0.3, 0.3, null);
        var normal = interpolator.GetSurfaceNormal();

        // Assert - should be approximately (0, 0, 1) for flat XY plane
        Assert.Equal(3, normal.Length);
        Assert.True(Math.Abs(normal[0] - 0) < 0.001); // nx ≈ 0
        Assert.True(Math.Abs(normal[1] - 0) < 0.001); // ny ≈ 0
        Assert.True(Math.Abs(normal[2] - 1) < 0.001); // nz ≈ 1
    }

    [Fact]
    public void Interpolate_AtExactVertex_ShouldReturnVertexValue()
    {
        // Arrange
        using var tin = new IncrementalTin();

        var v1 = new Vertex(0, 0, 10, 1);
        var v2 = new Vertex(2, 0, 20, 2);
        var v3 = new Vertex(1, 2, 30, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);

        var interpolator = new TriangularFacetInterpolator(tin);

        // Act - interpolate exactly at vertex positions
        var result1 = interpolator.Interpolate(0, 0, null);
        var result2 = interpolator.Interpolate(2, 0, null);
        var result3 = interpolator.Interpolate(1, 2, null);

        // Assert - should return exact vertex values
        Assert.True(Math.Abs(result1 - 10) < 0.001);
        Assert.True(Math.Abs(result2 - 20) < 0.001);
        Assert.True(Math.Abs(result3 - 30) < 0.001);
    }

    [Fact]
    public void Interpolate_WithCustomValuator_ShouldUseCustomValues()
    {
        // Arrange
        using var tin = new IncrementalTin();

        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(2, 0, 0, 2);
        var v3 = new Vertex(1, 2, 0, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);

        var interpolator = new TriangularFacetInterpolator(tin);

        // Create a custom valuator that returns constant value
        var customValuator = new TestVertexValuator(5.0);

        // Act
        var result = interpolator.Interpolate(1.0, 0.666666667, customValuator);

        // Assert - should return the constant value from custom valuator
        Assert.True(Math.Abs(result - 5.0) < 0.001);
    }

    [Fact]
    public void Interpolate_WithInclinedPlane_ShouldReturnCorrectValue()
    {
        // Arrange
        using var tin = new IncrementalTin();

        // Create a triangle forming an inclined plane: z = x
        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(2, 0, 2, 2);
        var v3 = new Vertex(1, 2, 1, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);

        var interpolator = new TriangularFacetInterpolator(tin);

        // Act - interpolate at various points where z should equal x
        var result1 = interpolator.Interpolate(0.5, 1.0, null);
        var result2 = interpolator.Interpolate(1.5, 0.5, null);
        var result3 = interpolator.Interpolate(1.0, 1.0, null);

        // Assert - z should approximately equal x
        Assert.True(Math.Abs(result1 - 0.5) < 0.001);
        Assert.True(Math.Abs(result2 - 1.5) < 0.001);
        Assert.True(Math.Abs(result3 - 1.0) < 0.001);
    }

    [Fact]
    public void Interpolate_WithNonBootstrappedTin_ShouldReturnNaN()
    {
        // Arrange
        using var tin = new IncrementalTin();
        var interpolator = new TriangularFacetInterpolator(tin);

        // Act
        var result = interpolator.Interpolate(1.0, 1.0, null);

        // Assert
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void Interpolate_WithSimpleTriangle_ShouldReturnCorrectValue()
    {
        // Arrange
        using var tin = new IncrementalTin();

        // Create a simple triangle at z=0 plane
        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(2, 0, 0, 2);
        var v3 = new Vertex(1, 2, 0, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);

        var interpolator = new TriangularFacetInterpolator(tin);

        // Act - interpolate at the center of the triangle
        var result = interpolator.Interpolate(1.0, 0.666666667, null);

        // Assert - should be close to 0 since all vertices have z=0
        Assert.True(Math.Abs(result) < 0.001);
    }

    [Fact]
    public void InterpolateWithExteriorSupport_OutsideTin_ShouldNotReturnNaN()
    {
        // Arrange
        using var tin = new IncrementalTin();

        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(2, 0, 2, 2);
        var v3 = new Vertex(1, 2, 1, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);

        var interpolator = new TriangularFacetInterpolator(tin);

        // Act - interpolate outside the TIN convex hull
        var result = interpolator.InterpolateWithExteriorSupport(-1.0, -1.0, null);

        // Assert - should return a finite value, not NaN
        Assert.False(double.IsNaN(result));
        Assert.True(double.IsFinite(result));
    }

    [Fact]
    public void ResetForChangeToTin_ShouldNotThrow()
    {
        // Arrange
        using var tin = new IncrementalTin();
        var interpolator = new TriangularFacetInterpolator(tin);

        // Act & Assert - should not throw
        interpolator.ResetForChangeToTin();
    }

    private class TestVertexValuator : IVertexValuator
    {
        private readonly double _constantValue;

        public TestVertexValuator(double constantValue)
        {
            this._constantValue = constantValue;
        }

        public double Value(IVertex v)
        {
            return this._constantValue;
        }
    }
}
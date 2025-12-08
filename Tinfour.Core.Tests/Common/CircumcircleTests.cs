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

public class CircumcircleTests
{
    [Fact]
    public void Compute_WithCollinearPoints_ShouldReturnInfiniteCircumcircle()
    {
        // Arrange - Three points on a line
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(2, 0, 0);
        var circumcircle = new Circumcircle();

        // Act
        var result = circumcircle.Compute(a, b, c);

        // Assert
        Assert.False(result);
        Assert.Equal(double.PositiveInfinity, circumcircle.GetX());
        Assert.Equal(double.PositiveInfinity, circumcircle.GetY());
        Assert.Equal(double.PositiveInfinity, circumcircle.GetRadiusSq());
    }

    [Fact]
    public void Compute_WithEquilateralTriangle_ShouldCalculateCorrectCircumcircle()
    {
        // Arrange - Equilateral triangle centered at origin
        var a = new Vertex(1, 0, 0);
        var b = new Vertex(-0.5, Math.Sqrt(3) / 2, 0);
        var c = new Vertex(-0.5, -Math.Sqrt(3) / 2, 0);
        var circumcircle = new Circumcircle();

        // Act
        var result = circumcircle.Compute(a, b, c);

        // Assert
        Assert.True(result);
        Assert.Equal(0.0, circumcircle.GetX(), 2);
        Assert.Equal(0.0, circumcircle.GetY(), 2);
        Assert.Equal(1.0, circumcircle.GetRadius(), 2); // circumradius of unit equilateral triangle
    }

    [Fact]
    public void Compute_WithNullVertices_ShouldReturnInfiniteCircumcircle()
    {
        // Arrange
        var circumcircle = new Circumcircle();

        // Act
        var result = circumcircle.Compute(Vertex.Null, Vertex.Null, Vertex.Null);

        // Assert
        Assert.False(result);
        Assert.Equal(double.PositiveInfinity, circumcircle.GetX());
        Assert.Equal(double.PositiveInfinity, circumcircle.GetY());
        Assert.Equal(double.PositiveInfinity, circumcircle.GetRadiusSq());
    }

    [Fact]
    public void Compute_WithValidTriangle_ShouldCalculateCorrectCircumcircle()
    {
        // Arrange - Right triangle with vertices at (0,0), (3,0), (0,4)
        // Expected circumcenter at (1.5, 2) with radius 2.5
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(3, 0, 0);
        var c = new Vertex(0, 4, 0);
        var circumcircle = new Circumcircle();

        // Act
        var result = circumcircle.Compute(a, b, c);

        // Assert
        Assert.True(result);
        Assert.Equal(1.5, circumcircle.GetX(), 3); // tolerance of 3 decimal places
        Assert.Equal(2.0, circumcircle.GetY(), 3);
        Assert.Equal(2.5, circumcircle.GetRadius(), 3);
        Assert.Equal(6.25, circumcircle.GetRadiusSq(), 3);
    }

    [Fact]
    public void ComputeCircumcircle_StaticMethod_ShouldReturnValidCircumcircle()
    {
        // Arrange
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(4, 0, 0);
        var c = new Vertex(2, 3, 0);

        // Act
        var circumcircle = Circumcircle.ComputeCircumcircle(a, b, c);

        // Assert
        Assert.NotNull(circumcircle);
        Assert.True(double.IsFinite(circumcircle.GetX()));
        Assert.True(double.IsFinite(circumcircle.GetY()));
        Assert.True(double.IsFinite(circumcircle.GetRadiusSq()));
        Assert.Equal(2.0, circumcircle.GetX(), 2);
    }

    [Fact]
    public void Copy_ShouldCopyAllValues()
    {
        // Arrange
        var original = new Circumcircle();
        original.SetCircumcenter(1.0, 2.0, 9.0);
        var copy = new Circumcircle();

        // Act
        copy.Copy(original);

        // Assert
        Assert.Equal(original.GetX(), copy.GetX());
        Assert.Equal(original.GetY(), copy.GetY());
        Assert.Equal(original.GetRadiusSq(), copy.GetRadiusSq());
    }

    [Fact]
    public void GetBounds_ShouldReturnCorrectBoundingRectangle()
    {
        // Arrange
        var circumcircle = new Circumcircle();
        circumcircle.SetCircumcenter(10.0, 20.0, 25.0); // radius = 5

        // Act
        var bounds = circumcircle.GetBounds();

        // Assert
        Assert.Equal(5.0, bounds.Left); // centerX - radius
        Assert.Equal(15.0, bounds.Top); // centerY - radius  
        Assert.Equal(10.0, bounds.Width); // 2 * radius
        Assert.Equal(10.0, bounds.Height); // 2 * radius
    }

    [Fact]
    public void SetCircumcenter_ShouldSetValuesCorrectly()
    {
        // Arrange
        var circumcircle = new Circumcircle();

        // Act
        circumcircle.SetCircumcenter(5.0, 10.0, 25.0);

        // Assert
        Assert.Equal(5.0, circumcircle.GetX());
        Assert.Equal(10.0, circumcircle.GetY());
        Assert.Equal(25.0, circumcircle.GetRadiusSq());
        Assert.Equal(5.0, circumcircle.GetRadius());
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var circumcircle = new Circumcircle();
        circumcircle.SetCircumcenter(1.5, 2.5, 6.25);

        // Act
        var result = circumcircle.ToString();

        // Assert
        Assert.Contains("1.5000", result);
        Assert.Contains("2.5000", result);
        Assert.Contains("2.5000", result); // radius = sqrt(6.25) = 2.5
    }
}
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

namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;

using Xunit;

public class PolygonConstraintTests
{
    [Fact]
    public void ApplicationData_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        var constraint = new PolygonConstraint(
            [
                new Vertex(0, 0, 0),
                new Vertex(1, 0, 0),
                new Vertex(0, 1, 0)
            ]);
        var testData = "Test Polygon Data";

        // Act
        constraint.SetApplicationData(testData);

        // Assert
        Assert.Equal(testData, constraint.GetApplicationData());
    }

    [Fact]
    public void Constructor_WithEmptyVertices_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PolygonConstraint(Array.Empty<IVertex>()));
    }

    [Fact]
    public void Constructor_WithIsHoleTrue_ShouldCreateHoleConstraint()
    {
        // Arrange
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0) };

        // Act
        var constraint = new PolygonConstraint(vertices, isHole: true);

        // Assert
        Assert.True(constraint.IsHole());
        Assert.True(constraint.DefinesConstrainedRegion());
    }

    [Fact]
    public void Constructor_WithMultipleVertices_ShouldCreateValidPolygon()
    {
        // Arrange - Square
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0)
                           };

        // Act
        var constraint = new PolygonConstraint(vertices);

        // Assert
        Assert.Equal(4, constraint.GetVertices().Count);
        Assert.Equal(4, constraint.GetEdgeCount());
        Assert.True(constraint.DefinesConstrainedRegion());
        Assert.False(constraint.IsHole());
    }

    [Fact]
    public void Constructor_WithThreeVertices_ShouldCreateValidTriangle()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(1, 0, 0);
        var v3 = new Vertex(0, 1, 0);

        // Act
        var constraint = new PolygonConstraint([v1, v2, v3]);

        // Assert
        Assert.Equal(3, constraint.GetVertices().Count);
        Assert.Equal(3, constraint.GetEdgeCount());
        Assert.True(constraint.DefinesConstrainedRegion());
        Assert.False(constraint.IsHole());
        Assert.Equal(-1, constraint.GetConstraintIndex());
    }

    [Fact]
    public void Constructor_WithTwoVertices_ShouldThrowArgumentException()
    {
        // Arrange
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 1, 0) };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PolygonConstraint(vertices));
    }

    [Fact]
    public void GetArea_ShouldReturnAbsoluteArea()
    {
        // Arrange - Unit square in clockwise order (negative signed area)
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(0, 1, 0), new Vertex(1, 1, 0), new Vertex(1, 0, 0)
                           };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var area = constraint.GetArea();

        // Assert
        Assert.True(area > 0);
        Assert.Equal(1.0, area, 3);
    }

    [Fact]
    public void GetPerimeter_WithTriangle_ShouldReturnCorrectValue()
    {
        // Arrange - 3-4-5 right triangle
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(3, 0, 0), new Vertex(0, 4, 0) };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var perimeter = constraint.GetPerimeter();

        // Assert
        Assert.Equal(12.0, perimeter, 3); // 3 + 4 + 5 = 12
    }

    [Fact]
    public void GetPerimeter_WithUnitSquare_ShouldReturnFour()
    {
        // Arrange - Unit square
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0)
                           };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var perimeter = constraint.GetPerimeter();

        // Assert
        Assert.Equal(4.0, perimeter, 3); // 1 + 1 + 1 + 1 = 4
    }

    [Fact]
    public void GetSignedArea_WithClockwiseSquare_ShouldReturnNegativeArea()
    {
        // Arrange - Unit square in clockwise order
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(0, 1, 0), new Vertex(1, 1, 0), new Vertex(1, 0, 0)
                           };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var signedArea = constraint.GetSignedArea();

        // Assert
        Assert.True(signedArea < 0);
        Assert.Equal(1.0, Math.Abs(signedArea), 3);
    }

    [Fact]
    public void GetSignedArea_WithCounterclockwiseSquare_ShouldReturnPositiveArea()
    {
        // Arrange - Unit square in counterclockwise order
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0)
                           };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var signedArea = constraint.GetSignedArea();

        // Assert
        Assert.True(signedArea > 0);
        Assert.Equal(1.0, Math.Abs(signedArea), 3);
    }

    [Fact]
    public void GetSignedArea_WithTriangle_ShouldCalculateCorrectArea()
    {
        // Arrange - Right triangle with area = 0.5
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(0, 1, 0) };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var area = constraint.GetSignedArea();

        // Assert
        Assert.Equal(0.5, Math.Abs(area), 3);
    }

    // [Fact]
    // public void ToString_ShouldIncludePolygonType()
    // {
    // // Arrange
    // var vertices = new[]
    // {
    // new Vertex(0, 0, 0),
    // new Vertex(1, 0, 0),
    // new Vertex(1, 1, 0),
    // new Vertex(0, 1, 0)
    // };
    // var constraint = new PolygonConstraint(vertices);

    // // Act
    // string result = constraint.ToString();

    // // Assert
    // Assert.Contains("PolygonConstraint", result);
    // Assert.Contains("Region", result); // Not a hole
    // Assert.Contains("4 vertices", result);
    // }

    // [Fact]
    // public void ToString_WithHole_ShouldIndicateHoleType()
    // {
    // // Arrange
    // var vertices = new[]
    // {
    // new Vertex(0, 0, 0),
    // new Vertex(1, 0, 0),
    // new Vertex(0, 1, 0)
    // };
    // var constraint = new PolygonConstraint(vertices, isHole: true);

    // // Act
    // string result = constraint.ToString();

    // // Assert
    // Assert.Contains("Hole", result);
    // }
    [Fact]
    public void GetVertices_ShouldReturnReadOnlyList()
    {
        // Arrange
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(0, 1, 0) };
        var constraint = new PolygonConstraint(vertices);

        // Act
        var returnedVertices = constraint.GetVertices();

        // Assert
        Assert.IsAssignableFrom<IList<Vertex>>(returnedVertices);
        Assert.Equal(3, returnedVertices.Count);

        // Verify it's read-only
        Assert.Throws<NotSupportedException>(() => returnedVertices.Add(new Vertex(2, 2, 0)));
    }

    [Fact]
    public void IsCounterclockwise_WithClockwiseVertices_ShouldReturnFalse()
    {
        // Arrange - Unit square in clockwise order
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(0, 1, 0), new Vertex(1, 1, 0), new Vertex(1, 0, 0)
                           };
        var constraint = new PolygonConstraint(vertices);

        // Act & Assert
        Assert.False(constraint.IsCounterclockwise());
    }

    [Fact]
    public void IsCounterclockwise_WithCounterclockwiseVertices_ShouldReturnTrue()
    {
        // Arrange - Unit square in counterclockwise order
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0)
                           };
        var constraint = new PolygonConstraint(vertices);

        // Act & Assert
        Assert.True(constraint.IsCounterclockwise());
    }
}
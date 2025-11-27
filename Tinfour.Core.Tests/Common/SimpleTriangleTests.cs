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
using Tinfour.Core.Edge;

using Xunit;

public class SimpleTriangleTests
{
    [Fact]
    public void Constructor_WithSingleEdge_ShouldCreateTriangleFromLinkedEdges()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        // Set up edge links to form a triangle
        e1.SetForward(e2);
        e1.SetReverse(e3);

        // Act
        var triangle = new SimpleTriangle(e1);

        // Assert
        Assert.Same(e1, triangle.GetEdgeA());
        Assert.Same(e2, triangle.GetEdgeB());
        Assert.Same(e3, triangle.GetEdgeC());
    }

    [Fact]
    public void Constructor_WithThreeEdges_ShouldCreateTriangle()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        // Act
        var triangle = new SimpleTriangle(e1, e2, e3);

        // Assert
        Assert.Same(e1, triangle.GetEdgeA());
        Assert.Same(e2, triangle.GetEdgeB());
        Assert.Same(e3, triangle.GetEdgeC());
        Assert.Equal(10, triangle.GetIndex()); // minimum of edge indices
    }

    [Fact]
    public void GetArea_WithClockwiseTriangle_ShouldReturnNegativeArea()
    {
        // Arrange - Same triangle but in clockwise order
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(0, 3, 0);
        var v3 = new Vertex(4, 0, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act
        var area = triangle.GetArea();

        // Assert
        Assert.Equal(-6.0, area, 3); // negative for clockwise
    }

    [Fact]
    public void GetArea_WithCounterclockwiseTriangle_ShouldReturnPositiveArea()
    {
        // Arrange - Right triangle with area = 6
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(4, 0, 0);
        var v3 = new Vertex(0, 3, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act
        var area = triangle.GetArea();

        // Assert
        Assert.Equal(6.0, area, 3); // tolerance of 3 decimal places
    }

    [Fact]
    public void GetCentroid_ShouldReturnAverageOfVertices()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 10);
        var v2 = new Vertex(6, 0, 20);
        var v3 = new Vertex(0, 9, 30);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act
        var centroid = triangle.GetCentroid();

        // Assert
        Assert.Equal(2.0, centroid.X, 3); // (0+6+0)/3 = 2
        Assert.Equal(3.0, centroid.Y, 3); // (0+0+9)/3 = 3
        Assert.Equal(20.0, centroid.GetZ(), 3); // (10+20+30)/3 = 20
        Assert.True(centroid.IsSynthetic());
    }

    [Fact]
    public void GetCircumcircle_CalledTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act
        var circumcircle1 = triangle.GetCircumcircle();
        var circumcircle2 = triangle.GetCircumcircle();

        // Assert
        Assert.Same(circumcircle1, circumcircle2);
    }

    [Fact]
    public void GetCircumcircle_ShouldReturnValidCircumcircle()
    {
        // Arrange - Right triangle
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act
        var circumcircle = triangle.GetCircumcircle();

        // Assert
        Assert.NotNull(circumcircle);
        Assert.Equal(1.5, circumcircle.GetX(), 2);
        Assert.Equal(2.0, circumcircle.GetY(), 2);
        Assert.Equal(2.5, circumcircle.GetRadius(), 2);
    }

    [Fact]
    public void GetIndex_ShouldReturnMinimumEdgeIndex()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(50);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act & Assert
        Assert.Equal(20, triangle.GetIndex()); // minimum of 50, 20, 30
    }

    [Fact]
    public void GetVertices_ShouldReturnCorrectVertices()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act & Assert
        // Note: Vertex naming follows trigonometric convention where
        // vertex A is opposite edge a, etc.
        Assert.Equal(v3, triangle.GetVertexA()); // opposite to edge A (e1)
        Assert.Equal(v1, triangle.GetVertexB()); // opposite to edge B (e2)  
        Assert.Equal(v2, triangle.GetVertexC()); // opposite to edge C (e3)
    }

    [Fact]
    public void IsGhost_WithAllRealVertices_ShouldReturnFalse()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);
        var v3 = new Vertex(0, 4, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act & Assert
        Assert.False(triangle.IsGhost());
    }

    [Fact]
    public void IsGhost_WithGhostVertex_ShouldReturnTrue()
    {
        // Arrange
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(3, 0, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, Vertex._NullVertex); // ghost vertex
        e3.SetVertices(Vertex._NullVertex, v1); // ghost vertex

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act & Assert
        Assert.True(triangle.IsGhost());
    }

    [Fact]
    public void ToString_ShouldIncludeVertexCoordinatesAndIndex()
    {
        // Arrange
        var v1 = new Vertex(1, 2, 0);
        var v2 = new Vertex(3, 4, 0);
        var v3 = new Vertex(5, 6, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(v1, v2);
        e2.SetVertices(v2, v3);
        e3.SetVertices(v3, v1);

        var triangle = new SimpleTriangle(e1, e2, e3);

        // Act
        var result = triangle.ToString();

        // Assert
        Assert.Contains("Triangle", result);
        Assert.Contains("10", result); // index
        Assert.Contains("5.00", result); // vertex coordinates
        Assert.Contains("6.00", result);
    }
}
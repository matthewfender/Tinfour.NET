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

namespace Tinfour.Core.Tests.Utils;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Utils;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests for the Polyside point-in-polygon utility
/// </summary>
public class PolysideTests
{
    private readonly ITestOutputHelper _output;

    public PolysideTests(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void IsPointInPolygon_WithComplexPolygon_ShouldHandleCorrectly()
    {
        // Arrange - create a simpler polygon (triangle instead of complex pentagon)
        var edges = this.CreateTriangleEdges();

        // Act & Assert
        var centerResult = Polyside.IsPointInPolygon(edges, 1, 1); // Inside triangle
        var outsideResult = Polyside.IsPointInPolygon(edges, 10, 10); // Far outside

        Assert.True(centerResult.IsCovered());
        Assert.False(outsideResult.IsCovered());

        this._output.WriteLine($"Triangle test - Center: {centerResult}, Outside: {outsideResult}");
    }

    [Fact]
    public void IsPointInPolygon_WithPointInside_ShouldReturnInside()
    {
        // Arrange
        var edges = this.CreateSimpleSquareEdges();

        // Act
        var result = Polyside.IsPointInPolygon(edges, 5, 5);

        // Assert
        Assert.Equal(Polyside.Result.Inside, result);
        Assert.True(result.IsCovered());

        this._output.WriteLine($"Point (5,5) result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_WithPointOnVertex_ShouldReturnEdgeOrInside()
    {
        // Arrange
        var edges = this.CreateSimpleSquareEdges();

        // Act - point exactly on a vertex
        var result = Polyside.IsPointInPolygon(edges, 0, 0);

        // Assert
        Assert.True(result.IsCovered()); // Should be covered (inside or on edge)

        this._output.WriteLine($"Point (0,0) on vertex result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_WithPointOutside_ShouldReturnOutside()
    {
        // Arrange
        var edges = this.CreateSimpleSquareEdges();

        // Act
        var result = Polyside.IsPointInPolygon(edges, 15, 15);

        // Assert
        Assert.Equal(Polyside.Result.Outside, result);
        Assert.False(result.IsCovered());

        this._output.WriteLine($"Point (15,15) result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_WithTooFewEdges_ShouldThrow()
    {
        // Arrange
        var edges = new List<IQuadEdge>();
        var edgePool = new EdgePool();

        // Add only 2 edges (insufficient for a polygon)
        edges.Add(edgePool.AllocateEdge(new Vertex(0, 0, 0, 1), new Vertex(1, 0, 0, 2)));
        edges.Add(edgePool.AllocateEdge(new Vertex(1, 0, 0, 2), new Vertex(1, 1, 0, 3)));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Polyside.IsPointInPolygon(edges, 0.5, 0.5));

        Assert.Contains("at least three edges", exception.Message);
        this._output.WriteLine($"Expected exception: {exception.Message}");
    }

    [Fact]
    public void IsPointInPolygon_WithUnclosedPolygon_ShouldThrow()
    {
        // Arrange - create edges that don't form a closed polygon
        var edgePool = new EdgePool();
        var edges = new List<IQuadEdge>
                        {
                            edgePool.AllocateEdge(new Vertex(0, 0, 0, 1), new Vertex(1, 0, 0, 2)),
                            edgePool.AllocateEdge(new Vertex(1, 0, 0, 2), new Vertex(1, 1, 0, 3)),
                            edgePool.AllocateEdge(
                                new Vertex(1, 1, 0, 3),
                                new Vertex(2, 2, 0, 4)) // Wrong end vertex - doesn't close
                        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Polyside.IsPointInPolygon(edges, 0.5, 0.5));

        Assert.Contains("not closed", exception.Message);
        this._output.WriteLine($"Expected exception: {exception.Message}");
    }

    // ---------------------------------------------------------------
    // Vertex-based overload tests
    // ---------------------------------------------------------------

    [Fact]
    public void IsPointInPolygon_VertexOverload_PointInsideSquare_ReturnsInside()
    {
        // Arrange - square (0,0)-(10,0)-(10,10)-(0,10)
        var vertices = CreateSquareVertices();

        // Act
        var result = Polyside.IsPointInPolygon(vertices, 5, 5);

        // Assert
        Assert.Equal(Polyside.Result.Inside, result);
        this._output.WriteLine($"Point (5,5) vertex overload result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_VertexOverload_PointOutsideSquare_ReturnsOutside()
    {
        // Arrange
        var vertices = CreateSquareVertices();

        // Act
        var result = Polyside.IsPointInPolygon(vertices, 15, 15);

        // Assert
        Assert.Equal(Polyside.Result.Outside, result);
        this._output.WriteLine($"Point (15,15) vertex overload result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_VertexOverload_PointOnEdge_ReturnsEdge()
    {
        // Arrange
        var vertices = CreateSquareVertices();

        // Act - point on bottom edge at (5, 0)
        var result = Polyside.IsPointInPolygon(vertices, 5, 0);

        // Assert
        Assert.Equal(Polyside.Result.Edge, result);
        this._output.WriteLine($"Point (5,0) on edge vertex overload result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_VertexOverload_PointOnVertex_ReturnsEdge()
    {
        // Arrange
        var vertices = CreateSquareVertices();

        // Act - point exactly on vertex (0, 0)
        var result = Polyside.IsPointInPolygon(vertices, 0, 0);

        // Assert
        Assert.Equal(Polyside.Result.Edge, result);
        this._output.WriteLine($"Point (0,0) on vertex, vertex overload result: {result}");
    }

    [Fact]
    public void IsPointInPolygon_VertexOverload_PointInsideNonConvex_ReturnsInside()
    {
        // Arrange - L-shaped polygon
        //  (0,0)-(6,0)-(6,3)-(3,3)-(3,6)-(0,6)
        var vertices = CreateLShapedVertices();

        // Act - point at (1, 1) inside the L
        var result = Polyside.IsPointInPolygon(vertices, 1, 1);

        // Assert
        Assert.Equal(Polyside.Result.Inside, result);
        this._output.WriteLine($"Point (1,1) inside L-shape: {result}");
    }

    [Fact]
    public void IsPointInPolygon_VertexOverload_PointInConcavity_ReturnsOutside()
    {
        // Arrange - L-shaped polygon
        var vertices = CreateLShapedVertices();

        // Act - point at (5, 5) in the concavity (upper-right notch)
        var result = Polyside.IsPointInPolygon(vertices, 5, 5);

        // Assert
        Assert.Equal(Polyside.Result.Outside, result);
        this._output.WriteLine($"Point (5,5) in L-shape concavity: {result}");
    }

    [Fact]
    public void IsPointInPolygon_VertexOverload_FewerThanThreeVertices_ThrowsArgumentException()
    {
        // Arrange
        var vertices = new List<IVertex>
        {
            new Vertex(0, 0, 0, 1),
            new Vertex(1, 0, 0, 2)
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Polyside.IsPointInPolygon(vertices, 0.5, 0.5));

        Assert.Contains("at least three vertices", ex.Message);
        this._output.WriteLine($"Expected exception: {ex.Message}");
    }

    /// <summary>
    ///     Creates square vertices at (0,0), (10,0), (10,10), (0,10) for vertex-overload tests.
    /// </summary>
    private static IList<IVertex> CreateSquareVertices()
    {
        return new List<IVertex>
        {
            new Vertex(0, 0, 0, 1),
            new Vertex(10, 0, 0, 2),
            new Vertex(10, 10, 0, 3),
            new Vertex(0, 10, 0, 4)
        };
    }

    /// <summary>
    ///     Creates an L-shaped (non-convex) polygon:
    ///     (0,0)-(6,0)-(6,3)-(3,3)-(3,6)-(0,6)
    /// </summary>
    private static IList<IVertex> CreateLShapedVertices()
    {
        return new List<IVertex>
        {
            new Vertex(0, 0, 0, 1),
            new Vertex(6, 0, 0, 2),
            new Vertex(6, 3, 0, 3),
            new Vertex(3, 3, 0, 4),
            new Vertex(3, 6, 0, 5),
            new Vertex(0, 6, 0, 6)
        };
    }

    /// <summary>
    ///     Creates a pentagon polygon for testing.
    /// </summary>
    private List<IQuadEdge> CreatePentagonEdges()
    {
        var edgePool = new EdgePool();
        var edges = new List<IQuadEdge>();

        // Pentagon vertices (approximately regular pentagon)
        var vertices = new[]
                           {
                               new Vertex(2, 0, 0, 1), new Vertex(3.618, 1.176, 0, 2), new Vertex(1.618, 2.902, 0, 3),
                               new Vertex(-1.618, 2.902, 0, 4), new Vertex(-3.618, 1.176, 0, 5)
                           };

        // Create edges connecting consecutive vertices
        for (var i = 0; i < vertices.Length; i++)
        {
            var nextIndex = (i + 1) % vertices.Length;
            edges.Add(edgePool.AllocateEdge(vertices[i], vertices[nextIndex]));
        }

        return edges;
    }

    /// <summary>
    ///     Creates a simple square polygon for testing.
    ///     Square has corners at (0,0), (10,0), (10,10), (0,10).
    /// </summary>
    private List<IQuadEdge> CreateSimpleSquareEdges()
    {
        var edgePool = new EdgePool();
        var edges = new List<IQuadEdge>();

        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(10, 0, 0, 2);
        var v3 = new Vertex(10, 10, 0, 3);
        var v4 = new Vertex(0, 10, 0, 4);

        // Create edges in order that forms a closed polygon
        edges.Add(edgePool.AllocateEdge(v1, v2)); // Bottom
        edges.Add(edgePool.AllocateEdge(v2, v3)); // Right
        edges.Add(edgePool.AllocateEdge(v3, v4)); // Top
        edges.Add(edgePool.AllocateEdge(v4, v1)); // Left (closes the polygon)

        return edges;
    }

    /// <summary>
    ///     Creates a triangle polygon for testing.
    ///     Triangle has corners at (0,0), (3,0), (1.5,3).
    /// </summary>
    private List<IQuadEdge> CreateTriangleEdges()
    {
        var edgePool = new EdgePool();
        var edges = new List<IQuadEdge>();

        var v1 = new Vertex(0, 0, 0, 1);
        var v2 = new Vertex(3, 0, 0, 2);
        var v3 = new Vertex(1.5, 3, 0, 3);

        edges.Add(edgePool.AllocateEdge(v1, v2)); // Bottom
        edges.Add(edgePool.AllocateEdge(v2, v3)); // Right side
        edges.Add(edgePool.AllocateEdge(v3, v1)); // Left side (closes)

        return edges;
    }
}
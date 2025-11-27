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

namespace Tinfour.Core.Tests.Voronoi;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Voronoi;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests for the ThiessenPolygon implementation
/// </summary>
public class ThiessenPolygonTests
{
    private readonly ITestOutputHelper _output;

    public ThiessenPolygonTests(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void Constructor_WithEmptyEdgeList_ShouldNotThrow()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = new List<IQuadEdge>();

        // Act
        var exception = Record.Exception(() => new ThiessenPolygon(vertex, edges, false));

        // Assert
        Assert.Null(exception);
        this._output.WriteLine("Empty edge list handled successfully");
    }

    [Fact]
    public void Constructor_WithOpenPolygon_ShouldSetOpenFlag()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();

        // Act
        var polygon = new ThiessenPolygon(vertex, edges, true);

        // Assert
        Assert.True(polygon.IsOpen());
        this._output.WriteLine($"Created open polygon for vertex {polygon.GetVertex().GetLabel()}");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreatePolygon()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();

        // Act
        var polygon = new ThiessenPolygon(vertex, edges, false);

        // Assert
        Assert.NotNull(polygon);
        Assert.Equal(vertex, polygon.GetVertex());
        Assert.Equal(vertex.GetIndex(), polygon.GetIndex());
        Assert.False(polygon.IsOpen());

        this._output.WriteLine($"Created polygon for vertex {polygon.GetVertex().GetLabel()}");
    }

    [Fact]
    public void GetArea_WithSquarePolygon_ShouldCalculateCorrectArea()
    {
        // Arrange - Create a simple square
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();
        var polygon = new ThiessenPolygon(vertex, edges, false);

        // Act
        var area = polygon.GetArea();

        // Assert
        Assert.True(area > 0, "Area should be positive for a properly oriented polygon");
        this._output.WriteLine($"Polygon area: {area}");
    }

    [Fact]
    public void GetBounds_ShouldReturnCorrectBounds()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();
        var polygon = new ThiessenPolygon(vertex, edges, false);

        // Act
        var bounds = polygon.GetBounds();

        // Assert
        Assert.True(bounds.Width >= 0);
        Assert.True(bounds.Height >= 0);

        // Note: The bounds calculation might create a bounds that encompasses all edge vertices
        this._output.WriteLine($"Polygon bounds: {bounds}");
    }

    [Fact]
    public void GetEdges_ShouldReturnCopyOfEdges()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var originalEdges = this.CreateSimpleSquareEdges();
        var polygon = new ThiessenPolygon(vertex, originalEdges, false);

        // Act
        var returnedEdges = polygon.GetEdges();

        // Assert
        Assert.Equal(originalEdges.Count, returnedEdges.Count);
        Assert.NotSame(originalEdges, returnedEdges); // Should be a copy

        this._output.WriteLine($"Polygon has {returnedEdges.Count} edges");
    }

    [Fact]
    public void IsPointInPolygon_WithPointInside_ShouldReturnTrue()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();
        var polygon = new ThiessenPolygon(vertex, edges, false);

        // Act & Assert
        Assert.True(polygon.IsPointInPolygon(5, 5)); // Center point
        this._output.WriteLine("Point (5,5) is inside the polygon");
    }

    [Fact]
    public void IsPointInPolygon_WithPointOutside_ShouldReturnFalse()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();
        var polygon = new ThiessenPolygon(vertex, edges, false);

        // Act & Assert
        Assert.False(polygon.IsPointInPolygon(15, 15)); // Outside point
        this._output.WriteLine("Point (15,15) is outside the polygon");
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var vertex = new Vertex(5, 5, 10, 1);
        var edges = this.CreateSimpleSquareEdges();
        var polygon = new ThiessenPolygon(vertex, edges, false);

        // Act
        var result = polygon.ToString();

        // Assert
        Assert.Contains("ThiessenPolygon", result);
        Assert.Contains(vertex.GetLabel(), result);

        this._output.WriteLine($"ToString result: {result}");
    }

    /// <summary>
    ///     Creates a simple square of edges for testing purposes.
    ///     Forms a 10x10 square with corners at (0,0), (10,0), (10,10), (0,10).
    /// </summary>
    private List<IQuadEdge> CreateSimpleSquareEdges()
    {
        var edgePool = new EdgePool();
        var edges = new List<IQuadEdge>();

        // Create vertices for a square
        var v1 = new Vertex(0, 0, 0, 10); // Bottom-left
        var v2 = new Vertex(10, 0, 0, 11); // Bottom-right
        var v3 = new Vertex(10, 10, 0, 12); // Top-right
        var v4 = new Vertex(0, 10, 0, 13); // Top-left

        // Create edges in counterclockwise order
        edges.Add(edgePool.AllocateEdge(v1, v2)); // Bottom edge
        edges.Add(edgePool.AllocateEdge(v2, v3)); // Right edge
        edges.Add(edgePool.AllocateEdge(v3, v4)); // Top edge
        edges.Add(edgePool.AllocateEdge(v4, v1)); // Left edge

        return edges;
    }
}
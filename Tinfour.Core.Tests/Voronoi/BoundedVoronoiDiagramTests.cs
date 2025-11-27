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

using System.Drawing;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;
using Tinfour.Core.Voronoi;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests for the BoundedVoronoiDiagram implementation
/// </summary>
public class BoundedVoronoiDiagramTests
{
    private readonly ITestOutputHelper _output;

    public BoundedVoronoiDiagramTests(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void Constructor_WithBuildOptions_ShouldRespectOptions()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var options = new BoundedVoronoiBuildOptions();
        options.SetBounds(new RectangleF(-5, -5, 20, 20));

        // Act
        var voronoi = new BoundedVoronoiDiagram(vertices, options);

        // Assert
        var bounds = voronoi.GetBounds();
        Assert.Equal(-5, bounds.Left);
        Assert.Equal(-5, bounds.Top);
        Assert.Equal(20, bounds.Width);
        Assert.Equal(20, bounds.Height);

        this._output.WriteLine($"Custom bounds: {bounds}");
    }

    [Fact]
    public void Constructor_WithCollinearVertices_ShouldThrow()
    {
        // Arrange - all vertices on a line
        var vertices = new List<IVertex> { new Vertex(0, 0, 0, 0), new Vertex(5, 0, 5, 1), new Vertex(10, 0, 10, 2) };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new BoundedVoronoiDiagram(vertices));
        Assert.Contains("point spacing", exception.Message);
    }

    [Fact]
    public void Constructor_WithInvalidBounds_ShouldThrow()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var options = new BoundedVoronoiBuildOptions();
        options.SetBounds(new RectangleF(2, 2, 5, 5)); // Too small to contain vertices

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new BoundedVoronoiDiagram(vertices, options));
        Assert.Contains("does not entirely contain", exception.Message);
    }

    [Fact]
    public void Constructor_WithNullVertexList_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BoundedVoronoiDiagram(null!));
    }

    [Fact]
    public void Constructor_WithTin_ShouldCreateDiagram()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        tin.Add(vertices);

        // Act
        var voronoi = new BoundedVoronoiDiagram(tin);

        // Assert
        Assert.NotNull(voronoi);
        var bounds = voronoi.GetBounds();
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);

        this._output.WriteLine($"Voronoi bounds: {bounds}");
        this._output.WriteLine($"Polygons: {voronoi.GetPolygons().Count}");
    }

    [Fact]
    public void Constructor_WithTooFewVertices_ShouldThrow()
    {
        // Arrange
        var vertices = new List<IVertex> { new Vertex(0, 0, 0, 0), new Vertex(10, 0, 5, 1) };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new BoundedVoronoiDiagram(vertices));
        Assert.Contains("at least 3 vertices", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidVertexList_ShouldCreateDiagram()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };

        // Act
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Assert
        Assert.NotNull(voronoi);
        var bounds = voronoi.GetBounds();
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);

        this._output.WriteLine($"Voronoi bounds: {bounds}");
        this._output.WriteLine($"Sample bounds: {voronoi.GetSampleBounds()}");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    public void Constructor_WithVariousVertexCounts_ShouldSucceed(int vertexCount)
    {
        // Arrange
        var vertices = new List<IVertex>();
        var random = new Random(42); // Fixed seed for reproducibility

        for (var i = 0; i < vertexCount; i++)
        {
            var x = random.NextDouble() * 100;
            var y = random.NextDouble() * 100;
            var z = random.NextDouble() * 20;
            vertices.Add(new Vertex(x, y, z, i));
        }

        // Act
        var exception = Record.Exception(() =>
            {
                var voronoi = new BoundedVoronoiDiagram(vertices);
                var bounds = voronoi.GetBounds();
                var polygons = voronoi.GetPolygons();

                this._output.WriteLine($"With {vertexCount} vertices: Bounds={bounds}, Polygons={polygons.Count}");
            });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void GetContainingPolygon_WithPointInsideBounds_ShouldReturnPolygon()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Act - point near the center
        var polygon = voronoi.GetContainingPolygon(5, 5);

        // Assert
        Assert.NotNull(polygon);
        this._output.WriteLine($"Containing polygon vertex: {polygon.GetVertex().GetLabel()}");
    }

    [Fact]
    public void GetContainingPolygon_WithPointOutsideBounds_ShouldReturnNull()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Act - point far outside
        var polygon = voronoi.GetContainingPolygon(1000, 1000);

        // Assert
        Assert.Null(polygon);
    }

    [Fact]
    public void GetEdges_ShouldReturnVoronoiEdges()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Act
        var edges = voronoi.GetEdges();

        // Assert
        Assert.NotNull(edges);
        this._output.WriteLine($"Number of Voronoi edges: {edges.Count}");
    }

    [Fact]
    public void GetPolygons_ShouldReturnThiessenPolygons()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Act
        var polygons = voronoi.GetPolygons();

        // Assert
        Assert.NotNull(polygons);
        Assert.True(polygons.Count > 0);

        foreach (var polygon in polygons)
        {
            Assert.NotNull(polygon.GetVertex());
            this._output.WriteLine($"Polygon for vertex {polygon.GetVertex().GetLabel()}");
        }
    }

    [Fact]
    public void GetVertices_ShouldReturnOriginalVertices()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Act
        var resultVertices = voronoi.GetVertices();

        // Assert
        Assert.Equal(vertices.Count, resultVertices.Count);
        this._output.WriteLine($"Original vertices: {vertices.Count}, Result vertices: {resultVertices.Count}");
    }

    [Fact]
    public void PrintDiagnostics_ShouldOutputStatistics()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 5, 1),
                               new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };
        var voronoi = new BoundedVoronoiDiagram(vertices);

        // Act
        using var writer = new StringWriter();
        voronoi.PrintDiagnostics(writer);
        var output = writer.ToString();

        // Assert
        Assert.Contains("Bounded Voronoi Diagram", output);
        Assert.Contains("Polygons:", output);
        Assert.Contains("Vertices:", output);
        Assert.Contains("Voronoi Bounds", output);

        this._output.WriteLine("Diagnostics output:");
        this._output.WriteLine(output);
    }
}
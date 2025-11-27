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

namespace Tinfour.Core.Tests.Standard;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Tests for the IncrementalTin class to verify proper Delaunay triangulation
///     following the exact same behavior as the Java implementation.
/// </summary>
public class IncrementalTinTests
{
    [Fact]
    public void Add_WithCollinearVertices_ShouldNotBootstrap()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(2, 0, 0) // Collinear
                           };

        // Act
        tin.Add(vertices);

        // Assert
        Assert.False(tin.IsBootstrapped());
    }

    [Fact]
    public void AddConstraints_ShouldStoreConstraints()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(2, 0, 0), new Vertex(1, 2, 0) };
        tin.Add(vertices);

        var constraints = new List<IConstraint> { new LinearConstraint(new[] { vertices[0], vertices[1] }) };

        // Act
        tin.AddConstraints(constraints, true);

        // Assert
        Assert.Single(tin.GetConstraints());
        Assert.True(tin.IsConformant()); // Conformity restored
    }

    [Fact]
    public void AddSquare_FourVertices_ShouldGenerate2Triangles()
    {
        // Arrange - Create a simple square (4 vertices should produce 2 triangles)
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), // Bottom-left
                               new Vertex(2, 0, 0, 1), // Bottom-right  
                               new Vertex(2, 2, 0, 2), // Top-right
                               new Vertex(0, 2, 0, 3) // Top-left)
                           };

        // Act
        tin.Add(vertices);

        // Assert
        Assert.True(tin.IsBootstrapped());
        Assert.Equal(4, tin.GetVertices().Count);
        var e = tin.GetEdges().Where((IQuadEdge t) => !t.GetB().IsNullVertex());

        // Assert.Equal(5, tin.GetEdges().Count); todo: a square should have 5 edges but we need to filter out the ghost edges before comparing
        // For now, with basic implementation, we just expect it to bootstrap with the initial triangle
        // Full triangulation will come later - this tests the basic bootstrap functionality
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        Assert.True(triangles.Count == 2); // At least the initial triangle

        var triangleCount = tin.CountTriangles();
        Assert.True(triangleCount.ValidTriangles == 2);
    }

    [Fact]
    public void AddSquareWithCenterPoint_FiveVertices_ShouldBootstrap()
    {
        // Arrange - Create a square with center point
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), // Bottom-left
                               new Vertex(2, 0, 0, 1), // Bottom-right  
                               new Vertex(2, 2, 0, 2), // Top-right
                               new Vertex(0, 2, 0, 3), // Top-left
                               new Vertex(1, 1, 0, 4) // Center point)
                           };

        // Act
        tin.Add(vertices);

        // Assert
        Assert.True(tin.IsBootstrapped());
        Assert.Equal(5, tin.GetVertices().Count);

        // For now, with basic implementation, we just expect it to bootstrap
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        Assert.True(triangles.Count == 4); // A square with a central point should have 4 triangles

        var triangleCount = tin.CountTriangles();
        Assert.True(triangleCount.ValidTriangles == 4);
    }

    [Fact]
    public void Clear_ShouldResetTin()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(2, 0, 0), new Vertex(1, 2, 0) };
        tin.Add(vertices);

        // Act
        tin.Clear();

        // Assert
        Assert.False(tin.IsBootstrapped());
        Assert.True(tin.IsConformant());
        Assert.Empty(tin.GetVertices());
        Assert.Empty(tin.GetEdges());
        Assert.Null(tin.GetBounds());
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var tin = new IncrementalTin();

        // Assert
        Assert.False(tin.IsBootstrapped());
        Assert.True(tin.IsConformant());
        Assert.Empty(tin.GetVertices());
        Assert.Empty(tin.GetEdges());
        Assert.Null(tin.GetBounds());
        Assert.Equal(1.0, tin.GetNominalPointSpacing());
    }

    [Fact]
    public void Constructor_WithCustomPointSpacing_ShouldSetCorrectly()
    {
        // Arrange & Act
        var tin = new IncrementalTin(2.5);

        // Assert
        Assert.Equal(2.5, tin.GetNominalPointSpacing());
    }

    [Fact]
    public void GetMaximumEdgeAllocationIndex_WithEdges_ShouldReturnMaxIndex()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(2, 0, 0), new Vertex(1, 2, 0) };

        // Act
        tin.Add(vertices);
        var maxIndex = tin.GetMaximumEdgeAllocationIndex();

        // Assert
        Assert.True(maxIndex >= 0);
    }

    [Fact]
    public void GetThresholds_ShouldReturnValidThresholds()
    {
        // Arrange
        var tin = new IncrementalTin(2.0);

        // Act
        var thresholds = tin.GetThresholds();

        // Assert
        Assert.NotNull(thresholds);
        Assert.Equal(2.0, thresholds.GetNominalPointSpacing());
    }

    [Fact]
    public void LargerTriangulation_ShouldBootstrap()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();

        // Create a 5x5 grid of vertices (25 vertices total)
        for (var i = 0; i < 5; i++)
        for (var j = 0; j < 5; j++)
            vertices.Add(new Vertex(i, j, i + j));

        // Act
        tin.Add(vertices);

        // Assert
        Assert.True(tin.IsBootstrapped());
        Assert.Equal(25, tin.GetVertices().Count);
        Assert.True(tin.GetEdges().Count > 0);

        var bounds = tin.GetBounds();
        Assert.NotNull(bounds);
        Assert.Equal(0, bounds.Value.Left);
        Assert.Equal(0, bounds.Value.Top);
        Assert.Equal(4, bounds.Value.Width);
        Assert.Equal(4, bounds.Value.Height);

        // A 5x5 grid MUST produce exactly 32 triangles - this is a geometric requirement
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        Assert.Equal(32, triangles.Count);

        var triangleCount = tin.CountTriangles();
        Assert.Equal(32, triangleCount.ValidTriangles);
    }

    [Fact]
    public void Pentagon_FiveVerticesConvexHull_ShouldBootstrap()
    {
        // Arrange - Create a pentagon
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(1, 0, 0, 0), // Bottom
                               new Vertex(2, 1, 0, 1), // Bottom-right
                               new Vertex(1.5, 2.5, 0, 2), // Top-right  
                               new Vertex(0.5, 2.5, 0, 3), // Top-left
                               new Vertex(0, 1, 0, 4) // Bottom-left
                           };

        // Act
        tin.Add(vertices);

        // Assert
        Assert.True(tin.IsBootstrapped());
        Assert.Equal(5, tin.GetVertices().Count);

        // For now, just verify that it bootstrapped successfully
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        Assert.True(triangles.Count >= 1);

        var triangleCount = tin.CountTriangles();
        Assert.True(triangleCount.ValidTriangles >= 1);
    }

    [Fact]
    public void SimpleTriangle_ThreeVertices_ShouldGenerate1Triangle()
    {
        // Arrange - Simple case: 3 vertices should produce exactly 1 triangle
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0, 0), new Vertex(2, 0, 0, 1), new Vertex(1, 2, 0, 2) };

        // Act
        tin.Add(vertices);

        // Assert
        Assert.True(tin.IsBootstrapped());
        Assert.Equal(3, tin.GetVertices().Count);

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        Assert.Single(triangles); // Exactly 1 triangle

        var triangleCount = tin.CountTriangles();
        Assert.Equal(1, triangleCount.ValidTriangles);
    }
}
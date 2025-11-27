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

namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Tests for the SimpleTriangleIterator class to ensure proper triangle enumeration.
/// </summary>
public class SimpleTriangleIteratorTests
{
    [Fact]
    public void CountTriangles_ShouldMatchIteratorResults()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), new Vertex(10, 0, 0, 1), new Vertex(5, 10, 0, 2),
                               new Vertex(5, 3, 0, 3)
                           };

        // Act
        tin.Add(vertices);
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        var triangleCount = tin.CountTriangles();

        // Assert
        Assert.Equal(triangles.Count, triangleCount.ValidTriangles);
    }

    [Fact]
    public void Iterator_ShouldNotReturnDuplicateTriangles()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), new Vertex(10, 0, 0, 1), new Vertex(5, 10, 0, 2),
                               new Vertex(5, 5, 0, 3)
                           };

        // Act
        tin.Add(vertices);

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();

        // Assert - check for uniqueness by comparing triangle indices
        var triangleIndices = triangles.Select((SimpleTriangle t) => t.GetIndex()).ToList();
        var uniqueIndices = triangleIndices.Distinct().ToList();

        Assert.Equal(triangleIndices.Count, uniqueIndices.Count);
    }

    [Fact]
    public void Iterator_TriangleEdges_ShouldHaveProperForwardAndReverseLinks()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[] { new Vertex(0, 0, 0, 0), new Vertex(10, 0, 0, 1), new Vertex(5, 10, 0, 2) };

        // Act
        tin.Add(vertices);

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();

        // Assert
        Assert.Single(triangles);
        var triangle = triangles[0];

        var edgeA = triangle.GetEdgeA();
        var edgeB = triangle.GetEdgeB();
        var edgeC = triangle.GetEdgeC();

        // Each edge should have proper forward and reverse links
        Assert.NotNull(edgeA.GetForward());
        Assert.NotNull(edgeA.GetReverse());
        Assert.NotNull(edgeB.GetForward());
        Assert.NotNull(edgeB.GetReverse());
        Assert.NotNull(edgeC.GetForward());
        Assert.NotNull(edgeC.GetReverse());

        // The forward/reverse cycle should be consistent
        Assert.Equal(edgeB, edgeA.GetForward());
        Assert.Equal(edgeC, edgeB.GetForward());
        Assert.Equal(edgeA, edgeC.GetForward());
    }

    [Fact]
    public void Iterator_WithBootstrappedTin_ShouldEnumerateTriangles()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[] { new Vertex(0, 0, 0, 0), new Vertex(10, 0, 0, 1), new Vertex(5, 10, 0, 2) };

        // Act
        tin.Add(vertices);
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();

        // Assert
        Assert.True(tin.IsBootstrapped());
        Assert.Single(triangles); // Should have exactly one triangle

        var triangle = triangles[0];
        Assert.False(triangle.IsGhost());
        Assert.True(triangle.GetArea() > 0); // Should have positive area
    }

    [Fact]
    public void Iterator_WithEdgesThatHaveNullVertices_ShouldNotThrow()
    {
        // Arrange - this tests the exact bug we found where edges without proper links cause exceptions
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), new Vertex(10, 0, 0, 1), new Vertex(5, 10, 0, 2),
                               new Vertex(15, 5, 0, 3) // Additional vertex to potentially create problematic edges
                           };

        // Act & Assert - should not throw any exceptions
        tin.Add(vertices);

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();

        // The iteration should complete without throwing NullReferenceException
        Assert.True(triangles.Count == 2);

        // All returned triangles should be valid (not have null vertices)
        foreach (var triangle in triangles)
        {
            Assert.False(triangle.GetVertexA().IsNullVertex());
            Assert.False(triangle.GetVertexB().IsNullVertex());
            Assert.False(triangle.GetVertexC().IsNullVertex());
        }
    }

    [Fact]
    public void Iterator_WithUnbootstrappedTin_ShouldReturnNoTriangles()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);

        // Act
        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();

        // Assert
        Assert.False(tin.IsBootstrapped());
        Assert.Empty(triangles);
    }
}
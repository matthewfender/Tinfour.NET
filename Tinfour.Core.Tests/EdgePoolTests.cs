/*
 * Copyright 2015 Gary W. Lucas.
 * Copyright 2023 Matt Sparr
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

using Xunit;

namespace Tinfour.Core.Tests;

public class EdgePoolTests
{
    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        using var pool = new EdgePool();

        // Assert
        Assert.Equal(1, pool.GetPageCount());
        Assert.Equal(1024, pool.GetPageSize());
        Assert.Equal(0, pool.Size());
        Assert.Equal(0, pool.GetEdgeCount());
        Assert.Equal(0, pool.GetMaximumAllocationIndex());
    }

    [Fact]
    public void AllocateEdge_ShouldCreateValidEdge()
    {
        // Arrange
        using var pool = new EdgePool();
        var vertexA = new Vertex(0, 0, 0);
        var vertexB = new Vertex(1, 1, 1);

        // Act
        var edge = pool.AllocateEdge(vertexA, vertexB);

        // Assert
        Assert.NotNull(edge);
        Assert.Equal(vertexA, edge.GetA());
        Assert.Equal(vertexB, edge.GetB());
        Assert.Equal(1, pool.Size());
        Assert.Equal(1, pool.GetEdgeCount());
        Assert.True(pool.GetMaximumAllocationIndex() > 0);
    }

    [Fact]
    public void AllocateUndefinedEdge_ShouldCreateEdgeWithNullVertices()
    {
        // Arrange
        using var pool = new EdgePool();

        // Act
        var edge = pool.AllocateUndefinedEdge();

        // Assert
        Assert.NotNull(edge);
        Assert.Null(edge.GetA());
        Assert.Null(edge.GetB());
        Assert.Equal(1, pool.Size());
    }

    [Fact]
    public void DeallocateEdge_ShouldReturnEdgeToPool()
    {
        // Arrange
        using var pool = new EdgePool();
        var vertexA = new Vertex(0, 0, 0);
        var vertexB = new Vertex(1, 1, 1);
        var edge = pool.AllocateEdge(vertexA, vertexB);

        // Act
        pool.DeallocateEdge(edge);

        // Assert
        Assert.Equal(0, pool.Size());
        Assert.Equal(0, pool.GetEdgeCount());
    }

    [Fact]
    public void PreAllocateEdges_ShouldAllocateCorrectNumber()
    {
        // Arrange
        using var pool = new EdgePool();
        int targetEdges = 3000; // More than one page

        // Act
        pool.PreAllocateEdges(targetEdges);

        // Assert
        Assert.True(pool.GetPageCount() >= 3); // Should have multiple pages
        Assert.Equal(0, pool.Size()); // No edges allocated yet, just pre-allocated
    }

    [Fact]
    public async Task PreAllocateEdgesAsync_ShouldWorkCorrectly()
    {
        // Arrange
        using var pool = new EdgePool();
        int targetEdges = 5000; // Large enough to trigger async behavior

        // Act
        await pool.PreAllocateEdgesAsync(targetEdges);

        // Assert
        Assert.True(pool.GetPageCount() >= 4); // Should have multiple pages
        Assert.Equal(0, pool.Size());
    }

    [Fact]
    public void AllocateMultipleEdges_ShouldWorkCorrectly()
    {
        // Arrange
        using var pool = new EdgePool();
        var edges = new List<QuadEdge>();
        int numEdges = 100;

        // Act
        for (int i = 0; i < numEdges; i++)
        {
            var edge = pool.AllocateEdge(new Vertex(i, i, i), new Vertex(i + 1, i + 1, i + 1));
            edges.Add(edge);
        }

        // Assert
        Assert.Equal(numEdges, pool.Size());
        Assert.Equal(numEdges, pool.GetEdgeCount());
        Assert.All(edges, edge => Assert.NotNull(edge));
    }

    [Fact]
    public void GetStartingEdge_WithNonGhostEdges_ShouldReturnValidEdge()
    {
        // Arrange
        using var pool = new EdgePool();
        var vertexA = new Vertex(0, 0, 0);
        var vertexB = new Vertex(1, 1, 1);
        pool.AllocateEdge(vertexA, vertexB);

        // Act
        var startingEdge = pool.GetStartingEdge();

        // Assert
        Assert.NotNull(startingEdge);
        Assert.NotNull(startingEdge.GetA());
        Assert.NotNull(startingEdge.GetB());
    }

    [Fact]
    public void GetStartingEdge_WithOnlyGhostEdges_ShouldReturnNull()
    {
        // Arrange
        using var pool = new EdgePool();
        var ghostEdge = pool.AllocateUndefinedEdge();
        ghostEdge.SetB(null); // Make it a ghost edge

        // Act
        var startingEdge = pool.GetStartingEdge();

        // Assert
        Assert.Null(startingEdge);
    }

    [Fact]
    public void GetStartingGhostEdge_WithGhostEdges_ShouldReturnGhostEdge()
    {
        // Arrange
        using var pool = new EdgePool();
        var ghostEdge = pool.AllocateUndefinedEdge();
        ghostEdge.SetB(null); // Make it a ghost edge

        // Act
        var startingGhostEdge = pool.GetStartingGhostEdge();

        // Assert
        Assert.NotNull(startingGhostEdge);
        Assert.Null(startingGhostEdge.GetB());
    }

    [Fact]
    public void GetEdges_ShouldReturnAllAllocatedEdges()
    {
        // Arrange
        using var pool = new EdgePool();
        var edge1 = pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));
        var edge2 = pool.AllocateEdge(new Vertex(2, 2, 2), new Vertex(3, 3, 3));

        // Act
        var edges = pool.GetEdges();

        // Assert
        Assert.Equal(2, edges.Count);
        Assert.Contains(edge1, edges);
        Assert.Contains(edge2, edges);
    }

    [Fact]
    public void Iterator_ShouldIterateOverAllEdges()
    {
        // Arrange
        using var pool = new EdgePool();
        var edge1 = pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));
        var edge2 = pool.AllocateEdge(new Vertex(2, 2, 2), new Vertex(3, 3, 3));

        // Act
        var edges = new List<IQuadEdge>();
        foreach (var edge in pool)
        {
            edges.Add(edge);
        }

        // Assert
        Assert.Equal(2, edges.Count);
        Assert.Contains(edge1, edges);
        Assert.Contains(edge2, edges);
    }

    [Fact]
    public void Iterator_WithGhostExclusion_ShouldSkipGhostEdges()
    {
        // Arrange
        using var pool = new EdgePool();
        var normalEdge = pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));
        var ghostEdge = pool.AllocateUndefinedEdge();
        ghostEdge.SetB(null); // Make it a ghost edge

        // Act
        var edges = new List<IQuadEdge>();
        var iterator = pool.GetIterator(false); // Exclude ghost edges
        while (iterator.MoveNext())
        {
            edges.Add(iterator.Current);
        }

        // Assert
        Assert.Single(edges);
        Assert.Contains(normalEdge, edges);
        Assert.DoesNotContain(ghostEdge, edges);
    }

    [Fact]
    public void Clear_ShouldResetPool()
    {
        // Arrange
        using var pool = new EdgePool();
        pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));
        pool.AllocateEdge(new Vertex(2, 2, 2), new Vertex(3, 3, 3));

        // Act
        pool.Clear();

        // Assert
        Assert.Equal(0, pool.Size());
        Assert.Equal(0, pool.GetEdgeCount());
        Assert.Empty(pool.GetEdges());
    }

    [Fact]
    public void SplitEdge_ShouldCreateTwoEdgesFromOne()
    {
        // Arrange
        using var pool = new EdgePool();
        var vertexA = new Vertex(0, 0, 0);
        var vertexB = new Vertex(2, 2, 2);
        var vertexM = new Vertex(1, 1, 1); // Midpoint
        var originalEdge = pool.AllocateEdge(vertexA, vertexB);

        // Act
        var newEdge = pool.SplitEdge(originalEdge, vertexM);

        // Assert
        Assert.NotNull(newEdge);
        Assert.Equal(vertexA, newEdge.GetA());
        Assert.Equal(vertexM, newEdge.GetB());
        Assert.Equal(vertexM, originalEdge.GetA());
        Assert.Equal(vertexB, originalEdge.GetB());
        Assert.Equal(2, pool.Size()); // Should now have two edges
    }

    [Fact]
    public void AddLinearConstraintToMap_ShouldStoreConstraint()
    {
        // Arrange
        using var pool = new EdgePool();
        var edge = pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));
        var constraint = new LinearConstraint();

        // Act
        pool.AddLinearConstraintToMap(edge, constraint);

        // Assert
        var retrievedConstraint = pool.GetLinearConstraint(edge);
        Assert.Equal(constraint, retrievedConstraint);
    }

    [Fact]
    public void GetLinearConstraint_WithoutConstraint_ShouldReturnNull()
    {
        // Arrange
        using var pool = new EdgePool();
        var edge = pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));

        // Act
        var constraint = pool.GetLinearConstraint(edge);

        // Assert
        Assert.Null(constraint);
    }

    [Fact]
    public void MultiplePageAllocation_ShouldWorkCorrectly()
    {
        // Arrange
        using var pool = new EdgePool();
        var edges = new List<QuadEdge>();
        int numEdges = 2500; // More than one page worth

        // Act
        for (int i = 0; i < numEdges; i++)
        {
            var edge = pool.AllocateEdge(new Vertex(i, i, i), new Vertex(i + 1, i + 1, i + 1));
            edges.Add(edge);
        }

        // Assert
        Assert.Equal(numEdges, pool.Size());
        Assert.True(pool.GetPageCount() > 1);
        Assert.All(edges, edge => 
        {
            Assert.NotNull(edge.GetA());
            Assert.NotNull(edge.GetB());
        });
    }

    [Fact]
    public void DeallocateEdge_WithNullEdge_ShouldThrow()
    {
        // Arrange
        using var pool = new EdgePool();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => pool.DeallocateEdge(null!));
    }

    [Fact]
    public void AfterDispose_OperationsShouldThrow()
    {
        // Arrange
        var pool = new EdgePool();
        pool.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1)));
        Assert.Throws<ObjectDisposedException>(() => pool.AllocateUndefinedEdge());
        Assert.Throws<ObjectDisposedException>(() => pool.Clear());
    }

    [Fact]
    public void ToString_ShouldReturnMeaningfulString()
    {
        // Arrange
        using var pool = new EdgePool();
        pool.AllocateEdge(new Vertex(0, 0, 0), new Vertex(1, 1, 1));

        // Act
        var result = pool.ToString();

        // Assert
        Assert.Contains("nEdges=1", result);
        Assert.Contains("nPages=1", result);
        Assert.Contains("nFree=", result);
    }
}
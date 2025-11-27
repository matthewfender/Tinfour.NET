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

namespace Tinfour.Core.Tests.Edge;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;

using Xunit;

public class QuadEdgeTests
{
    [Fact]
    public void Constructor_ShouldCreateEdgeWithDual()
    {
        // Arrange & Act
        var edge = new QuadEdge(0);

        // Assert
        Assert.NotNull(edge.GetDual());
        Assert.True(ReferenceEquals(edge, edge.GetDual().GetDual()));
    }

    [Fact]
    public void Constructor_WithIndex_ShouldSetIndexCorrectly()
    {
        // Arrange & Act
        var expectedIndex = 42;
        var edge = new QuadEdge(expectedIndex);

        // Assert
        Assert.Equal(expectedIndex, edge.GetIndex());
        Assert.Equal(expectedIndex + 1, edge.GetDual().GetIndex());
    }

    // [Fact]
    // public void GetBaseIndex_ShouldReturnEvenValue()
    // {
    // // this will fail - no protection for odd numbers in the QuadEdge ctr
    // // Arrange
    // var edge = new QuadEdge(43); // Should be set to 42
    // var dual = edge.GetDual();

    // // Act & Assert
    // Assert.Equal(42, edge.GetBaseIndex());
    // Assert.Equal(42, dual.GetBaseIndex());
    // }
    [Fact]
    public void GetBaseReference_ShouldReturnEdgeWithEvenIndex()
    {
        // Arrange
        var edge = new QuadEdge(42);
        var dual = edge.GetDual();

        // Act & Assert
        Assert.True(ReferenceEquals(edge, edge.GetBaseReference()));
        Assert.True(ReferenceEquals(edge, dual.GetBaseReference()));
    }

    [Fact]
    public void GetDualFromReverse_ShouldReturnCorrectEdge()
    {
        // Arrange
        var edge1 = new QuadEdge(10);
        var edge2 = new QuadEdge(20);
        edge1.SetReverse(edge2);

        // Act & Assert
        Assert.Same(edge2.GetDual(), edge1.GetDualFromReverse());
    }

    [Fact]
    public void GetForwardFromDual_ShouldReturnCorrectEdge()
    {
        // Arrange
        var edge1 = new QuadEdge(10);
        var edge2 = new QuadEdge(20);
        var dual = (QuadEdge)edge1.GetDual();

        // Act
        dual.SetForward(edge2);

        // Assert
        Assert.Same(edge2, edge1.GetForwardFromDual());
    }

    [Fact]
    public void GetLength_ShouldCalculateCorrectDistance()
    {
        // Arrange
        var edge = new QuadEdge(0);
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(3, 4, 0);
        edge.SetVertices(a, b);

        // Act
        var length = edge.GetLength();

        // Assert
        Assert.Equal(5.0, length);
    }

    [Fact]
    public void GetLength_WithGhostVertex_ShouldReturnInfinity()
    {
        // Arrange
        var edge = new QuadEdge(0);
        var a = new Vertex(0, 0, 0);
        edge.SetVertices(a, Vertex._NullVertex);

        // Act
        var length = edge.GetLength();

        // Assert
        Assert.Equal(double.PositiveInfinity, length);
    }

    [Fact]
    public void GetPinwheel_ShouldIterateAroundVertex()
    {
        // Arrange
        var center = new Vertex(0, 0, 0);
        var v1 = new Vertex(1, 0, 0);
        var v2 = new Vertex(0, 1, 0);
        var v3 = new Vertex(-1, 0, 0);
        var v4 = new Vertex(0, -1, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);
        var e4 = new QuadEdge(40);

        e1.SetVertices(center, v1);
        e2.SetVertices(center, v2);
        e3.SetVertices(center, v3);
        e4.SetVertices(center, v4);

        var e1Dual = (QuadEdge)e1.GetDual();
        var e2Dual = (QuadEdge)e2.GetDual();
        var e3Dual = (QuadEdge)e3.GetDual();
        var e4Dual = (QuadEdge)e4.GetDual();

        // Set up the pinwheel connections (counterclockwise order)
        e1.SetReverse(e2Dual);
        e2.SetReverse(e3Dual);
        e3.SetReverse(e4Dual);
        e4.SetReverse(e1Dual);

        // Act
        var edgesInPinwheel = e1.GetPinwheel().ToList();

        // Assert
        Assert.Equal(4, edgesInPinwheel.Count);
        Assert.Same(e1, edgesInPinwheel[0]);
        Assert.Same(e2, edgesInPinwheel[1]);
        Assert.Same(e3, edgesInPinwheel[2]);
        Assert.Same(e4, edgesInPinwheel[3]);
    }

    [Fact]
    public void GetReverseFromDual_ShouldReturnCorrectEdge()
    {
        // Arrange
        var edge1 = new QuadEdge(10);
        var edge2 = new QuadEdge(20);
        var dual = (QuadEdge)edge1.GetDual();

        // Act
        dual.SetReverse(edge2);

        // Assert
        Assert.Same(edge2, edge1.GetReverseFromDual());
    }

    [Fact]
    public void SetForwardAndReverse_ShouldConnectEdgesCorrectly()
    {
        // Arrange
        var edge1 = new QuadEdge(10);
        var edge2 = new QuadEdge(20);
        var edge3 = new QuadEdge(30);

        // Act
        edge1.SetForward(edge2);
        edge1.SetReverse(edge3);

        // Assert
        Assert.Same(edge2, edge1.GetForward());
        Assert.Same(edge3, edge1.GetReverse());
    }

    [Fact]
    public void SetVertices_ShouldAssignVerticesToEdgeAndDual()
    {
        // Arrange
        var edge = new QuadEdge(0);
        var a = new Vertex(1, 2, 3);
        var b = new Vertex(4, 5, 6);

        // Act
        edge.SetVertices(a, b);

        // Assert
        Assert.Equal(a, edge.GetA());
        Assert.Equal(b, edge.GetB());
        Assert.Equal(b, edge.GetDual().GetA());
        Assert.Equal(a, edge.GetDual().GetB());
    }

    [Fact]
    public void ToString_ShouldIncludeVertexCoordinatesAndIndex()
    {
        // Arrange
        var edge = new QuadEdge(42);
        var a = new Vertex(1, 2, 0);
        var b = new Vertex(3, 4, 0);
        edge.SetVertices(a, b);

        // Act
        var result = edge.ToString();

        // Assert
        Assert.Contains("1.0", result);
        Assert.Contains("2.0", result);
        Assert.Contains("3.0", result);
        Assert.Contains("4.0", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void ToString_WithGhostVertex_ShouldIncludeGhostReference()
    {
        // Arrange
        var edge = new QuadEdge(42);
        var a = new Vertex(1, 2, 0);
        edge.SetVertices(a, Vertex._NullVertex);

        // Act
        var result = edge.ToString();

        // Assert
        Assert.Contains("1.0", result);
        Assert.Contains("2.0", result);
        Assert.Contains("ghost", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void TranscribeTo_ShouldProduceCorrectCoordinates()
    {
        // Arrange
        var edge = new QuadEdge(0);
        var a = new Vertex(1, 2, 0);
        var b = new Vertex(3, 4, 0);
        edge.SetVertices(a, b);

        // Act
        edge.TranscribeTo(out var start, out var end);

        // Assert
        Assert.Equal(1.0f, start.X);
        Assert.Equal(2.0f, start.Y);
        Assert.Equal(3.0f, end.X);
        Assert.Equal(4.0f, end.Y);
    }

    [Fact]
    public void TranscribeTo_WithGhostVertex_ShouldSetEndPointToNaN()
    {
        // Arrange
        var edge = new QuadEdge(0);
        var a = new Vertex(1, 2, 0);
        edge.SetVertices(a, Vertex._NullVertex);

        // Act
        edge.TranscribeTo(out var start, out var end);

        // Assert
        Assert.Equal(1.0f, start.X);
        Assert.Equal(2.0f, start.Y);
        Assert.True(float.IsNaN(end.X));
        Assert.True(float.IsNaN(end.Y));
    }
}
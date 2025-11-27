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

public class PinwheelIteratorTests
{
    [Fact]
    public void GetPinwheel_UsingForeach_ShouldIterateCorrectly()
    {
        // Arrange
        var center = new Vertex(0, 0, 0);
        var v1 = new Vertex(1, 0, 0); // East
        var v2 = new Vertex(0, 1, 0); // North
        var v3 = new Vertex(-1, 0, 0); // West

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(center, v1);
        e2.SetVertices(center, v2);
        e3.SetVertices(center, v3);

        var e1Dual = e1.GetDual();
        var e2Dual = e2.GetDual();
        var e3Dual = e3.GetDual();

        // Set up the pinwheel connections for counterclockwise traversal
        // Starting from e1 (East), next should be e2 (North), then e3 (West)
        e1.SetReverse(e2Dual); // e1 -> e2
        e2.SetReverse(e3Dual); // e2 -> e3
        e3.SetReverse(e1Dual); // e3 -> e1

        // Act
        var edges = new List<IQuadEdge>();
        foreach (var edge in e1.GetPinwheel()) edges.Add(edge);

        // Assert
        Assert.Equal(3, edges.Count);
        Assert.Same(e1, edges[0]);
        Assert.Same(e2, edges[1]);
        Assert.Same(e3, edges[2]);
    }

    [Fact]
    public void GetPinwheel_WithMultipleEdges_ShouldIterateCorrectly()
    {
        // Arrange
        var center = new Vertex(0, 0, 0);
        var v1 = new Vertex(1, 0, 0); // East
        var v2 = new Vertex(0, 1, 0); // North  
        var v3 = new Vertex(-1, 0, 0); // West

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);
        var e3 = new QuadEdge(30);

        e1.SetVertices(center, v1);
        e2.SetVertices(center, v2);
        e3.SetVertices(center, v3);

        var e1Dual = e1.GetDual();
        var e2Dual = e2.GetDual();
        var e3Dual = e3.GetDual();

        // Set up the pinwheel connections for counterclockwise traversal
        // Starting from e1 (East), next should be e2 (North), then e3 (West)
        e1.SetReverse(e2Dual); // e1 -> e2
        e2.SetReverse(e3Dual); // e2 -> e3  
        e3.SetReverse(e1Dual); // e3 -> e1

        // Act
        var iterator = new PinwheelIterator(e1);
        var edges = iterator.ToList();

        // Assert
        Assert.Equal(3, edges.Count);
        Assert.Same(e1, edges[0]);
        Assert.Same(e2, edges[1]);
        Assert.Same(e3, edges[2]);
    }

    [Fact]
    public void GetPinwheel_WithSingleEdge_ShouldReturnOnlyStartingEdge()
    {
        // Arrange
        var center = new Vertex(0, 0, 0);
        var v1 = new Vertex(1, 0, 0);

        var e1 = new QuadEdge(10);
        e1.SetVertices(center, v1);

        var e1Dual = e1.GetDual();
        e1.SetReverse(e1Dual);

        // Act
        var iterator = new PinwheelIterator(e1);
        var edges = iterator.ToList();

        // Assert
        Assert.Single(edges);
        Assert.Same(e1, edges[0]);
    }

    [Fact]
    public void GetPinwheel_WithTwoEdges_ShouldIterateCorrectly()
    {
        // Arrange
        var center = new Vertex(0, 0, 0);
        var v1 = new Vertex(1, 0, 0);
        var v2 = new Vertex(0, 1, 0);

        var e1 = new QuadEdge(10);
        var e2 = new QuadEdge(20);

        e1.SetVertices(center, v1);
        e2.SetVertices(center, v2);

        var e1Dual = e1.GetDual();
        var e2Dual = e2.GetDual();

        // Set up the pinwheel connections for counterclockwise traversal
        e1.SetReverse(e2Dual);
        e2.SetReverse(e1Dual);

        // Act
        var iterator = new PinwheelIterator(e1);
        var edges = iterator.ToList();

        // Assert
        Assert.Equal(2, edges.Count);
        Assert.Same(e1, edges[0]);
        Assert.Same(e2, edges[1]);
    }
}
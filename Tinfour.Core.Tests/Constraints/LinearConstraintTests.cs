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

public class LinearConstraintTests
{
    [Fact]
    public void ApplicationData_InitiallyNull()
    {
        // Arrange
        var constraint = new LinearConstraint([new Vertex(0, 0, 0), new Vertex(1, 1, 0)]);

        // Assert
        Assert.Null(constraint.GetApplicationData());
    }

    [Fact]
    public void ApplicationData_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        var constraint = new LinearConstraint([new Vertex(0, 0, 0), new Vertex(1, 1, 0)]);
        var testData = "Test Application Data";

        // Act
        constraint.SetApplicationData(testData);

        // Assert
        Assert.Equal(testData, constraint.GetApplicationData());
    }

    [Fact]
    public void Constructor_WithEmptyVertices_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new LinearConstraint(Array.Empty<IVertex>()));
    }

    [Fact]
    public void Constructor_WithMultipleVertices_ShouldCreateValidConstraint()
    {
        // Arrange
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0), new Vertex(0, 1, 0)
                           };

        // Act
        var constraint = new LinearConstraint(vertices);

        // Assert
        Assert.Equal(4, constraint.GetVertices().Count);
        Assert.Equal(3, constraint.GetSegmentCount());
        Assert.False(constraint.DefinesConstrainedRegion());
    }

    [Fact]
    public void Constructor_WithSingleVertex_ShouldThrowArgumentException()
    {
        // Arrange
        var vertices = new IVertex[] { new Vertex(0, 0, 0) };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new LinearConstraint(vertices));
    }

    [Fact]
    public void Constructor_WithTwoVertices_ShouldCreateValidConstraint()
    {
        // Arrange
        var start = new Vertex(0, 0, 0);
        var end = new Vertex(1, 1, 0);

        // Act
        var constraint = new LinearConstraint([start, end]);

        // Assert
        Assert.Equal(2, constraint.GetVertices().Count);
        Assert.Equal(start, constraint.GetVertices()[0]);
        Assert.Equal(end, constraint.GetVertices()[1]);
        Assert.Equal(1, constraint.GetSegmentCount());
        Assert.False(constraint.DefinesConstrainedRegion());
        Assert.Equal(0, constraint.GetConstraintIndex());
    }

    [Fact]
    public void GetLength_ShouldCalculateCorrectTotalLength()
    {
        // Arrange - Rectangle path: (0,0) -> (3,0) -> (3,4) -> (0,4)
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(3, 0, 0), // Length = 3
                               new Vertex(3, 4, 0), // Length = 4
                               new Vertex(0, 4, 0) // Length = 3
                           };
        var constraint = new LinearConstraint(vertices);

        // Act
        var length = constraint.GetLength();

        // Assert
        Assert.Equal(10.0, length, 3); // 3 + 4 + 3 = 10
    }

    [Fact]
    public void GetLength_WithSingleSegment_ShouldCalculateCorrectLength()
    {
        // Arrange - 3-4-5 right triangle hypotenuse
        var start = new Vertex(0, 0, 0);
        var end = new Vertex(3, 4, 0);
        var constraint = new LinearConstraint([start, end]);

        // Act
        var length = constraint.GetLength();

        // Assert
        Assert.Equal(5.0, length, 3); // sqrt(3^2 + 4^2) = 5
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 4)]
    [InlineData(10, 9)]
    public void GetSegmentCount_ShouldReturnVertexCountMinusOne(int vertexCount, int expectedSegments)
    {
        // Arrange
        var vertices = new IVertex[vertexCount];
        for (var i = 0; i < vertexCount; i++) vertices[i] = new Vertex(i, 0, 0);
        var constraint = new LinearConstraint(vertices);

        // Act
        var segmentCount = constraint.GetSegmentCount();

        // Assert
        Assert.Equal(expectedSegments, segmentCount);
    }

    [Fact]
    public void GetVertices_ShouldReturnReadOnlyList()
    {
        // Arrange
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 1, 0) };
        var constraint = new LinearConstraint(vertices);

        // Act
        var returnedVertices = constraint.GetVertices();

        // Assert
        Assert.IsAssignableFrom<IList<Vertex>>(returnedVertices);
        Assert.Equal(2, returnedVertices.Count);

        // Verify it's read-only by checking if modification throws
        Assert.Throws<NotSupportedException>(() => returnedVertices.Add(new Vertex(2, 2, 0)));
    }

    [Fact]
    public void ToString_ShouldIncludeVertexAndSegmentCounts()
    {
        // Arrange
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(1, 1, 0) };
        var constraint = new LinearConstraint(vertices);

        // Act
        var result = constraint.ToString();

        // Assert
        Assert.Contains("3 vertices", result);
        Assert.Contains("LinearConstraint", result);
    }
}
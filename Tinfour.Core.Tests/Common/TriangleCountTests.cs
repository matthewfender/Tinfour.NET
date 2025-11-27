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

using Xunit;

public class TriangleCountTests
{
    [Fact]
    public void ConstrainedTriangles_ShouldNotAffectTotalTriangles()
    {
        // Arrange - Constrained triangles are a subset of valid triangles
        var count = new TriangleCount { ValidTriangles = 50, GhostTriangles = 10, ConstrainedTriangles = 20 };

        // Act
        var total = count.TotalTriangles;

        // Assert
        Assert.Equal(60, total); // Only valid + ghost, constrained doesn't add to total
    }

    [Fact]
    public void Constructor_Default_ShouldInitializeAllCountsToZero()
    {
        // Act
        var count = new TriangleCount();

        // Assert
        Assert.Equal(0, count.ValidTriangles);
        Assert.Equal(0, count.GhostTriangles);
        Assert.Equal(0, count.ConstrainedTriangles);
        Assert.Equal(0, count.TotalTriangles);
    }

    [Fact]
    public void Constructor_WithValues_ShouldSetCountsCorrectly()
    {
        // Act
        var count = new TriangleCount(10, 5, 3);

        // Assert
        Assert.Equal(10, count.ValidTriangles);
        Assert.Equal(5, count.GhostTriangles);
        Assert.Equal(3, count.ConstrainedTriangles);
        Assert.Equal(15, count.TotalTriangles); // 10 + 5
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var count = new TriangleCount();

        // Act
        count.ValidTriangles = 100;
        count.GhostTriangles = 25;
        count.ConstrainedTriangles = 40;

        // Assert
        Assert.Equal(100, count.ValidTriangles);
        Assert.Equal(25, count.GhostTriangles);
        Assert.Equal(40, count.ConstrainedTriangles);
        Assert.Equal(125, count.TotalTriangles);
    }

    [Fact]
    public void ToString_ShouldIncludeAllCounts()
    {
        // Arrange
        var count = new TriangleCount(15, 3, 7);

        // Act
        var result = count.ToString();

        // Assert
        Assert.Contains("Valid: 15", result);
        Assert.Contains("Ghost: 3", result);
        Assert.Contains("Constrained: 7", result);
        Assert.Contains("Total: 18", result);
        Assert.Contains("TriangleCount", result);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 2)]
    [InlineData(100, 25, 125)]
    [InlineData(1000, 0, 1000)]
    public void TotalTriangles_ShouldAlwaysEqualValidPlusGhost(int valid, int ghost, int expectedTotal)
    {
        // Arrange
        var count = new TriangleCount { ValidTriangles = valid, GhostTriangles = ghost };

        // Act
        var total = count.TotalTriangles;

        // Assert
        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public void TotalTriangles_ShouldCalculateCorrectSum()
    {
        // Arrange
        var count = new TriangleCount { ValidTriangles = 25, GhostTriangles = 8 };

        // Act
        var total = count.TotalTriangles;

        // Assert
        Assert.Equal(33, total);
    }

    [Fact]
    public void TotalTriangles_WithZeroValues_ShouldReturnZero()
    {
        // Arrange
        var count = new TriangleCount();

        // Act
        var total = count.TotalTriangles;

        // Assert
        Assert.Equal(0, total);
    }
}
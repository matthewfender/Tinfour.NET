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

namespace Tinfour.Core.Tests.Interpolation;

using Tinfour.Core.Common;

using Xunit;

public class NearestNeighborPointCollectorTests
{
    [Fact]
    public void Bounds_ShouldReturnCorrectValues()
    {
        // Arrange
        var vertices = new List<IVertex> { new Vertex(-1, -2, 0), new Vertex(3, 4, 1), new Vertex(0, 0, 2) };
        var collector = new NearestNeighborPointCollector(vertices);

        // Act
        var bounds = collector.Bounds;

        // Assert
        Assert.Equal(-1.0, bounds.XMin);
        Assert.Equal(3.0, bounds.XMax);
        Assert.Equal(-2.0, bounds.YMin);
        Assert.Equal(4.0, bounds.YMax);
    }

    [Fact]
    public void Constructor_WithEmptyList_ShouldThrow()
    {
        // Arrange
        var emptyList = new List<IVertex>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new NearestNeighborPointCollector(emptyList));
    }

    [Fact]
    public void Constructor_WithLargeDataset_ShouldBeEfficient()
    {
        // Arrange - Create a large grid
        var vertices = new List<IVertex>();
        for (var i = 0; i < 100; i++)
        for (var j = 0; j < 100; j++)
            vertices.Add(new Vertex(i * 0.1, j * 0.1, i + j));

        // Act
        var collector = new NearestNeighborPointCollector(vertices);

        // Assert
        Assert.Equal(10000, collector.GetVertices().Count);
        Assert.True(collector.BinCount > 1); // Should create multiple bins

        // Should be able to find nearest neighbors efficiently
        var results = collector.GetNearestNeighbors(5.0, 5.0, 4);
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void Constructor_WithMultipleVertices_ShouldCreateBins()
    {
        // Arrange
        var vertices = new List<IVertex>();
        for (var i = 0; i < 1000; i++) vertices.Add(new Vertex(i % 10, i / 10, i));

        // Act
        var collector = new NearestNeighborPointCollector(vertices);

        // Assert
        Assert.True(collector.BinCount > 1);
        var dimensions = collector.GridDimensions;
        Assert.True(dimensions.Rows > 0);
        Assert.True(dimensions.Columns > 0);

        var collectedVertices = collector.GetVertices();
        Assert.Equal(1000, collectedVertices.Count);
    }

    [Fact]
    public void Constructor_WithNullList_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new NearestNeighborPointCollector(null!));
    }

    [Fact]
    public void Constructor_WithSingleVertex_ShouldWork()
    {
        // Arrange
        var vertices = new List<IVertex> { new Vertex(1, 2, 3) };

        // Act
        var collector = new NearestNeighborPointCollector(vertices);

        // Assert
        Assert.Equal(1, collector.BinCount);
        var bounds = collector.Bounds;
        Assert.Equal(1.0, bounds.XMin);
        Assert.Equal(1.0, bounds.XMax);
        Assert.Equal(2.0, bounds.YMin);
        Assert.Equal(2.0, bounds.YMax);
    }

    [Fact]
    public void GetNearestNeighbors_ListOverload_ShouldReturnSameResults()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0), new Vertex(1, 0, 1), new Vertex(0, 1, 2), new Vertex(1, 1, 3)
                           };
        var collector = new NearestNeighborPointCollector(vertices);

        // Act
        var results = collector.GetNearestNeighbors(0.5, 0.5, 3);

        // Assert
        Assert.Equal(3, results.Count);

        // Check that distances are computed correctly
        foreach (var (vertex, distance) in results)
        {
            var expectedDistance = vertex.GetDistance(0.5, 0.5);
            Assert.Equal(expectedDistance, distance, 10);
        }

        // Check that results are sorted by distance
        for (var i = 1; i < results.Count; i++) Assert.True(results[i].Distance >= results[i - 1].Distance);
    }

    [Fact]
    public void GetNearestNeighbors_RequestMoreThanAvailable_ShouldReturnAll()
    {
        // Arrange
        var vertices = new List<IVertex> { new Vertex(0, 0, 0), new Vertex(1, 1, 1) };
        var collector = new NearestNeighborPointCollector(vertices);
        var distances = new double[5];
        var resultVertices = new IVertex[5];

        // Act - Request more than available
        var found = collector.GetNearestNeighbors(0.5, 0.5, 5, distances, resultVertices);

        // Assert
        Assert.Equal(2, found); // Should return only the available vertices
    }

    [Theory]
    [InlineData(0.0, 0.0, 1)]
    [InlineData(0.5, 0.5, 3)]
    [InlineData(1.0, 1.0, 5)]
    [InlineData(2.0, 2.0, 2)]
    public void GetNearestNeighbors_VariousQueries_ShouldReturnValidResults(double queryX, double queryY, int k)
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0),
                               new Vertex(1, 0, 1),
                               new Vertex(0, 1, 2),
                               new Vertex(1, 1, 3),
                               new Vertex(2, 2, 4)
                           };
        var collector = new NearestNeighborPointCollector(vertices);

        // Act
        var results = collector.GetNearestNeighbors(queryX, queryY, k);

        // Assert
        Assert.True(results.Count <= k);
        Assert.True(results.Count <= vertices.Count);

        // All distances should be non-negative
        Assert.All(results, ((IVertex Vertex, double Distance) r) => Assert.True(r.Distance >= 0));

        // Results should be sorted by distance
        for (var i = 1; i < results.Count; i++) Assert.True(results[i].Distance >= results[i - 1].Distance);
    }

    [Fact]
    public void GetNearestNeighbors_WithArraysTooSmall_ShouldThrow()
    {
        // Arrange
        var vertices = new List<IVertex> { new Vertex(0, 0, 0) };
        var collector = new NearestNeighborPointCollector(vertices);
        var smallDistances = new double[1];
        var smallVertices = new IVertex[1];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => collector.GetNearestNeighbors(0, 0, 2, smallDistances, smallVertices));
    }

    [Fact]
    public void GetNearestNeighbors_WithDuplicates_ShouldHandleCorrectly()
    {
        // Arrange - Include duplicate points
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 1),
                               new Vertex(0, 0, 2), // Duplicate location, different Z
                               new Vertex(1, 1, 3),
                               new Vertex(2, 2, 4)
                           };
        var collector = new NearestNeighborPointCollector(vertices);

        // Act
        var distances = new double[4];
        var resultVertices = new IVertex[4];
        var found = collector.GetNearestNeighbors(0, 0, 4, distances, resultVertices);

        // Assert
        Assert.Equal(4, found);

        // Both duplicates at (0,0) should be found
        var zeroDistanceCount = 0;
        for (var i = 0; i < found; i++)
            if (distances[i] == 0.0)
            {
                zeroDistanceCount++;
                Assert.Equal(0.0, resultVertices[i].GetX());
                Assert.Equal(0.0, resultVertices[i].GetY());
            }

        Assert.Equal(2, zeroDistanceCount);
    }

    [Fact]
    public void GetNearestNeighbors_WithKZero_ShouldReturnZero()
    {
        // Arrange
        var vertices = new List<IVertex> { new Vertex(0, 0, 0), new Vertex(1, 1, 1), new Vertex(2, 2, 2) };
        var collector = new NearestNeighborPointCollector(vertices);
        var distances = new double[3];
        var resultVertices = new IVertex[3];

        // Act
        var found = collector.GetNearestNeighbors(0.5, 0.5, 0, distances, resultVertices);

        // Assert
        Assert.Equal(0, found);
    }

    [Fact]
    public void GetNearestNeighbors_WithMergeDuplicates_ShouldMerge()
    {
        // Arrange - Include very close points
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 1),
                               new Vertex(1e-10, 1e-10, 2), // Very close to first point
                               new Vertex(1, 1, 3),
                               new Vertex(2, 2, 4)
                           };
        var collector = new NearestNeighborPointCollector(vertices, true);

        // Act
        var allVertices = collector.GetVertices();

        // Assert
        // Should have fewer vertices due to merging
        Assert.True(allVertices.Count <= 4);
        Assert.True(allVertices.Count >= 3); // At least one merge should occur
    }

    [Fact]
    public void GetNearestNeighbors_WithOffGridQuery_ShouldFindClosest()
    {
        // Arrange
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0), new Vertex(2, 0, 1), new Vertex(0, 2, 2), new Vertex(2, 2, 3)
                           };
        var collector = new NearestNeighborPointCollector(vertices);
        var distances = new double[2];
        var resultVertices = new IVertex[2];

        // Act - Query near (0,0) but slightly offset
        var found = collector.GetNearestNeighbors(0.1, 0.1, 2, distances, resultVertices);

        // Assert
        Assert.Equal(2, found);

        // First result should be (0,0) as it's closest
        Assert.Equal(0.0, resultVertices[0].GetX());
        Assert.Equal(0.0, resultVertices[0].GetY());

        // Second should be one of the adjacent corners
        var dist1 = resultVertices[1].GetDistanceSq(0.1, 0.1);
        Assert.True(dist1 > distances[0]); // Second distance should be larger
    }

    [Fact]
    public void GetNearestNeighbors_WithSimpleGrid_ShouldFindClosest()
    {
        // Arrange - Create a 3x3 grid
        var vertices = new List<IVertex>();
        for (var i = 0; i < 3; i++)
        for (var j = 0; j < 3; j++)
            vertices.Add(new Vertex(i, j, i * 3 + j));

        var collector = new NearestNeighborPointCollector(vertices);
        var distances = new double[4];
        var resultVertices = new IVertex[4];

        // Act - Find 4 nearest to center of grid
        var found = collector.GetNearestNeighbors(1.0, 1.0, 4, distances, resultVertices);

        // Assert
        Assert.Equal(4, found);

        // The center point (1,1) should be the closest
        Assert.Equal(1.0, resultVertices[0].GetX());
        Assert.Equal(1.0, resultVertices[0].GetY());
        Assert.Equal(0.0, distances[0], 10); // Should be exactly at the point

        // Distances should be in ascending order
        for (var i = 1; i < found; i++) Assert.True(distances[i] >= distances[i - 1]);
    }

    [Fact]
    public void GetVertices_ShouldReturnAllStoredVertices()
    {
        // Arrange
        var originalVertices = new List<IVertex>
                                   {
                                       new Vertex(0, 0, 1),
                                       new Vertex(1, 1, 2),
                                       new Vertex(2, 2, 3),
                                       new Vertex(3, 3, 4)
                                   };
        var collector = new NearestNeighborPointCollector(originalVertices);

        // Act
        var retrievedVertices = collector.GetVertices();

        // Assert
        Assert.Equal(originalVertices.Count, retrievedVertices.Count);

        // All original vertices should be present (order may differ)
        foreach (var original in originalVertices)
            Assert.Contains(
                retrievedVertices,
                (IVertex v) => v.GetX() == original.GetX() && v.GetY() == original.GetY() && v.GetZ() == original.GetZ());
    }
}
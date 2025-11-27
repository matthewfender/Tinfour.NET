/*
 * Copyright 2016 Gary W. Lucas.
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

public class BootstrapUtilityTests
{
    [Fact]
    public void Constructor_WithValidThresholds_ShouldSucceed()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);

        // Act & Assert
        var utility = new BootstrapUtility(thresholds);
        Assert.NotNull(utility);
    }

    [Fact]
    public void Constructor_WithNullThresholds_ShouldThrow()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BootstrapUtility(null!));
    }

    [Fact]
    public void Bootstrap_WithTooFewVertices_ShouldReturnNull()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 1, 1)
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Bootstrap_WithNullList_ShouldReturnNull()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);

        // Act
        var result = utility.Bootstrap(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Bootstrap_WithValidTriangle_ShouldReturnThreeVertices()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 0, 0),
            new Vertex(0.5, 1, 0)
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.All(result, v => Assert.NotNull(v));
    }

    [Fact]
    public void Bootstrap_WithCollinearVertices_ShouldReturnNull()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 0, 0),
            new Vertex(2, 0, 0), // Collinear
            new Vertex(3, 0, 0)  // Collinear
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Bootstrap_WithCoincidentVertices_ShouldReturnNull()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(0, 0, 0), // Same as first
            new Vertex(0, 0, 0)  // Same as first
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Bootstrap_WithLargeValidSet_ShouldReturnValidTriangle()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>();
        
        // Create a grid of vertices with one good triangle
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                vertices.Add(new Vertex(i, j, 0));
            }
        }

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        
        // Verify the triangle has positive area
        var geoOps = new GeometricOperations(thresholds);
        double area = geoOps.Area(result[0], result[1], result[2]);
        Assert.True(area > 0);
    }

    [Fact]
    public void Bootstrap_WithMixedValidAndInvalidVertices_ShouldFindValidTriangle()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            // Many collinear vertices
            new Vertex(0, 0, 0),
            new Vertex(1, 0, 0),
            new Vertex(2, 0, 0),
            new Vertex(3, 0, 0),
            new Vertex(4, 0, 0),
            // One vertex that forms a good triangle
            new Vertex(2, 2, 0)
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void TestInput_WithInsufficientPoints_ShouldReturnInsufficientPointSet()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex> { new Vertex(0, 0, 0) };
        var output = new List<Vertex>();

        // Act
        var result = utility.TestInput(vertices, output);

        // Assert
        Assert.Equal(BootstrapTestResult.InsufficientPointSet, result);
        Assert.Empty(output);
    }

    [Fact]
    public void TestInput_WithCoincidentPoints_ShouldReturnTrivialPointSet()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(0.0001, 0.0001, 0), // Very close to first
            new Vertex(0.0001, 0.0002, 0)  // Very close to first
        };
        var output = new List<Vertex>();

        // Act
        var result = utility.TestInput(vertices, output);

        // Assert
        Assert.Equal(BootstrapTestResult.TrivialPointSet, result);
    }

    [Fact]
    public void TestInput_WithCollinearPoints_ShouldReturnCollinearPointSet()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 0, 0),
            new Vertex(2, 0, 0),
            new Vertex(3, 0, 0)
        };
        var output = new List<Vertex>();

        // Act
        var result = utility.TestInput(vertices, output);

        // Assert
        Assert.Equal(BootstrapTestResult.CollinearPointSet, result);
    }

    [Fact]
    public void TestInput_WithValidPoints_ShouldReturnValid()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(2, 0, 0),
            new Vertex(1, 2, 0),
            new Vertex(1, 1, 0)
        };
        var output = new List<Vertex>();

        // Act
        var result = utility.TestInput(vertices, output);

        // Assert
        Assert.Equal(BootstrapTestResult.Valid, result);
        Assert.Equal(3, output.Count);
    }

    [Fact]
    public void TestInput_WithNullInput_ShouldReturnInsufficientPointSet()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var output = new List<Vertex>();

        // Act
        var result = utility.TestInput(null!, output);

        // Assert
        Assert.Equal(BootstrapTestResult.InsufficientPointSet, result);
    }

    [Fact]
    public void TestInput_WithNullOutput_ShouldNotThrow()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 0, 0),
            new Vertex(0, 1, 0)
        };

        // Act & Assert
        var result = utility.TestInput(vertices, null!);
        Assert.Equal(BootstrapTestResult.Valid, result);
    }

    [Fact]
    public void Bootstrap_ShouldHandleRandomSelection()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>();
        
        // Create many vertices including some good triangles
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < 100; i++)
        {
            vertices.Add(new Vertex(random.NextDouble() * 10, random.NextDouble() * 10, 0));
        }

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        
        // Verify the triangle has reasonable area
        var geoOps = new GeometricOperations(thresholds);
        double area = geoOps.Area(result[0], result[1], result[2]);
        Assert.True(area > 0);
    }

    [Fact]
    public void Bootstrap_WithExactlyThreeVertices_ShouldTestOnlyThoseThree()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(2, 0, 0),
            new Vertex(1, 2, 0)
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Contains(vertices[0], result);
        Assert.Contains(vertices[1], result);
        Assert.Contains(vertices[2], result);
    }

    [Fact]
    public void Bootstrap_ShouldEnsurePositiveOrientation()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 2, 0), // This will create negative orientation if not corrected
            new Vertex(2, 0, 0)
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.NotNull(result);
        
        // Verify positive orientation
        var geoOps = new GeometricOperations(thresholds);
        double area = geoOps.Area(result[0], result[1], result[2]);
        Assert.True(area > 0);
    }
}
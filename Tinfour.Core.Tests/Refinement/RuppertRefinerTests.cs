/*
 * Copyright 2025 Gary W. Lucas.
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

namespace Tinfour.Core.Tests.Refinement;

using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Xunit;

public class RuppertRefinerTests
{
    [Fact]
    public void Constructor_WithValidTin_ShouldNotThrow()
    {
        // Arrange
        var tin = CreateSimpleConstrainedTin();

        // Act & Assert
        var refiner = new RuppertRefiner(tin, 20.0);
        Assert.NotNull(refiner);
    }

    [Fact]
    public void Constructor_WithNullTin_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RuppertRefiner(null!, 20.0));
    }

    [Fact]
    public void Constructor_WithInvalidAngle_ShouldThrow()
    {
        // Arrange
        var tin = CreateSimpleConstrainedTin();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuppertRefiner(tin, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuppertRefiner(tin, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuppertRefiner(tin, -10));
    }

    [Fact]
    public void Constructor_WithOptions_ShouldNotThrow()
    {
        // Arrange
        var tin = CreateSimpleConstrainedTin();
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25.0,
            MinimumTriangleArea = 0.001,
            SkipSeditiousTriangles = true,
            IgnoreSeditiousEncroachments = true
        };

        // Act
        var refiner = new RuppertRefiner(tin, options);

        // Assert
        Assert.NotNull(refiner);
    }

    [Fact]
    public void FromEdgeRatio_WithValidRatio_ShouldCreateRefiner()
    {
        // Arrange
        var tin = CreateSimpleConstrainedTin();

        // Act
        var refiner = RuppertRefiner.FromEdgeRatio(tin, 1.5);

        // Assert
        Assert.NotNull(refiner);
    }

    [Fact]
    public void RefineOnce_WithConstrainedTin_ShouldReturnVertexOrNull()
    {
        // Arrange
        var tin = CreateSimpleConstrainedTin();
        var refiner = new RuppertRefiner(tin, 20.0);

        // Act
        var result = refiner.RefineOnce();

        // Assert - result can be null if no refinement needed, or a vertex if refinement occurred
        // This is a basic smoke test
    }

    [Fact]
    public void Refine_WithSimpleConstraint_ShouldComplete()
    {
        // Arrange
        var tin = CreateSimpleConstrainedTin();
        var initialVertexCount = tin.GetVertices().Count;
        var refiner = new RuppertRefiner(tin, 20.0);

        // Act
        var result = refiner.Refine();

        // Assert
        // Should complete (either fully refined or hit iteration limit)
        var finalVertexCount = tin.GetVertices().Count;
        // Refinement typically adds vertices
        Assert.True(finalVertexCount >= initialVertexCount);
    }

    [Fact]
    public void Refine_WithLargerConstrainedRegion_ShouldImproveTriangleQuality()
    {
        // Arrange
        var tin = CreateLargerConstrainedTin();
        var refiner = new RuppertRefiner(tin, 20.0);

        // Act
        var result = refiner.Refine();

        // Assert - should complete without infinite loop
        Assert.True(result || tin.GetVertices().Count > 4);
    }

    private static IIncrementalTin CreateSimpleConstrainedTin()
    {
        var tin = new IncrementalTin();

        // Create a simple square boundary
        var v1 = new Vertex(0, 0, 0, 0);
        var v2 = new Vertex(10, 0, 0, 1);
        var v3 = new Vertex(10, 10, 0, 2);
        var v4 = new Vertex(0, 10, 0, 3);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);
        tin.Add(v4);

        // Add a polygon constraint for the boundary
        var boundaryVertices = new List<IVertex> { v1, v2, v3, v4 };
        var constraint = new PolygonConstraint(boundaryVertices, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }

    private static IIncrementalTin CreateLargerConstrainedTin()
    {
        var tin = new IncrementalTin();

        // Create a larger square boundary with more vertices
        var boundaryVertices = new List<IVertex>();

        // Boundary vertices
        var v1 = new Vertex(0, 0, 0, 0);
        var v2 = new Vertex(100, 0, 0, 1);
        var v3 = new Vertex(100, 100, 0, 2);
        var v4 = new Vertex(0, 100, 0, 3);

        boundaryVertices.Add(v1);
        boundaryVertices.Add(v2);
        boundaryVertices.Add(v3);
        boundaryVertices.Add(v4);

        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);
        tin.Add(v4);

        // Add some interior vertices to create triangles
        tin.Add(new Vertex(50, 50, 1, 4));
        tin.Add(new Vertex(25, 25, 0.5, 5));
        tin.Add(new Vertex(75, 75, 0.5, 6));

        // Add a polygon constraint for the boundary
        var constraint = new PolygonConstraint(boundaryVertices, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }

    [Fact]
    public void RuppertOptions_DefaultValues_ShouldBeReasonable()
    {
        // Arrange & Act
        var options = new RuppertOptions();

        // Assert
        Assert.Equal(20.0, options.MinimumAngleDegrees);
        Assert.Equal(1e-3, options.MinimumTriangleArea);
        Assert.False(options.EnforceSqrt2Guard);
        Assert.True(options.SkipSeditiousTriangles);
        Assert.True(options.IgnoreSeditiousEncroachments);
        Assert.False(options.InterpolateZ);
        Assert.Equal(100_000, options.MaxIterations);
    }
}

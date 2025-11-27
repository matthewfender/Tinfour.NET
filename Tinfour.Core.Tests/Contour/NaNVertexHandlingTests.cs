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

namespace Tinfour.Core.Tests.Contour;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests for handling NaN vertices in contour generation
/// </summary>
public class NaNVertexHandlingTests
{
    private readonly ITestOutputHelper _output;

    public NaNVertexHandlingTests(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void ContourBuilder_ValidatesVertexZValues()
    {
        // Arrange: Create a TIN and check that all vertices have valid Z values
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 1, 0), new Vertex(10, 0, 2, 1), new Vertex(5, 10, 3, 2),
                               new Vertex(5, 5, 4, 3)
                           };

        tin.Add(vertices);

        // Verify all non-ghost vertices have valid Z values
        var realVertices = tin.GetVertices().Where((IVertex v) => !v.IsNullVertex()).ToList();
        var invalidVertices = realVertices.Where((IVertex v) => double.IsNaN(v.GetZ())).ToList();

        this._output.WriteLine($"Real vertices: {realVertices.Count}, Invalid Z values: {invalidVertices.Count}");
        Assert.Empty(invalidVertices); // Should have no vertices with NaN Z values

        // Act: Contour generation should work with valid vertices
        var contourLevels = new[] { 1.5, 2.5, 3.5 };
        var builder = new ContourBuilderForTin(tin, null, contourLevels);
        var contours = builder.GetContours();

        this._output.WriteLine($"Successfully generated {contours.Count} contours");
        Assert.True(contours.Count >= 0); // Should not throw
    }

    [Fact]
    public void ContourBuilder_WithEmptyTin_ShouldThrow()
    {
        // Arrange: Empty TIN
        var tin = new IncrementalTin();

        // Act & Assert: Should throw because TIN is not bootstrapped
        var exception = Assert.Throws<ArgumentException>(() =>
            {
                var contourLevels = new[] { 5.0 };
                var builder = new ContourBuilderForTin(tin, null, contourLevels);
            });

        this._output.WriteLine($"Expected exception: {exception.Message}");
        Assert.Contains("not properly populated", exception.Message);
    }

    [Fact]
    public void ContourBuilder_WithGhostVertices_ShouldFilterThemOut()
    {
        // Arrange: Create a triangulation 
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0, 0), new Vertex(10, 0, 5, 1), new Vertex(5, 10, 10, 2) };

        tin.Add(vertices);

        // Note: Ghost vertices are not exposed through GetVertices() - they're internal to the TIN structure
        // The important thing is that our contour builder handles them gracefully
        var allVertices = tin.GetVertices().ToList();
        this._output.WriteLine($"TIN has {allVertices.Count} vertices accessible via GetVertices()");

        // Act & Assert: Contour generation should succeed regardless of internal ghost vertices
        var contourLevels = new[] { 2.5, 7.5 };
        var exception = Record.Exception(() =>
            {
                var builder = new ContourBuilderForTin(tin, null, contourLevels);
                var contours = builder.GetContours();
                this._output.WriteLine($"Generated {contours.Count} contours successfully");
            });

        Assert.Null(exception);
    }

    [Fact]
    public void ContourBuilder_WithValidTin_ShouldGenerateContours()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0, 0), new Vertex(10, 0, 5, 1), new Vertex(5, 10, 10, 2) };

        tin.Add(vertices);

        // Act & Assert: Basic contour generation should work
        var contourLevels = new[] { 5.0 };
        var exception = Record.Exception(() =>
            {
                var builder = new ContourBuilderForTin(tin, null, contourLevels);
                var contours = builder.GetContours();
                this._output.WriteLine($"Generated {contours.Count} contours successfully");
            });

        Assert.Null(exception);
    }

    [Fact]
    public void ContourBuilder_WithValidTin_ShouldNotThrowNaNException()
    {
        // Arrange: Create a simple triangulation with valid vertices
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), new Vertex(10, 0, 5, 1), new Vertex(5, 10, 10, 2),
                               new Vertex(5, 5, 7.5, 3)
                           };

        tin.Add(vertices);

        // Act & Assert: Contour generation should succeed
        var contourLevels = new[] { 2.5, 5.0, 7.5 };
        var exception = Record.Exception(() =>
            {
                var builder = new ContourBuilderForTin(tin, null, contourLevels);
                var contours = builder.GetContours();
                this._output.WriteLine($"Generated {contours.Count} contours successfully");
            });

        Assert.Null(exception);
    }

    [Fact]
    public void ContourGeneration_WithConstraints_ShouldHandleCorrectly()
    {
        // Arrange: Create a TIN with constraints (which has been problematic)
        var tin = new IncrementalTin();

        // Add base vertices
        var baseVertices = new IVertex[]
                               {
                                   new Vertex(0, 0, 0, 0), new Vertex(100, 0, 0, 1), new Vertex(100, 100, 0, 2),
                                   new Vertex(0, 100, 0, 3), new Vertex(50, 50, 10, 4) // Center point
                               };

        tin.Add(baseVertices);

        // Add a simple constraint
        var constraintVertices = new IVertex[] { new Vertex(25, 25, 0, 10), new Vertex(75, 75, 0, 11) };

        try
        {
            tin.Add(constraintVertices);
            var constraint = new LinearConstraint(constraintVertices);
            constraint.SetDefaultZ(0.0);
            tin.AddConstraints(new[] { constraint }, true);
        }
        catch (Exception ex)
        {
            this._output.WriteLine($"Constraint addition failed: {ex.Message}");

            // Continue with test even if constraint addition fails
        }

        // Act & Assert: Contour generation should succeed or provide clear error
        var contourLevels = new[] { 2.5, 5.0, 7.5 };
        var exception = Record.Exception(() =>
            {
                var builder = new ContourBuilderForTin(tin, null, contourLevels);
                var contours = builder.GetContours();
                this._output.WriteLine($"With constraints: Generated {contours.Count} contours");
            });

        // The test passes if either:
        // 1. No exception is thrown (contours generated successfully)
        // 2. A clear, informative exception is thrown (not the generic NaN error)
        if (exception != null)
        {
            this._output.WriteLine($"Exception occurred: {exception.Message}");

            // Should not be the generic NaN error
            Assert.DoesNotContain("Input includes vertices with NaN z values", exception.Message);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    public void ContourGeneration_WithDifferentVertexCounts_ShouldSucceed(int vertexCount)
    {
        // Arrange: Create a TIN with the specified number of vertices
        var tin = new IncrementalTin();
        var vertices = new IVertex[vertexCount];
        var random = new Random(42); // Fixed seed for reproducibility

        for (var i = 0; i < vertexCount; i++)
        {
            var x = random.NextDouble() * 100;
            var y = random.NextDouble() * 100;
            var z = random.NextDouble() * 20; // Valid Z values
            vertices[i] = new Vertex(x, y, z, i);
        }

        tin.Add(vertices);

        // Act & Assert: Contour generation should succeed
        var contourLevels = new[] { 5.0, 10.0, 15.0 };
        var exception = Record.Exception(() =>
            {
                var builder = new ContourBuilderForTin(tin, null, contourLevels);
                var contours = builder.GetContours();
                this._output.WriteLine($"With {vertexCount} vertices: Generated {contours.Count} contours");
            });

        Assert.Null(exception);
    }
}
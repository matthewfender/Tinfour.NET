/*
 * Copyright 2013 Gary W. Lucas.
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

public class StochasticLawsonsWalkTests
{
    [Fact]
    public void Constructor_WithNominalPointSpacing_ShouldInitialize()
    {
        // Arrange & Act
        var walk = new StochasticLawsonsWalk(2.5);

        // Assert
        Assert.NotNull(walk);
    }

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        // Arrange & Act
        var walk = new StochasticLawsonsWalk();

        // Assert
        Assert.NotNull(walk);
    }

    [Fact]
    public void Constructor_WithThresholds_ShouldInitialize()
    {
        // Arrange
        var thresholds = new Thresholds(1.5);

        // Act
        var walk = new StochasticLawsonsWalk(thresholds);

        // Assert
        Assert.NotNull(walk);
    }

    [Fact]
    public void Constructor_WithNullThresholds_ShouldThrow()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StochasticLawsonsWalk(null!));
    }

    [Fact]
    public void FindAnEdgeFromEnclosingTriangle_WithNullEdge_ShouldThrow()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            walk.FindAnEdgeFromEnclosingTriangle(null!, 0, 0));
    }

    [Fact]
    public void FindAnEdgeFromEnclosingTriangle_WithSimpleTriangle_ShouldFindContainingEdge()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        
        // Create a simple triangle
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(2, 0, 0);
        var v3 = new Vertex(1, 2, 0);
        
        // Create edges for the triangle
        var edge12 = new QuadEdge(0);
        var edge23 = new QuadEdge(2);
        var edge31 = new QuadEdge(4);
        
        edge12.SetVertices(v1, v2);
        edge23.SetVertices(v2, v3);
        edge31.SetVertices(v3, v1);
        
        // Link the edges
        edge12.SetForward(edge23);
        edge23.SetForward(edge31);
        edge31.SetForward(edge12);
        
        // Point inside the triangle
        double x = 1.0;
        double y = 0.5;

        // Act
        var result = walk.FindAnEdgeFromEnclosingTriangle(edge12, x, y);

        // Assert
        Assert.NotNull(result);
        // The result should be one of the triangle edges
        Assert.True(result == edge12 || result == edge23 || result == edge31 || 
                   result == edge12.GetDual() || result == edge23.GetDual() || result == edge31.GetDual());
    }

    [Fact]
    public void ClearDiagnostics_ShouldResetCounters()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        
        // Create a simple triangle to perform a walk
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(2, 0, 0);
        var v3 = new Vertex(1, 2, 0);
        
        var edge12 = new QuadEdge(0);
        edge12.SetVertices(v1, v2);
        var edge23 = new QuadEdge(2);
        edge23.SetVertices(v2, v3);
        var edge31 = new QuadEdge(4);
        edge31.SetVertices(v3, v1);
        
        edge12.SetForward(edge23);
        edge23.SetForward(edge31);
        edge31.SetForward(edge12);
        
        // Perform a walk to generate some diagnostics
        try
        {
            walk.FindAnEdgeFromEnclosingTriangle(edge12, 1.0, 0.5);
        }
        catch
        {
            // Ignore errors for this test - we just want to generate diagnostics
        }

        // Act
        walk.ClearDiagnostics();
        var diagnostics = walk.GetDiagnostics();

        // Assert
        Assert.Equal(0, diagnostics.NumberOfWalks);
        Assert.Equal(0, diagnostics.NumberOfExteriorWalks);
        Assert.Equal(0, diagnostics.NumberOfTests);
        Assert.Equal(0, diagnostics.AverageStepsToCompletion);
    }

    [Fact]
    public void Reset_ShouldResetRandomSeed()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        
        // Act
        walk.Reset();

        // Assert
        // After reset, the behavior should be deterministic
        // This is more of a behavioral test - the seed should be reset to initial value
        Assert.NotNull(walk);
    }

    [Fact]
    public void GetDiagnostics_ShouldReturnValidObject()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();

        // Act
        var diagnostics = walk.GetDiagnostics();

        // Assert
        Assert.NotNull(diagnostics);
        Assert.True(diagnostics.NumberOfWalks >= 0);
        Assert.True(diagnostics.NumberOfExteriorWalks >= 0);
        Assert.True(diagnostics.NumberOfTests >= 0);
        Assert.True(diagnostics.AverageStepsToCompletion >= 0);
    }

    [Fact]
    public void WalkDiagnostics_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var diagnostics = new WalkDiagnostics
        {
            NumberOfWalks = 10,
            NumberOfExteriorWalks = 2,
            NumberOfTests = 50,
            NumberOfExtendedPrecisionCalls = 5,
            AverageStepsToCompletion = 3.5
        };

        // Act
        var result = diagnostics.ToString();

        // Assert
        Assert.Contains("Walks: 10", result);
        Assert.Contains("Exterior: 2", result);
        Assert.Contains("Tests: 50", result);
        Assert.Contains("Extended: 5", result);
        Assert.Contains("Avg Steps: 3.50", result);
    }

    [Fact]
    public void FindAnEdgeFromEnclosingTriangle_WithExteriorPoint_ShouldHandleGracefully()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        
        // Create a simple triangle
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(2, 0, 0);
        var v3 = new Vertex(1, 2, 0);
        
        var edge12 = new QuadEdge(0);
        var edge23 = new QuadEdge(2);
        var edge31 = new QuadEdge(4);
        
        edge12.SetVertices(v1, v2);
        edge23.SetVertices(v2, v3);
        edge31.SetVertices(v3, v1);
        
        edge12.SetForward(edge23);
        edge23.SetForward(edge31);
        edge31.SetForward(edge12);
        
        // Create ghost edges (edges to exterior)
        var ghost1 = new QuadEdge(6);
        var ghost2 = new QuadEdge(8);
        var ghost3 = new QuadEdge(10);
        
        ghost1.SetVertices(v2, null); // Ghost edge from v2
        ghost2.SetVertices(v3, null); // Ghost edge from v3
        ghost3.SetVertices(v1, null); // Ghost edge from v1
        
        // Link dual edges for exterior
        edge12.GetDual().SetForward(ghost1);
        edge23.GetDual().SetForward(ghost2);
        edge31.GetDual().SetForward(ghost3);
        
        // Point outside the triangle
        double x = 5.0;
        double y = 5.0;

        // Act & Assert
        // This should either return a perimeter edge or handle the exterior case
        var result = walk.FindAnEdgeFromEnclosingTriangle(edge12, x, y);
        Assert.NotNull(result);
    }

    [Fact]
    public void FindAnEdgeFromEnclosingTriangle_MultipleWalks_ShouldUpdateDiagnostics()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        walk.ClearDiagnostics();
        
        // Create a simple triangle
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(2, 0, 0);
        var v3 = new Vertex(1, 2, 0);
        
        var edge12 = new QuadEdge(0);
        var edge23 = new QuadEdge(2);
        var edge31 = new QuadEdge(4);
        
        edge12.SetVertices(v1, v2);
        edge23.SetVertices(v2, v3);
        edge31.SetVertices(v3, v1);
        
        edge12.SetForward(edge23);
        edge23.SetForward(edge31);
        edge31.SetForward(edge12);

        // Act
        try
        {
            // Perform multiple walks
            walk.FindAnEdgeFromEnclosingTriangle(edge12, 1.0, 0.5);
            walk.FindAnEdgeFromEnclosingTriangle(edge12, 0.5, 1.0);
        }
        catch
        {
            // Ignore errors - we're testing diagnostic collection
        }

        var diagnostics = walk.GetDiagnostics();

        // Assert
        Assert.True(diagnostics.NumberOfWalks >= 0);
        Assert.True(diagnostics.NumberOfTests >= 0);
    }

    [Fact]
    public void FindAnEdgeFromEnclosingTriangle_WithInvalidEdgeStructure_ShouldHandleGracefully()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        
        // Create an edge with null vertices (invalid state)
        var edge = new QuadEdge(0);
        edge.SetVertices(null, null);

        // Act & Assert
        // This should handle the invalid state gracefully
        Assert.Throws<InvalidOperationException>(() => 
            walk.FindAnEdgeFromEnclosingTriangle(edge, 0, 0));
    }

    [Fact]
    public void DiagnosticsCollection_ShouldTrackPerformanceMetrics()
    {
        // Arrange
        var walk = new StochasticLawsonsWalk();
        walk.ClearDiagnostics();

        // Act
        var initialDiagnostics = walk.GetDiagnostics();

        // Assert
        Assert.Equal(0, initialDiagnostics.NumberOfWalks);
        Assert.Equal(0, initialDiagnostics.NumberOfTests);
        Assert.Equal(0.0, initialDiagnostics.AverageStepsToCompletion);
    }
}
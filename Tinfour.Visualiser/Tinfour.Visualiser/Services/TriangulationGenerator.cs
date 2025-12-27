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

namespace Tinfour.Visualiser.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Tinfour.Core.Common;
using Tinfour.Core.Diagnostics;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     Service for generating various types of triangulation test data.
/// </summary>
public class TriangulationGenerator
{
    /// <summary>
    ///     Adds concentric circle constraints to an existing triangulation.
    /// </summary>
    /// <param name="tin">The existing triangulation to add constraints to</param>
    /// <param name="width">Width of the area (used for circle sizing)</param>
    /// <param name="height">Height of the area (used for circle sizing)</param>
    /// <param name="preInterpolateZ">Whether to pre-interpolate Z values for the constraints</param>
    /// <returns>A result with updated constraint information</returns>
    public static TriangulationResult AddConcentricCircleConstraints(IncrementalTin tin, double width, double height, bool preInterpolateZ = false)
    {
        var stopwatch = Stopwatch.StartNew();

        // Store a copy of the original vertices before modifications
        var originalVertices = tin.GetVertices().ToList();

        // Get the bounds of the existing triangulation to center the circles
        var bounds = tin.GetBounds();
        if (!bounds.HasValue)
            throw new InvalidOperationException("Cannot add constraints to triangulation without bounds");

        var (left, top, tinWidth, tinHeight) = bounds.Value;
        var centerX = left + tinWidth / 2.0;
        var centerY = top + tinHeight / 2.0;

        // Calculate circle radii based on the smaller dimension to ensure circles fit
        var maxRadius = Math.Min(tinWidth, tinHeight) * 0.4; // Outer circle is 80% of smallest dimension
        var innerRadius = maxRadius * 0.67; // Inner circle is 2/3 the size

        // Create vertices for the circles - use starting indices that are far from existing vertices
        var baseIndex = 10000;

        // For boundary polygons, use counter-clockwise winding (false = not a hole)
        var outerCircleVertices = CreateCircleVertices(centerX, centerY, maxRadius, 32, false, baseIndex);

        // For hole polygons, use clockwise winding (true = hole)
        var innerCircleVertices = CreateCircleVertices(centerX, centerY, innerRadius, 24, true, baseIndex + 1000);

        // Add all new vertices to our tracking list
        var allNewVertices = new List<IVertex>();
        allNewVertices.AddRange(outerCircleVertices);
        allNewVertices.AddRange(innerCircleVertices);

        Debug.WriteLine($"Adding {allNewVertices.Count} circle vertices to triangulation");

        // Create polygon constraints for the circles
        // Outer circle is the main constraint region
        var outerCircleConstraint = new PolygonConstraint(outerCircleVertices, definesRegion: true, isHole: false);
        
        // Inner circle is a hole within the outer region
        var innerCircleConstraint = new PolygonConstraint(innerCircleVertices, definesRegion: true, isHole: true);

        // Store all vertices for potential recovery
        var allVertices = new List<IVertex>();
        allVertices.AddRange(originalVertices);
        allVertices.AddRange(allNewVertices);

        // Process polygon constraints first, then line constraints (as in Java implementation)
        // First create list of polygon constraints
        var polygonConstraints = new List<IConstraint>();
        polygonConstraints.Add(outerCircleConstraint);
        polygonConstraints.Add(innerCircleConstraint);

        // No line constraints in this demo, but if there were, they would be in a separate list
        var lineConstraints = new List<IConstraint>();

        // Combined list with polygons first, then lines (matching Java implementation)
        var allConstraints = new List<IConstraint>();
        allConstraints.AddRange(polygonConstraints);
        allConstraints.AddRange(lineConstraints);

        Debug.WriteLine(
            $"Adding {allConstraints.Count} constraints to triangulation (polygons: {polygonConstraints.Count}, lines: {lineConstraints.Count})");

        var constraintAdditionFailed = false;
        try
        {
            // Add all constraints with conformity restoration
            tin.AddConstraints(allConstraints, true, preInterpolateZ);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding constraints: {ex.Message}");
            constraintAdditionFailed = true;
        }
        

        var additionTime = stopwatch.Elapsed;

        // Validate triangulation after adding constraints
        Debug.WriteLine("Triangulation state AFTER adding constraints:");

        // If constraint addition failed or the TIN has no triangles, attempt recovery
        var triangleCount = tin.CountTriangles();
        if (constraintAdditionFailed || triangleCount.ValidTriangles == 0)
        {
            Debug.WriteLine("Constraint addition failed or resulted in no triangles. Attempting recovery...");

            try
            {
                // Create a new TIN with all the vertices
                var recoveredTin = new IncrementalTin();

                // Add all vertices first
                foreach (var vertex in allVertices)
                    try
                    {
                        recoveredTin.Add(vertex);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Recovery: Error adding vertex: {ex.Message}");
                    }

                // Add polygon constraints first, then line constraints (one by one)
                foreach (var constraint in polygonConstraints)
                    try
                    {
                        recoveredTin.AddConstraints(new[] { constraint }, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Recovery: Error adding constraint: {ex.Message}");
                    }

                foreach (var constraint in lineConstraints)
                    try
                    {
                        recoveredTin.AddConstraints(new[] { constraint }, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Recovery: Error adding constraint: {ex.Message}");
                    }

                // Check if recovery was successful
                var recoveredTriangleCount = recoveredTin.CountTriangles();
                if (recoveredTriangleCount.ValidTriangles > 0)
                {
                    Debug.WriteLine($"Recovery successful! Restored {recoveredTriangleCount.ValidTriangles} triangles");
                    tin = recoveredTin; // Use the recovered TIN
                    triangleCount = recoveredTriangleCount;
                }
                else
                {
                    Debug.WriteLine("Recovery failed to restore triangles.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Recovery attempt failed: {ex.Message}");
            }
        }

        stopwatch.Stop();

        // Count constrained edges for verification using proper checks
        var edgeCount = 0;
        var invalidEdgeCount = 0;
        var interiorEdgeCount = 0;
        var borderEdgeCount = 0;

        Debug.WriteLine("Examining edges for constraint flags:");
        foreach (var edge in tin.GetEdges())
            try
            {
                // Skip ghost edges
                if (edge.GetB().IsNullVertex())
                    continue;

                var vertexA = edge.GetA();
                var vertexB = edge.GetB();

                // Additional validation to detect edges with invalid vertices
                if (vertexA.IsNullVertex())
                {
                    invalidEdgeCount++;
                    Debug.WriteLine($"Found invalid edge: A={vertexA.GetIndex()}, B={vertexB.GetIndex()}");
                    continue;
                }

                // Check if the edge is constrained
                if (edge.IsConstraintRegionInterior())
                {
                    interiorEdgeCount++;
                    Debug.WriteLine(
                        $"Found interior edge: {vertexA.GetIndex()}-{vertexB.GetIndex()}, "
                        + $"Interior idx: {edge.GetConstraintRegionInteriorIndex()}");
                }

                if (edge.IsConstraintRegionBorder())
                {
                    borderEdgeCount++;
                    Debug.WriteLine(
                        $"Found border edge: {vertexA.GetIndex()}-{vertexB.GetIndex()}, "
                        + $"Border idx: {edge.GetConstraintBorderIndex()}");
                }

                if (edge.IsConstrained()) edgeCount++;
            }
            catch (Exception ex)
            {
                invalidEdgeCount++;
                Debug.WriteLine($"Exception examining edge: {ex.Message}");
            }

        Debug.WriteLine($"Total constrained edges: {edgeCount}");
        Debug.WriteLine($"Border edges: {borderEdgeCount}, Interior edges: {interiorEdgeCount}");
        Debug.WriteLine($"Invalid edges: {invalidEdgeCount}");
        Debug.WriteLine(
            $"Triangle counts - Valid: {triangleCount.ValidTriangles}, "
            + $"Ghost: {triangleCount.GhostTriangles}, Constrained: {triangleCount.ConstrainedTriangles}");

        var result = MeshValidator.Validate(tin);
        Debug.WriteLine(result);

        return new TriangulationResult
                   {
                       Tin = tin,
                       VertexCount = tin.GetVertices().Count,
                       GenerationTime = TimeSpan.Zero, // No generation, just constraint addition
                       TriangulationTime = additionTime,
                       TriangleCount = triangleCount,
                       Bounds = tin.GetBounds(),
                       ConstraintCount = tin.GetConstraints().Count,
                       ConstrainedEdgeCount = edgeCount
                   };
    }

    public static TriangulationResult GenerateConstrainedTest(
        int pointCount = 1000,
        double width = 800,
        double height = 600,
        int seed = 42)
    {
        var stopwatch = Stopwatch.StartNew();
        var vertices = new List<IVertex>();

        // Create a simple triangulation first - use a uniform grid with more points
        var rows = 3;
        var cols = 3;
        var xSpace = width / (cols - 1);
        var ySpace = height / (rows - 1);

        // Place grid vertices
        for (var i = 0; i < cols; i++)
        for (var j = 0; j < rows; j++)
        {
            var x = i * xSpace;
            var y = j * ySpace;
            var z = (i + j) * 0.5; // More gradual elevation change
            vertices.Add(new Vertex(x, y, z, i * rows + j));
        }

        var generationTime = stopwatch.Elapsed;
        stopwatch.Restart();

        // Create TIN with an appropriate nominal spacing
        var nominalSpacing = Math.Min(xSpace, ySpace) / 2;
        var tin = new IncrementalTin(nominalSpacing);
        tin.PreAllocateForVertices(vertices.Count);

        // Add all vertices to the TIN
        var success = tin.Add(vertices);

        if (!success) throw new InvalidOperationException("Failed to bootstrap triangulation with grid vertices");

        var triangulationTime = stopwatch.Elapsed;
        stopwatch.Restart();

        // Define constraint vertices with slight inset to ensure they're inside the grid
        var inset = Math.Min(xSpace, ySpace) * 0.1;

        // Create boundary constraint (rectangle) with explicit indices for debugging
        var boundaryVertices = new IVertex[]
                                   {
                                       new Vertex(inset, inset, 0, 100), // Bottom-left
                                       new Vertex(width - inset, inset, 0, 101), // Bottom-right
                                       new Vertex(width - inset, height - inset, 0, 102), // Top-right
                                       new Vertex(inset, height - inset, 0, 103) // Top-left
                                   };

        var boundary = new PolygonConstraint(boundaryVertices);
        boundary.SetDefaultZ(0.0); // Set default Z for interpolation

        // Create two diagonal constraints with explicit indices
        var linearConstraints = new IConstraint[]
                                    {
                                        new LinearConstraint(
                                            [
                                                new Vertex(width - inset * 2, inset * 2, 0, 300),
                                                new Vertex(inset * 2, height - inset * 2, 0, 301)
                                            ]),
                                        new LinearConstraint(
                                            [
                                                new Vertex(inset * 2, inset * 2, 0, 200),
                                                new Vertex(width - inset * 2, height - inset * 2, 0, 201)
                                            ]),
                                        boundary
                                    };

        // Set default Z for all linear constraints
        foreach (var constraint in linearConstraints) constraint.SetDefaultZ(0.0);

        try
        {
            Debug.WriteLine("Adding linear constraint vertices to TIN:");
            foreach (var c in linearConstraints)
            foreach (var v in c.GetVertices())
            {
                Debug.WriteLine($"  Adding constraint vertex at ({v.X:F1}, {v.Y:F1}), index {v.GetIndex()}");
                tin.Add(v);
            }

            // Then add linear constraints
            Debug.WriteLine("Adding linear constraints");
            tin.AddConstraints(linearConstraints, true);

            // Debug output for constraints
            var constraints = tin.GetConstraints();
            Debug.WriteLine($"Total constraints added: {constraints.Count}");
            foreach (var c in constraints)
                Debug.WriteLine(
                    $"Constraint index: {c.GetConstraintIndex()}, " + $"Is region: {c.DefinesConstrainedRegion()}, "
                                                                    + $"Vertex count: {c.GetVertices().Count}");

            // Debug output for constrained edges
            var constrainedEdgeCount = 0;
            foreach (var edge in tin.GetEdges())
                if (edge.IsConstraintLineMember())
                {
                    constrainedEdgeCount++;
                    var vertexA = edge.GetA();
                    var vertexB = edge.GetB();
                    Debug.WriteLine(
                        $"Constrained edge: {vertexA.GetIndex()}-{vertexB.GetIndex()}, "
                        + $"BorderIdx: {edge.GetConstraintBorderIndex()}, LineIdx: {edge.GetConstraintLineIndex()}");
                }

            Debug.WriteLine($"Total constrained edges: {constrainedEdgeCount}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ERROR adding constraints: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);

            // Continue without constraints if there's an error
        }

        var constraintTime = stopwatch.Elapsed;
        stopwatch.Stop();

        // Count constrained edges for verification
        var constrainedEdgeCount2 = 0;
        foreach (var edge in tin.GetEdges())
            if (!edge.GetB().IsNullVertex() && (edge.IsConstraintRegionBorder() || edge.IsConstraintLineMember()))
                constrainedEdgeCount2++;

        // Get triangle counts
        var triangleCount = tin.CountTriangles();
        Debug.WriteLine(
            $"Triangle counts - Valid: {triangleCount.ValidTriangles}, "
            + $"Ghost: {triangleCount.GhostTriangles}, Constrained: {triangleCount.ConstrainedTriangles}");

        var c2 = tin.GetConstraints();

        return new TriangulationResult
                   {
                       Tin = tin,
                       VertexCount = tin.GetVertices().Count,
                       ActualRows = rows,
                       ActualCols = cols,
                       GenerationTime = generationTime,
                       TriangulationTime = triangulationTime,
                       TriangleCount = triangleCount,
                       Bounds = tin.GetBounds(),
                       ConstraintCount = c2.Count,
                       ConstrainedEdgeCount = constrainedEdgeCount2
                   };
    }

    public static TriangulationResult GenerateConstrainedTest2(
        int pointCount = 1000,
        double width = 800,
        double height = 600)
    {
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(width, 0, 1, 1),
                               new Vertex(width, height, 2, 2),
                               new Vertex(0, height, 3, 3),
                               new Vertex(width / 2, height / 2, 4, 4)
                           };

        var constraintVertices = new List<IVertex>
                                     {
                                         new Vertex(0, height / 2, 5, 5),
                                         new Vertex(width / 2, 0, 6, 6),
                                         new Vertex(width, height / 2, 7, 7),
                                         new Vertex(width / 2, height, 8, 8)
                                     };

        var tin = new IncrementalTin(1);
        tin.PreAllocateForVertices(vertices.Count);
        var success = tin.AddSorted(vertices);

        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddSorted(constraintVertices);
        constraint.Complete();
        tin.AddConstraints([constraint], true);

        var result = MeshValidator.Validate(tin);
        return new TriangulationResult
                   {
                       Tin = tin,
                       VertexCount = tin.GetVertices().Count,
                       TriangleCount = tin.CountTriangles(),
                       Bounds = tin.GetBounds()
                   };
    }

    public static TriangulationResult GenerateGrid(int rows, int cols, double width = 800, double height = 600)
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();

        // Create a 5x5 grid of vertices (25 vertices total)
        for (var i = 0; i < cols; i++)
        for (var j = 0; j < rows; j++)
            vertices.Add(new Vertex(i * width / cols, j * width / rows, i + j));

        tin.Add(vertices);
        return new TriangulationResult { Tin = tin, VertexCount = vertices.Count };
    }

    /// <summary>
    ///     Generates a simple test triangulation with a smaller number of points.
    /// </summary>
    /// <param name="pointCount">Number of points to generate</param>
    /// <param name="width">Width of the area</param>
    /// <param name="height">Height of the area</param>
    /// <param name="seed">Random seed</param>
    /// <returns>A simple triangulation for testing</returns>
    public static TriangulationResult GenerateSimpleTest(
        int pointCount = 5,
        double width = 800,
        double height = 600,
        int seed = 42)
    {
        var stopwatch = Stopwatch.StartNew();
        var vertices = new List<IVertex>();

        // Create a simple triangle + center point for basic testing
        if (pointCount <= 3)
        {
            vertices.Add(new Vertex(200, 150, 0, 0)); // Triangle vertex 1
            vertices.Add(new Vertex(600, 150, 0, 1)); // Triangle vertex 2
            vertices.Add(new Vertex(400, 450, 0, 2)); // Triangle vertex 3

            // Trim to exact count
            while (vertices.Count > pointCount) vertices.RemoveAt(vertices.Count - 1);
        }
        else if (pointCount <= 10)
        {
            // Add a few more predictable points
            vertices.Add(new Vertex(200, 150, 0, 0)); // Triangle vertex 1
            vertices.Add(new Vertex(600, 150, 0, 1)); // Triangle vertex 2
            vertices.Add(new Vertex(400, 450, 0, 2)); // Triangle vertex 3
            vertices.Add(new Vertex(400, 250, 5, 3)); // Center point

            if (pointCount > 4) vertices.Add(new Vertex(300, 200, 2, 4)); // Extra point 1
            if (pointCount > 5) vertices.Add(new Vertex(500, 300, 3, 5)); // Extra point 2
            if (pointCount > 6) vertices.Add(new Vertex(350, 350, 1, 6)); // Extra point 3

            // Trim to exact count
            while (vertices.Count > pointCount) vertices.RemoveAt(vertices.Count - 1);
        }
        else
        {
            // For larger counts, use random but seeded generation
            var random = new Random(seed);
            for (var i = 0; i < pointCount; i++)
            {
                var x = random.NextDouble() * width;
                var y = random.NextDouble() * height;
                var z = random.NextDouble() * 10; // Simple random elevation

                vertices.Add(new Vertex(x, y, z, i));
            }
        }

        var generationTime = stopwatch.Elapsed;
        stopwatch.Restart();

        // Create TIN
        var tin2 = new IncrementalTin(20.0);
        tin2.PreAllocateForVertices(vertices.Count);
        var success2 = tin2.AddSorted(vertices);

        var triangulationTime2 = stopwatch.Elapsed;
        stopwatch.Stop();

        if (!success2) throw new InvalidOperationException("Failed to bootstrap triangulation");

        return new TriangulationResult
                   {
                       Tin = tin2,
                       VertexCount = vertices.Count,
                       GenerationTime = generationTime,
                       TriangulationTime = triangulationTime2,
                       TriangleCount = tin2.CountTriangles(),
                       Bounds = tin2.GetBounds()
                   };
    }

    /// <summary>
    ///     Generates terrain data using sin/cos functions as described in the usage documentation.
    /// </summary>
    /// <param name="pointCount">Number of points to generate</param>
    /// <param name="width">Width of the terrain area</param>
    /// <param name="height">Height of the terrain area</param>
    /// <param name="seed">Random seed for reproducible results</param>
    /// <returns>A triangulated TIN with terrain data</returns>
    public static TriangulationResult GenerateTerrainData(
        int pointCount = 25,
        double width = 800,
        double height = 600,
        int seed = 42)
    {
        var stopwatch = Stopwatch.StartNew();

        var vertices = new List<IVertex>();

        // For debugging: create a simple, predictable set of vertices
        if (pointCount <= 10)
        {
            // Create a simple rectangle with some interior points
            vertices.Add(new Vertex(100, 100, 0, 0)); // Bottom-left
            vertices.Add(new Vertex(700, 100, 0, 1)); // Bottom-right
            vertices.Add(new Vertex(700, 500, 0, 2)); // Top-right
            vertices.Add(new Vertex(100, 500, 0, 3)); // Top-left

            if (pointCount > 4) vertices.Add(new Vertex(400, 300, 10, 4)); // Center point
            if (pointCount > 5) vertices.Add(new Vertex(300, 200, 5, 5)); // Interior point 1
            if (pointCount > 6) vertices.Add(new Vertex(500, 400, 5, 6)); // Interior point 2
            if (pointCount > 7) vertices.Add(new Vertex(200, 400, 3, 7)); // Interior point 3
        }
        else
        {
            // For larger counts, use random distribution
            var random = new Random(seed);
            for (var i = 0; i < pointCount; i++)
            {
                var x = random.NextDouble() * width;
                var y = random.NextDouble() * height;

                // Generate terrain with multiple frequency components (as in usage documentation)
                var z = 50 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01) + // Large hills
                        20 * Math.Sin(x * 0.03) * Math.Sin(y * 0.03) + // Medium features
                        5 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1) + // Small features
                        random.NextDouble() * 2; // Noise

                vertices.Add(new Vertex(x, y, z, i));
            }
        }

        var generationTime = stopwatch.Elapsed;
        stopwatch.Restart();

        // Create and populate TIN
        var nominalSpacing = 1; // Math.Sqrt((width * height) / vertices.Count);
        var tin = new IncrementalTin(nominalSpacing);
        tin.PreAllocateForVertices(vertices.Count);
        var success = tin.AddSorted(vertices);

        var triangulationTime = stopwatch.Elapsed;
        stopwatch.Stop();

        if (!success) throw new InvalidOperationException("Failed to bootstrap triangulation");

        return new TriangulationResult
                   {
                       Tin = tin,
                       VertexCount = vertices.Count,
                       ActualRows = 0, // Not applicable for this simplified approach
                       ActualCols = 0, // Not applicable for this simplified approach
                       GenerationTime = generationTime,
                       TriangulationTime = triangulationTime,
                       TriangleCount = tin.CountTriangles(),
                       Bounds = tin.GetBounds()
                   };
    }

    /// <summary>
    ///     Interpolates Z values for a list of vertices using the provided interpolator.
    /// </summary>
    /// <param name="interpolator">The interpolator to use</param>
    /// <param name="vertices">The vertices to interpolate</param>
    /// <returns>A new list of vertices with interpolated Z values</returns>
    private static List<IVertex> InterpolateVertices(IInterpolatorOverTin interpolator, List<IVertex> vertices)
    {
        var result = new List<IVertex>(vertices.Count);
        
        foreach (var v in vertices)
        {
            var z = interpolator.Interpolate(v.X, v.Y, null);
            
            // If interpolation fails (e.g. outside convex hull), default to 0.0
            if (double.IsNaN(z))
            {
                z = 0.0;
            }
            
            // Create a new vertex with the interpolated Z value
            // Preserve the index if possible
            var index = v.GetIndex();
            result.Add(new Vertex(v.X, v.Y, z, index));
        }
        
        return result;
    }

    /// <summary>
    ///     Creates vertices arranged in a circle with explicit indices.
    /// </summary>
    /// <param name="centerX">Center X coordinate</param>
    /// <param name="centerY">Center Y coordinate</param>
    /// <param name="radius">Circle radius</param>
    /// <param name="segments">Number of segments (vertices) around the circle</param>
    /// <param name="startingIndex">Starting index for vertex numbering</param>
    /// <returns>List of vertices forming a circle</returns>
    /// Boundary constraint vertices are created in counter-clockwise order
    /// Holes are created in clockwise order
    /// No checking to see if holes are within another region - if they are not, then the rest of the tin becomes a contrained region
    private static List<IVertex> CreateCircleVertices(
        double centerX,
        double centerY,
        double radius,
        int segments,
        bool hole,
        int startingIndex = 0)
    {
        var vertices = new List<IVertex>();

        if (!hole)

            // For holes, use clockwise order (0 to segments-1)
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0 * Math.PI * i / segments;
                var x = centerX + radius * Math.Cos(angle);
                var y = centerY + radius * Math.Sin(angle);

                // Set Z to NaN for constraint vertices so they can be interpolated if requested
                vertices.Add(new Vertex(x, y, double.NaN, startingIndex + i));
            }
        else

            // For boundary polygons, use counter-clockwise order (0 to segments-1)
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0 * Math.PI * (segments - i - 1) / segments;
                var x = centerX + radius * Math.Cos(angle);
                var y = centerY + radius * Math.Sin(angle);

                // Set Z to NaN for constraint vertices so they can be interpolated if requested
                vertices.Add(new Vertex(x, y, double.NaN, startingIndex + i));
            }

        return vertices;
    }
}

/// <summary>
///     Results from triangulation generation.
/// </summary>
public class TriangulationResult
{
    public int ActualCols { get; set; }

    public int ActualRows { get; set; }

    public (double Left, double Top, double Width, double Height)? Bounds { get; set; }

    public int ConstrainedEdgeCount { get; set; }

    public int ConstraintCount { get; set; }

    public int EdgeCount { get; set; }

    public TimeSpan GenerationTime { get; set; }

    public IncrementalTin Tin { get; set; } = null!;

    public TriangleCount TriangleCount { get; set; } = new();

    public TimeSpan TriangulationTime { get; set; }

    public int VertexCount { get; set; }

    // Add Voronoi data to the triangulation result
    public VoronoiRenderingService.VoronoiResult? VoronoiResult { get; set; }

    public override string ToString()
    {
        var stats =
            $"Vertices: {this.VertexCount:N0}, Triangles: {this.TriangleCount.ValidTriangles:N0}, Edges: {this.EdgeCount}";

        if (this.ConstraintCount > 0)
            stats += $", Constraints: {this.ConstraintCount}";
        if (this.ConstrainedEdgeCount > 0)
            stats += $", Constrained Edges: {this.ConstrainedEdgeCount}";
        stats +=
            $"\nGeneration: {this.GenerationTime.TotalMilliseconds:F1}ms, Triangulation: {this.TriangulationTime.TotalMilliseconds:F1}ms";
        if (this.Bounds.HasValue)
        {
            var (left, top, width, height) = this.Bounds.Value;
            stats += $"\nBounds: ({left:F0},{top:F0}) {width:F0}�{height:F0}";
        }

        // Add debug info for small triangulations
        if (this.VertexCount <= 10)
            stats +=
                $"\nTriangle Details: Valid={this.TriangleCount.ValidTriangles}, Ghost={this.TriangleCount.GhostTriangles}";

        // Add Voronoi info if available
        if (this.VoronoiResult != null)
            stats += $"\nVoronoi: {this.VoronoiResult.PolygonCount} polygons, {this.VoronoiResult.EdgeCount} edges";

        return stats;
    }
}
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
using System.Drawing;
using System.Linq;

using Tinfour.Core.Common;
using Tinfour.Core.Voronoi;

/// <summary>
///     Service for generating and rendering Voronoi diagrams from TIN data
/// </summary>
public static class VoronoiRenderingService
{
    /// <summary>
    ///     Validates that a Voronoi diagram can be generated from the given TIN
    /// </summary>
    /// <param name="tin">The TIN to validate</param>
    /// <returns>True if Voronoi diagram can be generated; otherwise false</returns>
    public static bool CanGenerateVoronoi(IIncrementalTin tin)
    {
        if (tin == null || !tin.IsBootstrapped()) return false;

        var vertices = tin.GetVertices();
        if (vertices.Count < 3) return false;

        // Check if we have valid bounds
        var bounds = tin.GetBounds();
        if (!bounds.HasValue) return false;

        return true;
    }

    /// <summary>
    ///     Creates build options with custom bounds
    /// </summary>
    /// <param name="bounds">The bounds to use</param>
    /// <param name="enableAutoColoring">Whether to enable automatic color assignment</param>
    /// <returns>Build options instance</returns>
    public static BoundedVoronoiBuildOptions CreateBuildOptions(
        RectangleF? bounds = null,
        bool enableAutoColoring = false)
    {
        var options = new BoundedVoronoiBuildOptions();

        if (bounds.HasValue) options.SetBounds(bounds.Value);

        options.SetAutomaticColorAssignment(enableAutoColoring);

        return options;
    }

    /// <summary>
    ///     Generates a Voronoi diagram from the given TIN
    /// </summary>
    /// <param name="tin">The TIN to generate Voronoi diagram from</param>
    /// <param name="options">Optional build options</param>
    /// <returns>Voronoi generation result</returns>
    public static VoronoiResult GenerateVoronoi(IIncrementalTin tin, BoundedVoronoiBuildOptions? options = null)
    {
        if (!tin.IsBootstrapped()) throw new ArgumentException("TIN is not bootstrapped", nameof(tin));

        var stopwatch = Stopwatch.StartNew();

        // Generate the Voronoi diagram
        var diagram = new BoundedVoronoiDiagram(tin);

        stopwatch.Stop();

        // Extract information from the diagram
        var polygons = diagram.GetPolygons();
        var edges = diagram.GetEdges();
        var bounds = diagram.GetBounds();
        var sampleBounds = diagram.GetSampleBounds();

        return new VoronoiResult
                   {
                       Diagram = diagram,
                       Polygons = polygons,
                       Edges = edges,
                       Bounds = bounds,
                       SampleBounds = sampleBounds,
                       PolygonCount = polygons.Count,
                       EdgeCount = edges.Count,
                       GenerationTime = stopwatch.Elapsed
                   };
    }

    /// <summary>
    ///     Generates a Voronoi diagram from a list of vertices
    /// </summary>
    /// <param name="vertices">The vertices to generate Voronoi diagram from</param>
    /// <param name="options">Optional build options</param>
    /// <returns>Voronoi generation result</returns>
    public static VoronoiResult GenerateVoronoi(List<IVertex> vertices, BoundedVoronoiBuildOptions? options = null)
    {
        if (vertices == null || vertices.Count < 3)
            throw new ArgumentException("At least 3 vertices are required", nameof(vertices));

        var stopwatch = Stopwatch.StartNew();

        // Generate the Voronoi diagram
        var diagram = new BoundedVoronoiDiagram(vertices, options);

        stopwatch.Stop();

        // Extract information from the diagram
        var polygons = diagram.GetPolygons();
        var edges = diagram.GetEdges();
        var bounds = diagram.GetBounds();
        var sampleBounds = diagram.GetSampleBounds();

        return new VoronoiResult
                   {
                       Diagram = diagram,
                       Polygons = polygons,
                       Edges = edges,
                       Bounds = bounds,
                       SampleBounds = sampleBounds,
                       PolygonCount = polygons.Count,
                       EdgeCount = edges.Count,
                       GenerationTime = stopwatch.Elapsed
                   };
    }

    /// <summary>
    ///     Gets the Thiessen polygon containing the specified point
    /// </summary>
    /// <param name="diagram">The Voronoi diagram</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>The containing polygon or null if not found</returns>
    public static ThiessenPolygon? GetContainingPolygon(BoundedVoronoiDiagram diagram, double x, double y)
    {
        return diagram.GetContainingPolygon(x, y);
    }

    /// <summary>
    ///     Gets statistics about the Voronoi diagram
    /// </summary>
    /// <param name="result">The Voronoi generation result</param>
    /// <returns>Formatted statistics string</returns>
    public static string GetStatistics(VoronoiResult result)
    {
        var stats = new List<string>
                        {
                            $"Polygons: {result.PolygonCount}",
                            $"Edges: {result.EdgeCount}",
                            $"Generation Time: {result.GenerationTime.TotalMilliseconds:F1}ms"
                        };

        if (result.Polygons.Count > 0)
        {
            var openPolygons = result.Polygons.Count((ThiessenPolygon p) => p.IsOpen());
            var closedPolygons = result.Polygons.Count((ThiessenPolygon p) => !p.IsOpen());
            stats.Add($"Open Polygons: {openPolygons}");
            stats.Add($"Closed Polygons: {closedPolygons}");

            // Calculate area statistics for closed polygons
            var areas = result.Polygons.Where((ThiessenPolygon p) => !p.IsOpen() && !double.IsInfinity(p.GetArea()))
                .Select((ThiessenPolygon p) => p.GetArea()).ToList();

            if (areas.Count > 0)
            {
                var avgArea = areas.Average();
                var minArea = areas.Min();
                var maxArea = areas.Max();
                stats.Add($"Avg Area: {avgArea:F2}");
                stats.Add($"Min Area: {minArea:F2}");
                stats.Add($"Max Area: {maxArea:F2}");
            }
        }

        var bounds = result.Bounds;
        stats.Add($"Bounds: ({bounds.Left:F1}, {bounds.Top:F1}) to ({bounds.Right:F1}, {bounds.Bottom:F1})");

        return string.Join(Environment.NewLine, stats);
    }

    /// <summary>
    ///     Result of Voronoi diagram generation
    /// </summary>
    public class VoronoiResult
    {
        public RectangleF Bounds { get; set; }

        public BoundedVoronoiDiagram Diagram { get; set; } = null!;

        public int EdgeCount { get; set; }

        public List<IQuadEdge> Edges { get; set; } = new();

        public TimeSpan GenerationTime { get; set; }

        public bool HasEdges => this.EdgeCount > 0;

        public bool HasPolygons => this.PolygonCount > 0;

        public int PolygonCount { get; set; }

        public List<ThiessenPolygon> Polygons { get; set; } = new();

        public RectangleF SampleBounds { get; set; }
    }
}
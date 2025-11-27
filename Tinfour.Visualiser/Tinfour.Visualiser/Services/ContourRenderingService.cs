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
using System.Linq;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Interpolation;

/// <summary>
///     Service for generating contours from TIN data for visualization.
/// </summary>
public static class ContourRenderingService
{
    /// <summary>
    ///     Generates evenly spaced contour levels between min and max values.
    /// </summary>
    /// <param name="minZ">Minimum Z value</param>
    /// <param name="maxZ">Maximum Z value</param>
    /// <param name="numberOfLevels">Number of levels to generate</param>
    /// <returns>Array of contour levels</returns>
    public static double[] GenerateContourLevels(double minZ, double maxZ, int numberOfLevels)
    {
        if (numberOfLevels <= 0)
            throw new ArgumentException("Number of levels must be positive", nameof(numberOfLevels));

        if (minZ >= maxZ) throw new ArgumentException("Min Z must be less than max Z");

        if (numberOfLevels == 1) return new[] { (minZ + maxZ) / 2.0 };

        var levels = new double[numberOfLevels];
        var step = (maxZ - minZ) / (numberOfLevels - 1);

        for (var i = 0; i < numberOfLevels; i++) levels[i] = minZ + i * step;

        return levels;
    }

    /// <summary>
    ///     Generates contours from the TIN using automatic level calculation.
    /// </summary>
    /// <param name="tin">The TIN to generate contours from</param>
    /// <param name="numberOfLevels">Number of contour levels to generate</param>
    /// <param name="buildRegions">Whether to build polygon regions from contours</param>
    /// <param name="vertexValuator">Optional custom vertex valuator</param>
    /// <returns>Contour generation result</returns>
    public static ContourResult GenerateContours(
        IIncrementalTin tin,
        int numberOfLevels = 10,
        bool buildRegions = false,
        IVertexValuator? vertexValuator = null)
    {
        if (!tin.IsBootstrapped()) throw new ArgumentException("TIN is not bootstrapped", nameof(tin));

        if (numberOfLevels <= 0)
            throw new ArgumentException("Number of levels must be positive", nameof(numberOfLevels));

        // Calculate Z range from TIN vertices
        var vertices = tin.GetVertices().ToList();
        if (vertices.Count == 0) throw new ArgumentException("TIN contains no vertices", nameof(tin));

        var minZ = double.MaxValue;
        var maxZ = double.MinValue;

        foreach (var vertex in vertices)
        {
            var z = vertexValuator?.Value(vertex) ?? vertex.GetZ();
            if (!double.IsNaN(z) && double.IsFinite(z))
            {
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
        }

        if (minZ >= maxZ) throw new ArgumentException("Invalid Z range in TIN data", nameof(tin));

        // Generate evenly spaced contour levels
        var contourLevels = GenerateContourLevels(minZ, maxZ, numberOfLevels);

        return GenerateContours(tin, contourLevels, buildRegions, vertexValuator);
    }

    /// <summary>
    ///     Generates contours from the TIN using specific contour levels.
    /// </summary>
    /// <param name="tin">The TIN to generate contours from</param>
    /// <param name="contourLevels">Specific Z values for contour generation</param>
    /// <param name="buildRegions">Whether to build polygon regions from contours</param>
    /// <param name="vertexValuator">Optional custom vertex valuator</param>
    /// <returns>Contour generation result</returns>
    public static ContourResult GenerateContours(
        IIncrementalTin tin,
        double[] contourLevels,
        bool buildRegions = false,
        IVertexValuator? vertexValuator = null)
    {
        if (!tin.IsBootstrapped()) throw new ArgumentException("TIN is not bootstrapped", nameof(tin));

        if (contourLevels == null || contourLevels.Length == 0)
            throw new ArgumentException("Contour levels cannot be null or empty", nameof(contourLevels));

        // Validate TIN for contour generation
        ValidateTinForContouring(tin);

        // Sort contour levels to ensure ascending order
        var sortedLevels = contourLevels.OrderBy((double z) => z).ToArray();

        // Validate that levels are unique
        for (var i = 1; i < sortedLevels.Length; i++)
            if (sortedLevels[i] == sortedLevels[i - 1])
                throw new ArgumentException("Contour levels must be unique", nameof(contourLevels));

        // Generate contours using ContourBuilderForTin
        var builder = new ContourBuilderForTin(tin, vertexValuator, sortedLevels, buildRegions);

        var contours = builder.GetContours();
        var regions = buildRegions ? builder.GetRegions() : new List<ContourRegion>();

        return new ContourResult
                   {
                       Contours = contours,
                       Regions = regions,
                       ContourLevels = sortedLevels,
                       MinZ = sortedLevels.First(),
                       MaxZ = sortedLevels.Last(),
                       LineContourCount = contours.Count,
                       RegionCount = regions.Count,
                       HasRegions = buildRegions && regions.Count > 0
                   };
    }

    /// <summary>
    ///     Generates "nice" contour levels using round numbers.
    /// </summary>
    /// <param name="minZ">Minimum Z value</param>
    /// <param name="maxZ">Maximum Z value</param>
    /// <param name="approximateNumberOfLevels">Approximate number of levels desired</param>
    /// <returns>Array of "nice" contour levels</returns>
    public static double[] GenerateNiceContourLevels(double minZ, double maxZ, int approximateNumberOfLevels = 10)
    {
        if (approximateNumberOfLevels <= 0)
            throw new ArgumentException("Number of levels must be positive", nameof(approximateNumberOfLevels));

        if (minZ >= maxZ) throw new ArgumentException("Min Z must be less than max Z");

        var range = maxZ - minZ;
        var roughStep = range / approximateNumberOfLevels;

        // Find a "nice" step size
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
        var normalizedStep = roughStep / magnitude;

        double niceStep;
        if (normalizedStep < 1.5)
            niceStep = 1.0;
        else if (normalizedStep < 3.0)
            niceStep = 2.0;
        else if (normalizedStep < 7.0)
            niceStep = 5.0;
        else
            niceStep = 10.0;

        niceStep *= magnitude;

        // Find nice start value
        var niceMin = Math.Ceiling(minZ / niceStep) * niceStep;

        // Generate levels
        var levels = new List<double>();
        var level = niceMin;
        while (level <= maxZ)
        {
            levels.Add(level);
            level += niceStep;
        }

        // Ensure we have at least one level
        if (levels.Count == 0) levels.Add((minZ + maxZ) / 2.0);

        return levels.ToArray();
    }

    /// <summary>
    ///     Gets boundary contours (contours that lie on the TIN perimeter).
    /// </summary>
    /// <param name="result">Contour generation result</param>
    /// <returns>List of boundary contours</returns>
    public static List<Contour> GetBoundaryContours(ContourResult result)
    {
        return result.Contours.Where((Contour c) => c.IsBoundary()).ToList();
    }

    /// <summary>
    ///     Gets closed contours (contours that form complete loops).
    /// </summary>
    /// <param name="result">Contour generation result</param>
    /// <returns>List of closed contours</returns>
    public static List<Contour> GetClosedContours(ContourResult result)
    {
        return result.Contours.Where((Contour c) => c.IsClosed()).ToList();
    }

    /// <summary>
    ///     Gets interior contours (contours that lie entirely within the TIN).
    /// </summary>
    /// <param name="result">Contour generation result</param>
    /// <returns>List of interior contours</returns>
    public static List<Contour> GetInteriorContours(ContourResult result)
    {
        return result.Contours.Where((Contour c) => !c.IsBoundary()).ToList();
    }

    /// <summary>
    ///     Gets line contours (non-closed contours that cross the TIN interior).
    /// </summary>
    /// <param name="result">Contour generation result</param>
    /// <returns>List of line contours</returns>
    public static List<Contour> GetLineContours(ContourResult result)
    {
        return result.Contours.Where((Contour c) => !c.IsClosed()).ToList();
    }

    /// <summary>
    ///     Validates a TIN for contour generation, checking for common issues that could cause problems.
    /// </summary>
    /// <param name="tin">The TIN to validate</param>
    /// <exception cref="InvalidOperationException">If the TIN has issues that prevent contour generation</exception>
    private static void ValidateTinForContouring(IIncrementalTin tin)
    {
        // Check for vertices with NaN Z values (excluding ghost vertices)
        var invalidVertices = tin.GetVertices().Where((IVertex v) => !v.IsNullVertex() && double.IsNaN(v.GetZ())).ToList();

        if (invalidVertices.Count > 0)
        {
            var indices = string.Join(", ", invalidVertices.Take(10).Select((IVertex v) => v.GetIndex()));
            var message = $"TIN contains {invalidVertices.Count} vertices with NaN Z values. "
                          + $"Sample indices: {indices}";
            if (invalidVertices.Count > 10) message += "...";
            throw new InvalidOperationException(message);
        }

        // Check for sufficient triangles
        var triangleCount = tin.CountTriangles();
        if (triangleCount.ValidTriangles == 0)
            throw new InvalidOperationException("TIN contains no valid triangles for contour generation");

        // Basic bounds check
        var bounds = tin.GetBounds();
        if (!bounds.HasValue) throw new InvalidOperationException("TIN has no valid bounds for contour generation");
    }

    /// <summary>
    ///     Represents the result of contour generation.
    /// </summary>
    public class ContourResult
    {
        public double[] ContourLevels { get; init; } = Array.Empty<double>();

        public List<Contour> Contours { get; init; } = new();

        public TimeSpan GenerationTime { get; init; }

        public bool HasRegions { get; init; }

        public int LineContourCount { get; init; }

        public double MaxZ { get; init; }

        public double MinZ { get; init; }

        public int RegionContourCount { get; init; }

        public int RegionCount { get; init; }

        public List<ContourRegion> Regions { get; init; } = new();

        public override string ToString()
        {
            var result = $"Contours: {this.LineContourCount} lines";
            if (this.HasRegions) result += $", {this.RegionCount} regions";
            result += $"\nLevels: {this.ContourLevels.Length} from {this.MinZ:F2} to {this.MaxZ:F2}";
            return result;
        }
    }
}
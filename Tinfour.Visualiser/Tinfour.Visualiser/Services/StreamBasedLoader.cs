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

using static Tinfour.Visualiser.Services.CoordinateConverter;

namespace Tinfour.Visualiser.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

/// <summary>
///     Provides methods for loading vertices and constraints from streams rather than file paths.
///     This enables compatibility with browser environments.
/// </summary>
public static class StreamBasedLoader
{
    /// <summary>
    ///     Loads constraint points from a stream and creates polygon constraints.
    /// </summary>
    /// <param name="stream">The stream containing constraint data</param>
    /// <param name="transformationType">Type of coordinate transformation to apply</param>
    /// <returns>Result containing the constraint vertices and statistics</returns>
    public static async Task<ConstraintFileLoader.LoadResult> LoadConstraintsFromStreamAsync(
        Stream stream,
        TransformationType transformationType)
    {
        var result = new ConstraintFileLoader.LoadResult { TransformationType = transformationType };

        // Create a memory copy of the stream to ensure we can read it multiple times
        // This is especially important for browser environments where streams might not support seeking
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Detect and use appropriate culture for number parsing
        var culture = CultureInfo.InvariantCulture;

        // Track centroid for UTM zone determination if needed
        double sumLon = 0;
        double sumLat = 0;
        var validCoordCount = 0;

        // First pass: determine if file has constraint index
        var hasConstraintIndex = await FileLoaderUtils.HasConstraintIndexColumnAsync(memoryStream);

        // Reset memory stream position for second pass
        memoryStream.Position = 0;

        Debug.WriteLine($"Constraint file has index column: {hasConstraintIndex}");

        using (var reader = new StreamReader(memoryStream))
        {
            string? line;
            var lineNumber = 0;
            var defaultConstraintIndex = 0;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                // Skip empty lines and comment lines
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                // Split the line into parts
                var parts = line.Split(',', StringSplitOptions.TrimEntries);

                // Check if we have enough parts
                if (hasConstraintIndex && parts.Length < 3)
                    continue;
                if (!hasConstraintIndex && parts.Length < 2)
                    continue;

                // Parse constraint index, latitude, and longitude
                int constraintIndex;
                double latitude, longitude;

                if (hasConstraintIndex)
                {
                    if (!int.TryParse(parts[0], NumberStyles.Any, culture, out constraintIndex)
                        || !double.TryParse(parts[1], NumberStyles.Any, culture, out latitude) || !double.TryParse(
                            parts[2],
                            NumberStyles.Any,
                            culture,
                            out longitude))
                    {
                        Debug.WriteLine($"Failed to parse constraint line with index: {line}");
                        continue;
                    }
                }
                else
                {
                    // If no constraint index column, use default (0)
                    constraintIndex = defaultConstraintIndex;

                    if (!double.TryParse(parts[0], NumberStyles.Any, culture, out latitude) || !double.TryParse(
                            parts[1],
                            NumberStyles.Any,
                            culture,
                            out longitude))
                    {
                        Debug.WriteLine($"Failed to parse constraint line without index: {line}");
                        continue;
                    }
                }

                // Track for UTM zone determination
                sumLat += latitude;
                sumLon += longitude;
                validCoordCount++;

                // Create vertex with coordinate transformation (isConstraint=true for z=0)
                var (vertex, x, y, utmZone) = FileLoaderUtils.CreateVertexFromLatLon(
                    latitude,
                    longitude,
                    0,
                    transformationType,
                    true);

                // Update result UTM zone if applicable
                if (utmZone.HasValue && !result.UtmZone.HasValue) result.UtmZone = utmZone;

                // Add to the appropriate constraint group
                if (!result.ConstraintVertices.TryGetValue(constraintIndex, out var vertices))
                {
                    vertices = new List<IVertex>();
                    result.ConstraintVertices[constraintIndex] = vertices;
                }

                vertices.Add(vertex);

                // Update min/max values
                result.MinX = Math.Min(result.MinX, x);
                result.MaxX = Math.Max(result.MaxX, x);
                result.MinY = Math.Min(result.MinY, y);
                result.MaxY = Math.Max(result.MaxY, y);
            }
        }

        Debug.WriteLine($"Parsed {validCoordCount} constraint points into {result.ConstraintVertices.Count} groups");

        // If UTM was selected but no zone was determined yet (empty file),
        // calculate it from the centroid
        if (transformationType == TransformationType.UTM && !result.UtmZone.HasValue && validCoordCount > 0)
            result.UtmZone = FileLoaderUtils.CalculateUtmZoneFromCentroid(sumLon, validCoordCount);

        // Create polygon constraints for each group
        foreach (var constraintGroup in result.ConstraintVertices)
        {
            var vertices = constraintGroup.Value;

            Debug.WriteLine($"Processing constraint group {constraintGroup.Key} with {vertices.Count} vertices");

            // Ensure we have enough points for a polygon constraint (at least 3)
            if (vertices.Count >= 3)
            {
                // Check if the polygon is closed (first and last points match)
                var first = vertices[0];
                var last = vertices[^1];

                // If not closed, add the first point again to close the polygon
                if (Math.Abs(first.X - last.X) > 1e-9 || Math.Abs(first.Y - last.Y) > 1e-9)
                {
                    vertices.Add(first);
                    Debug.WriteLine($"Closed polygon for constraint {constraintGroup.Key}");
                }

                if (constraintGroup.Key > 0)
                {
                    vertices.Reverse();
                    Debug.WriteLine($"Reversed vertices for constraint {constraintGroup.Key}");
                }

                try
                {
                    // Create a polygon constraint
                    var constraint = new PolygonConstraint(vertices);
                    constraint.Complete();
                    constraint.SetDefaultZ(0.0); // Set default Z value to 0 for interpolation
                    result.Constraints.Add(constraint);
                    Debug.WriteLine(
                        $"Successfully created constraint from group {constraintGroup.Key} with default Z=0");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating constraint from group {constraintGroup.Key}: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine(
                    $"Not enough vertices for constraint group {constraintGroup.Key}: {vertices.Count} < 3");
            }
        }

        return result;
    }

    /// <summary>
    ///     Loads vertices from a stream and creates a TIN.
    /// </summary>
    /// <param name="stream">The stream containing vertex data</param>
    /// <param name="transformationType">Type of coordinate transformation to apply</param>
    /// <returns>Result containing the TIN and statistics</returns>
    public static async Task<VertexFileLoader.LoadResult> LoadVerticesFromStreamAsync(
        Stream stream,
        TransformationType transformationType)
    {
        var vertices = new List<IVertex>();
        var result = new VertexFileLoader.LoadResult
                         {
                             Tin = new IncrementalTin(),
                             TransformationType = transformationType,
                             MinDepth = double.MaxValue,
                             MaxDepth = double.MinValue
                         };

        // Create a memory copy of the stream to ensure we can read it multiple times
        // This is especially important for browser environments where streams might not support seeking
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Detect and use appropriate culture for number parsing
        var culture = CultureInfo.InvariantCulture;

        // Track centroid for UTM zone determination if needed
        double sumLon = 0;
        double sumLat = 0;
        var validCoordCount = 0;

        Debug.WriteLine("Reading vertex data from stream...");

        using (var reader = new StreamReader(memoryStream))
        {
            string? line;
            var lineNumber = 0;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                // Skip empty lines and comment lines
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                // Split the line into parts
                var parts = line.Split(',', StringSplitOptions.TrimEntries);

                // Check if we have at least 3 parts (lat, lon, depth)
                if (parts.Length < 3)
                {
                    Debug.WriteLine($"Skipping line {lineNumber}: Not enough columns");
                    continue;
                }

                // Try to parse the values
                if (!double.TryParse(parts[0], NumberStyles.Any, culture, out var latitude)
                    || !double.TryParse(parts[1], NumberStyles.Any, culture, out var longitude) || !double.TryParse(
                        parts[2],
                        NumberStyles.Any,
                        culture,
                        out var depth))
                {
                    Debug.WriteLine($"Skipping line {lineNumber}: Parse error");
                    continue;
                }

                // Track for UTM zone determination
                sumLat += latitude;
                sumLon += longitude;
                validCoordCount++;

                // Create vertex with coordinate transformation
                var (vertex, x, y, utmZone) = FileLoaderUtils.CreateVertexFromLatLon(
                    latitude,
                    longitude,
                    depth,
                    transformationType);

                vertices.Add(vertex);

                // Update result UTM zone if applicable
                if (utmZone.HasValue && !result.UtmZone.HasValue) result.UtmZone = utmZone;

                // Update min/max values
                result.MinX = Math.Min(result.MinX, x);
                result.MaxX = Math.Max(result.MaxX, x);
                result.MinY = Math.Min(result.MinY, y);
                result.MaxY = Math.Max(result.MaxY, y);
                result.MinDepth = Math.Min(result.MinDepth, depth);
                result.MaxDepth = Math.Max(result.MaxDepth, depth);
            }
        }

        Debug.WriteLine($"Loaded {validCoordCount} valid vertices");

        // If UTM was selected but no zone was determined yet (empty file),
        // calculate it from the centroid
        if (transformationType == TransformationType.UTM && !result.UtmZone.HasValue && validCoordCount > 0)
            result.UtmZone = FileLoaderUtils.CalculateUtmZoneFromCentroid(sumLon, validCoordCount);

        if (vertices.Count == 0)
        {
            Debug.WriteLine("No valid vertices found in file");
            result.Tin = new IncrementalTin();
            return result;
        }

        try
        {
            // Create the TIN
            var tin = new IncrementalTin();
            tin.Add(vertices);

            // Populate the result
            result.Tin = tin;
            result.VertexCount = vertices.Count;

            Debug.WriteLine($"Successfully created TIN with {vertices.Count} vertices");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating TIN: {ex.Message}");
            throw;
        }

        return result;
    }
}
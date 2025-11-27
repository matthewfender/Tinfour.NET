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
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Tinfour.Core.Common;

/// <summary>
///     Base class for file loading results with common properties and methods.
/// </summary>
public abstract class FileLoadResultBase
{
    public double MaxX { get; set; } = double.MinValue;

    public double MaxY { get; set; } = double.MinValue;

    /// <summary>Bounds of data</summary>
    public double MinX { get; set; } = double.MaxValue;

    public double MinY { get; set; } = double.MaxValue;

    /// <summary>Coordinate transformation used</summary>
    public TransformationType TransformationType { get; set; }

    /// <summary>UTM zone if applicable</summary>
    public int? UtmZone { get; set; }

    /// <summary>
    ///     Gets a string describing the projection used.
    /// </summary>
    protected string ProjectionDescription =>
        this.TransformationType switch
            {
                TransformationType.None => "Geographic (WGS84)",
                TransformationType.WebMercator => "Web Mercator",
                TransformationType.UTM => $"UTM Zone {this.UtmZone}",
                _ => "Unknown"
            };
}

/// <summary>
///     Common utility methods for loading geographic data from files and streams.
/// </summary>
public static class FileLoaderUtils
{
    /// <summary>
    ///     Finds the UTM zone for a given longitude centroid.
    /// </summary>
    public static int? CalculateUtmZoneFromCentroid(double sumLon, int validCoordCount)
    {
        if (validCoordCount == 0)
            return null;

        var avgLon = sumLon / validCoordCount;
        return GetUTMZone(avgLon);
    }

    /// <summary>
    ///     Creates a vertex from latitude, longitude, and depth with appropriate coordinate transformation.
    /// </summary>
    public static (Vertex Vertex, double X, double Y, int? UtmZone) CreateVertexFromLatLon(
        double latitude,
        double longitude,
        double depth,
        TransformationType transformationType,
        bool isConstraint = false)
    {
        double x, y;
        int? utmZone = null;

        switch (transformationType)
        {
            case TransformationType.WebMercator:
                var mercator = LatLonToWebMercator(latitude, longitude);
                x = mercator.X;
                y = mercator.Y;
                break;

            case TransformationType.UTM:
                var utm = LatLonToUTM(latitude, longitude);
                x = utm.X;
                y = utm.Y;
                utmZone = utm.Zone;
                break;

            case TransformationType.None:
            default:
                // Use longitude as X and latitude as Y
                x = longitude;
                y = latitude;
                break;
        }

        // For constraints, use z=0
        // For terrain vertices, use z = -depth (negative depth becomes positive elevation)
        var z = isConstraint ? 0 : -depth;

        return (new Vertex(x, y, z), x, y, utmZone);
    }

    /// <summary>
    ///     Checks if the first line of data has a constraint index column.
    /// </summary>
    public static async Task<bool> HasConstraintIndexColumnAsync(Stream stream)
    {
        // Create a memory buffer to avoid position reset issues in browser environments
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Reset original stream position if possible
        try
        {
            stream.Position = 0;
        }
        catch (NotSupportedException)
        {
            // Some browser streams may not support seeking
            // We'll use the memory stream copy instead
        }

        using var reader = new StreamReader(memoryStream);
        string? line;

        // Skip comments and empty lines
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                continue;

            // Found a non-empty, non-comment line
            break;
        }

        if (line == null)
            return false;

        // Split the line into parts
        var parts = line.Split(',', StringSplitOptions.TrimEntries);

        // Need at least 3 parts for constraint_index,latitude,longitude
        if (parts.Length < 3)
            return false;

        // Try to parse the first column as an integer (constraint index)
        // and the second and third columns as doubles (latitude/longitude)
        var culture = CultureInfo.InvariantCulture;
        return int.TryParse(parts[0], NumberStyles.Any, culture, out _)
               && double.TryParse(parts[1], NumberStyles.Any, culture, out _)
               && double.TryParse(parts[2], NumberStyles.Any, culture, out _);
    }
}
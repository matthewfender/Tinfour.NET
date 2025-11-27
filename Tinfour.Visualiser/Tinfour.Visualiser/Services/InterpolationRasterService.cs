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

using InterpolationType = Tinfour.Core.Interpolation.InterpolationType;

namespace Tinfour.Visualiser.Services;

using System;
using System.Linq;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;

/// <summary>
///     Service for creating interpolated raster visualizations from TIN data.
/// </summary>
public static class InterpolationRasterService
{
    /// <summary>
    ///     Creates a default interpolator for the given TIN.
    /// </summary>
    public static IInterpolatorOverTin CreateDefaultInterpolator(IIncrementalTin tin)
    {
        return new TriangularFacetInterpolator(tin);
    }

    /// <summary>
    ///     Creates an interpolated raster from the TIN using the specified interpolator.
    /// </summary>
    /// <param name="tin">The TIN to interpolate over</param>
    /// <param name="bounds">The world coordinate bounds to rasterize</param>
    /// <param name="pixelWidth">Width of the resulting raster in pixels</param>
    /// <param name="pixelHeight">Height of the resulting raster in pixels</param>
    /// <param name="interpolator">The interpolation method to use</param>
    /// <param name="constrainedOnly">If true, only interpolate within constrained regions</param>
    /// <returns>A raster result containing the bitmap and metadata</returns>
    public static RasterResult CreateInterpolatedRaster(
        IIncrementalTin tin,
        Rect bounds,
        int pixelWidth,
        int pixelHeight,
        InterpolationType interpolationType,
        bool constrainedOnly = false)
    {
        if (!tin.IsBootstrapped()) throw new ArgumentException("TIN is not bootstrapped", nameof(tin));

        // Use TinRasterizer to generate the raster grid
        var rasterizer = new TinRasterizer(tin, interpolationType, constrainedOnly);
        var rasterResult = rasterizer.CreateRaster(
            pixelWidth,
            pixelHeight,
            (bounds.X, bounds.Y, bounds.Width, bounds.Height));

        var bitmap = new WriteableBitmap(
            new PixelSize(rasterResult.Width, rasterResult.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        var minZ = double.MaxValue;
        var maxZ = double.MinValue;
        var validPixels = 0;
        var totalPixels = rasterResult.Width * rasterResult.Height;
        var values = rasterResult.Data;

        // Compute min/max and valid pixel count
        for (var y = 0; y < rasterResult.Height; y++)
        for (var x = 0; x < rasterResult.Width; x++)
        {
            var z = values[x, y];
            if (!double.IsNaN(z) && double.IsFinite(z))
            {
                validPixels++;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
        }

        // If no valid values found, create a blank bitmap
        if (validPixels == 0)
        {
            using var lockedBitmap = bitmap.Lock();
            var buffer = lockedBitmap.Address;
            unsafe
            {
                var ptr = (uint*)buffer;
                for (var i = 0; i < totalPixels; i++) ptr[i] = 0x00000000; // Transparent
            }

            return new RasterResult
                       {
                           Bitmap = bitmap,
                           MinZ = 0,
                           MaxZ = 0,
                           ValidPixels = 0,
                           TotalPixels = totalPixels,
                           Bounds = bounds,
                           InterpolatorName = interpolationType.ToString() // todo: friendly name
                       };
        }

        // Second pass: apply color mapping
        using (var lockedBitmap = bitmap.Lock())
        {
            var buffer = lockedBitmap.Address;
            var range = maxZ - minZ;
            if (range == 0) range = 1; // Avoid division by zero
            unsafe
            {
                var ptr = (uint*)buffer;
                for (var y = 0; y < rasterResult.Height; y++)
                for (var x = 0; x < rasterResult.Width; x++)
                {
                    var index = y * rasterResult.Width + x;
                    var z = values[x, y];
                    if (!double.IsNaN(z) && double.IsFinite(z))
                    {
                        var normalized = (z - minZ) / range;
                        var color = GetColorForValue(normalized);
                        ptr[index] = (uint)((255 << 24) | (color.R << 16) | (color.G << 8) | color.B);
                    }
                    else
                    {
                        ptr[index] = 0x00000000; // Transparent for invalid/outside regions
                    }
                }
            }
        }

        return new RasterResult
                   {
                       Bitmap = bitmap,
                       MinZ = minZ,
                       MaxZ = maxZ,
                       ValidPixels = validPixels,
                       TotalPixels = totalPixels,
                       Bounds = bounds,
                       InterpolatorName = interpolationType.ToString()
                   };
    }

    /// <summary>
    ///     Creates an interpolator for the given TIN based on the specified interpolation type.
    /// </summary>
    /// <param name="tin">The TIN to create the interpolator for</param>
    /// <param name="interpolationType">The type of interpolation to use</param>
    /// <returns>An appropriate interpolator instance</returns>
    public static IInterpolatorOverTin CreateInterpolator(IIncrementalTin tin, InterpolationType interpolationType)
    {
        return interpolationType switch
            {
                InterpolationType.NaturalNeighbor => new NaturalNeighborInterpolator(tin),
                InterpolationType.InverseDistanceWeighting => new InverseDistanceWeightingInterpolator(tin, 3, false),
                InterpolationType.TriangularFacet => new TriangularFacetInterpolator(tin),
                _ => new TriangularFacetInterpolator(tin) // Default to triangular facet
            };
    }

    /// <summary>
    ///     Determines optimal raster dimensions based on TIN bounds and display size.
    /// </summary>
    public static (int width, int height) GetOptimalRasterSize(Rect bounds, Size displaySize, double maxPixels = 500000)
    {
        var aspectRatio = bounds.Width / bounds.Height;
        var displayAspectRatio = displaySize.Width / displaySize.Height;

        int width, height;

        if (aspectRatio > displayAspectRatio)
        {
            // Bounds are wider than display
            width = (int)Math.Sqrt(maxPixels * aspectRatio);
            height = (int)(width / aspectRatio);
        }
        else
        {
            // Bounds are taller than display
            height = (int)Math.Sqrt(maxPixels / aspectRatio);
            width = (int)(height * aspectRatio);
        }

        // Ensure minimum size and reasonable maximums
        width = Math.Max(100, Math.Min(2000, width));
        height = Math.Max(100, Math.Min(2000, height));

        return (width, height);
    }

    /// <summary>
    ///     Gets a color for a normalized value (0-1) using a terrain-like color palette.
    /// </summary>
    private static Color GetColorForValue(double normalizedValue)
    {
        // Clamp value to [0, 1]
        normalizedValue = Math.Max(0, Math.Min(1, normalizedValue));

        // Define color stops for a terrain-like palette
        var colorStops = new[]
                             {
                                 (0.0, Color.FromRgb(0, 0, 139)), // Dark blue (deep water)
                                 (0.1, Color.FromRgb(0, 119, 190)), // Blue (water)
                                 (0.2, Color.FromRgb(0, 180, 216)), // Light blue (shallow water)
                                 (0.3, Color.FromRgb(144, 238, 144)), // Light green (lowland)
                                 (0.4, Color.FromRgb(34, 139, 34)), // Green (plains)
                                 (0.5, Color.FromRgb(107, 142, 35)), // Olive green (hills)
                                 (0.6, Color.FromRgb(160, 82, 45)), // Saddle brown (low mountains)
                                 (0.7, Color.FromRgb(139, 69, 19)), // Brown (mountains)
                                 (0.8, Color.FromRgb(128, 128, 128)), // Gray (high mountains)
                                 (0.9, Color.FromRgb(255, 255, 255)) // White (peaks/snow)
                             };

        // Find the appropriate color segment
        for (var i = 0; i < colorStops.Length - 1; i++)
        {
            var (value1, color1) = colorStops[i];
            var (value2, color2) = colorStops[i + 1];

            if (normalizedValue >= value1 && normalizedValue <= value2)
            {
                // Interpolate between the two colors
                var t = (normalizedValue - value1) / (value2 - value1);
                return InterpolateColor(color1, color2, t);
            }
        }

        // Fallback to the last color
        return colorStops.Last().Item2;
    }

    /// <summary>
    ///     Linearly interpolates between two colors.
    /// </summary>
    private static Color InterpolateColor(Color color1, Color color2, double t)
    {
        var r = (byte)(color1.R + t * (color2.R - color1.R));
        var g = (byte)(color1.G + t * (color2.G - color1.G));
        var b = (byte)(color1.B + t * (color2.B - color1.B));
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    ///     Represents the result of raster interpolation.
    /// </summary>
    public class RasterResult
    {
        public WriteableBitmap Bitmap { get; init; } = null!;

        public Rect Bounds { get; init; }

        public string InterpolatorName { get; init; } = string.Empty;

        public double MaxZ { get; init; }

        public double MinZ { get; init; }

        public int TotalPixels { get; init; }

        public int ValidPixels { get; init; }

        public override string ToString()
        {
            return $"Raster: {this.Bitmap.PixelSize.Width}×{this.Bitmap.PixelSize.Height}, "
                   + $"Z: {this.MinZ:F2} to {this.MaxZ:F2}, "
                   + $"Valid: {this.ValidPixels}/{this.TotalPixels} ({(double)this.ValidPixels / this.TotalPixels:P1}), "
                   + $"Method: {this.InterpolatorName}";
        }
    }
}
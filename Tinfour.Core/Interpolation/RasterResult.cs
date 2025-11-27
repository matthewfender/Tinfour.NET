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

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Utils;

/// <summary>
///     Represents the result of a TIN rasterization operation.
/// </summary>
/// <remarks>
///     This class contains both the raster data array and metadata about the raster,
///     including bounds, dimensions, cell size, and coverage statistics.
/// </remarks>
public class RasterResult
{
    /// <summary>
    ///     Creates a new RasterResult with double[,] data (legacy constructor for backward compatibility).
    /// </summary>
    /// <param name="data">The 2D array of interpolated values.</param>
    /// <param name="bounds">The bounds of the rasterized area in world coordinates.</param>
    /// <param name="width">The width of the raster in pixels.</param>
    /// <param name="height">The height of the raster in pixels.</param>
    /// <param name="cellWidth">The width of each cell in world units.</param>
    /// <param name="cellHeight">The height of each cell in world units.</param>
    /// <param name="noDataCount">The number of cells with NaN values.</param>
    public RasterResult(
        double[,] data,
        (double Left, double Top, double Width, double Height) bounds,
        int width,
        int height,
        double cellWidth,
        double cellHeight,
        int noDataCount)
    {
        Data = data;
        RasterData = new Float64RasterData(data);
        Bounds = bounds;
        Width = width;
        Height = height;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        NoDataCount = noDataCount;
    }

    /// <summary>
    ///     Creates a new RasterResult with flexible IRasterData storage.
    /// </summary>
    /// <param name="rasterData">The raster data storage.</param>
    /// <param name="bounds">The bounds of the rasterized area in world coordinates.</param>
    /// <param name="cellWidth">The width of each cell in world units.</param>
    /// <param name="cellHeight">The height of each cell in world units.</param>
    /// <param name="noDataCount">The number of cells with NaN values.</param>
    public RasterResult(
        IRasterData rasterData,
        (double Left, double Top, double Width, double Height) bounds,
        double cellWidth,
        double cellHeight,
        int noDataCount)
    {
        RasterData = rasterData ?? throw new ArgumentNullException(nameof(rasterData));

        // For backward compatibility, expose double[,] if the backing type is Float64
        Data = rasterData is Float64RasterData f64 ? f64.GetDoubleArray() : null;

        Bounds = bounds;
        Width = rasterData.Width;
        Height = rasterData.Height;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        NoDataCount = noDataCount;
    }

    /// <summary>
    ///     The bounds of the rasterized area in world coordinates.
    /// </summary>
    public (double Left, double Top, double Width, double Height) Bounds { get; }

    /// <summary>
    ///     The height of each cell in world units.
    /// </summary>
    public double CellHeight { get; }

    /// <summary>
    ///     The width of each cell in world units.
    /// </summary>
    public double CellWidth { get; }

    /// <summary>
    ///     The percentage of cells with valid data (non-NaN).
    /// </summary>
    public double CoveragePercent => 100.0 * (1.0 - (double)NoDataCount / (Width * Height));

    /// <summary>
    ///     The raster data as a 2D array [x,y] where x is the column and y is the row.
    ///     May be null if the raster was created with a non-Float64 data type.
    /// </summary>
    /// <remarks>
    ///     For non-Float64 rasters, use <see cref="RasterData"/> instead.
    /// </remarks>
    public double[,]? Data { get; }

    /// <summary>
    ///     The raster data with flexible storage type.
    /// </summary>
    /// <remarks>
    ///     This property provides access to the underlying data regardless of storage type.
    ///     Use <see cref="IRasterData.GetValue"/> and <see cref="IRasterData.SetValue"/>
    ///     for type-independent access.
    /// </remarks>
    public IRasterData RasterData { get; }

    /// <summary>
    ///     The height of the raster in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     The number of cells with NaN values.
    /// </summary>
    public int NoDataCount { get; }

    /// <summary>
    ///     The width of the raster in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets statistics about the raster data.
    /// </summary>
    /// <returns>A tuple containing min, max, mean, and standard deviation values.</returns>
    public (double Min, double Max, double Mean, double StdDev) GetStatistics()
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        var sum = new KahanSummation();
        var count = 0;

        // First pass: min, max, mean
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var value = RasterData.GetValue(x, y);
            if (!double.IsNaN(value))
            {
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                sum.Add((double)value);
                count++;
            }
        }

        // Handle empty raster case
        if (count == 0) return (double.NaN, double.NaN, double.NaN, double.NaN);

        var mean = sum.GetSum() / count;

        // Second pass: standard deviation
        var sumSquaredDiff = new KahanSummation();
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var value = RasterData.GetValue(x, y);
            if (!double.IsNaN(value))
            {
                var diff = value - mean;
                sumSquaredDiff.Add(diff * diff);
            }
        }

        var variance = sumSquaredDiff.GetSum() / count;
        var stdDev = Math.Sqrt(variance);

        return (min, max, mean, stdDev);
    }

    /// <summary>
    ///     Converts raster coordinates to world coordinates (at cell center).
    /// </summary>
    /// <param name="column">Raster column (x).</param>
    /// <param name="row">Raster row (y).</param>
    /// <returns>World coordinates at the center of the specified cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the coordinates are outside the raster bounds.</exception>
    public (double X, double Y) RasterToWorld(int column, int row)
    {
        if (column < 0 || column >= Width || row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException($"Coordinates ({column}, {row}) are outside raster bounds");

        var x = Bounds.Left + (column + 0.5) * CellWidth;
        var y = Bounds.Top + (row + 0.5) * CellHeight;

        return (x, y);
    }

    /// <summary>
    ///     Converts world coordinates to raster coordinates.
    /// </summary>
    /// <param name="x">World x-coordinate.</param>
    /// <param name="y">World y-coordinate.</param>
    /// <returns>Raster coordinates (column, row), or null if outside the raster bounds.</returns>
    public (int Column, int Row)? WorldToRaster(double x, double y)
    {
        if (x < Bounds.Left || x >= Bounds.Left + Bounds.Width || y < Bounds.Top
            || y >= Bounds.Top + Bounds.Height) return null;

        var col = (int)((x - Bounds.Left) / CellWidth);
        var row = (int)((y - Bounds.Top) / CellHeight);

        // Clamp to valid indices
        col = Math.Min(Math.Max(col, 0), Width - 1);
        row = Math.Min(Math.Max(row, 0), Height - 1);

        return (col, row);
    }
}

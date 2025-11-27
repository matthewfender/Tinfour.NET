/*
 * Copyright 2025 Gary W. Lucas / ReefMaster Software Ltd.
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

/// <summary>
///     Raster data storage using 64-bit floating point values (double precision).
/// </summary>
/// <remarks>
///     <para>
///         This implementation uses 8 bytes per cell, providing full double precision.
///         Use this when maximum precision is required, but be aware of memory usage
///         for large rasters.
///     </para>
///     <para>
///         NaN values are stored natively as double.NaN.
///     </para>
/// </remarks>
public class Float64RasterData : IRasterData
{
    private readonly double[,] _data;

    /// <summary>
    ///     Creates a new Float64RasterData with the specified dimensions.
    /// </summary>
    /// <param name="width">The width in cells.</param>
    /// <param name="height">The height in cells.</param>
    public Float64RasterData(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");

        Width = width;
        Height = height;
        _data = new double[width, height];

        // Initialize with NaN (no-data)
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            _data[x, y] = double.NaN;
    }

    /// <summary>
    ///     Creates a Float64RasterData wrapping an existing double[,] array.
    /// </summary>
    /// <param name="data">The existing data array.</param>
    public Float64RasterData(double[,] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Width = data.GetLength(0);
        Height = data.GetLength(1);
    }

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public RasterDataType DataType => RasterDataType.Float64;

    /// <inheritdoc />
    public long MemorySize => (long)Width * Height * sizeof(double);

    /// <inheritdoc />
    public double GetValue(int x, int y)
    {
        return _data[x, y];
    }

    /// <inheritdoc />
    public void SetValue(int x, int y, double value)
    {
        _data[x, y] = value;
    }

    /// <inheritdoc />
    public Array GetBackingArray()
    {
        return _data;
    }

    /// <summary>
    ///     Gets direct access to the backing double array.
    /// </summary>
    /// <returns>The double[,] backing array.</returns>
    public double[,] GetDoubleArray()
    {
        return _data;
    }
}

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
///     Defines an interface for raster data storage with varying precision and memory usage.
/// </summary>
/// <remarks>
///     <para>
///         This interface allows different backing storage types (Float64, Float32, Int16)
///         while providing a uniform API for reading and writing cell values.
///     </para>
///     <para>
///         All implementations store NaN (no-data) values appropriately for their data type.
///         For integer types, a reserved sentinel value represents NaN.
///     </para>
/// </remarks>
public interface IRasterData
{
    /// <summary>
    ///     Gets the width of the raster in cells.
    /// </summary>
    int Width { get; }

    /// <summary>
    ///     Gets the height of the raster in cells.
    /// </summary>
    int Height { get; }

    /// <summary>
    ///     Gets the data type used for storage.
    /// </summary>
    RasterDataType DataType { get; }

    /// <summary>
    ///     Gets the approximate memory size in bytes used by this raster data.
    /// </summary>
    long MemorySize { get; }

    /// <summary>
    ///     Gets the value at the specified cell coordinates.
    /// </summary>
    /// <param name="x">The column index (0 to Width-1).</param>
    /// <param name="y">The row index (0 to Height-1).</param>
    /// <returns>The cell value, or NaN if the cell contains no data.</returns>
    double GetValue(int x, int y);

    /// <summary>
    ///     Sets the value at the specified cell coordinates.
    /// </summary>
    /// <param name="x">The column index (0 to Width-1).</param>
    /// <param name="y">The row index (0 to Height-1).</param>
    /// <param name="value">The value to set. NaN indicates no data.</param>
    void SetValue(int x, int y, double value);

    /// <summary>
    ///     Gets the raw backing array. The type depends on the implementation.
    /// </summary>
    /// <returns>The backing array (float[,], double[,], or short[,]).</returns>
    Array GetBackingArray();
}

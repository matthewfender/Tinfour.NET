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
///     Specifies the data type used to store raster cell values.
/// </summary>
/// <remarks>
///     Different data types provide trade-offs between precision and memory usage.
///     For large rasters (e.g., 40km × 40km at 1m resolution = 1.6 billion cells),
///     choosing an appropriate data type can significantly reduce memory consumption.
/// </remarks>
public enum RasterDataType
{
    /// <summary>
    ///     64-bit floating point (double precision).
    ///     8 bytes per cell, full precision.
    ///     Memory: 12.8 GB for 1.6B cells.
    /// </summary>
    Float64,

    /// <summary>
    ///     32-bit floating point (single precision).
    ///     4 bytes per cell, approximately 7 significant digits.
    ///     Memory: 6.4 GB for 1.6B cells.
    ///     Sufficient for most bathymetric and terrain applications.
    /// </summary>
    Float32,

    /// <summary>
    ///     16-bit signed integer with scale and offset.
    ///     2 bytes per cell, requires scale factor for conversion.
    ///     Memory: 3.2 GB for 1.6B cells.
    ///     Best for bounded value ranges (e.g., depth -327m to +327m at 1cm resolution).
    /// </summary>
    Int16Scaled
}

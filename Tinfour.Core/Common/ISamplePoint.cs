/*
 * Copyright 2014 Gary W. Lucas.
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

namespace Tinfour.Core.Common;

/// <summary>
///     Defines a sample point interface to be used for spatial data analysis.
/// </summary>
public interface ISamplePoint
{
    /// <summary>
    ///     Get the square of the distance to the specified coordinates
    /// </summary>
    /// <param name="x">X coordinate for distance calculation</param>
    /// <param name="y">Y coordinate for distance calculation</param>
    /// <returns>A positive floating-point value</returns>
    double GetDistanceSq(double x, double y);

    /// <summary>
    ///     Get the X coordinate of the sample point
    /// </summary>
    /// <returns>A valid floating-point value</returns>
    double GetX();

    /// <summary>
    ///     Get the Y coordinate of the sample point
    /// </summary>
    /// <returns>A valid floating-point value</returns>
    double GetY();

    /// <summary>
    ///     Get the Z coordinate of the sample point
    /// </summary>
    /// <returns>A valid floating point value</returns>
    double GetZ();
}
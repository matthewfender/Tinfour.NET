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

/*
 * -----------------------------------------------------------------------
 *
 * Revision History:
 * Date     Name         Description
 * ------   ---------    -------------------------------------------------
 * 08/2014  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;

/// <summary>
///     Defines methods for navigating a TIN structure for interpolation operations.
/// </summary>
public interface IIncrementalTinNavigator : IProcessUsingTin
{
    /// <summary>
    ///     Finds an edge from the triangle that contains the specified coordinates.
    ///     If the coordinates are outside the TIN, returns an edge on the perimeter.
    /// </summary>
    /// <param name="x">The x coordinate for point location</param>
    /// <param name="y">The y coordinate for point location</param>
    /// <returns>An edge from the containing triangle or perimeter edge</returns>
    IQuadEdge GetNeighborEdge(double x, double y);

    /// <summary>
    ///     Finds the triangle that contains the specified coordinates.
    /// </summary>
    /// <param name="x">The x coordinate for point location</param>
    /// <param name="y">The y coordinate for point location</param>
    /// <returns>The containing triangle, or null if outside the TIN</returns>
    SimpleTriangle? GetContainingTriangle(double x, double y);

    /// <summary>
    ///     Finds the vertex nearest to the specified coordinates.
    /// </summary>
    /// <param name="x">The x coordinate</param>
    /// <param name="y">The y coordinate</param>
    /// <returns>The nearest vertex, or null if the TIN is empty</returns>
    IVertex? GetNearestVertex(double x, double y);
}
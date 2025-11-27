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
///     Defines methods for extracting a scalar value from a vertex for
///     interpolation operations.
/// </summary>
/// <remarks>
///     <para>
///         While Tinfour vertices have a Z coordinate field that is accessed
///         through the GetZ() method, there are applications in which the
///         interpolation is to be based on some other value associated with
///         the vertex. For example, an application might interpolate temperature
///         based on vertices that store x, y, and elevation, with temperature
///         stored in an auxiliary data field. To access the auxiliary field,
///         an application would implement its own version of this interface.
///     </para>
/// </remarks>
public interface IVertexValuator
{
    /// <summary>
    ///     Extract a value from the specified vertex to be used in an
    ///     interpolation.
    /// </summary>
    /// <param name="v">A valid vertex</param>
    /// <returns>A floating point value, potentially Double.NaN</returns>
    double Value(IVertex v);
}
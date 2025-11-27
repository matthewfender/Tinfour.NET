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
///     A default vertex valuator that simply returns the Z coordinate
///     of the vertex using the GetZ() method.
/// </summary>
public class VertexValuatorDefault : IVertexValuator
{
    /// <summary>
    ///     Extract the Z coordinate from the specified vertex.
    /// </summary>
    /// <param name="v">A valid vertex</param>
    /// <returns>The Z coordinate of the vertex</returns>
    public double Value(IVertex v)
    {
        return v.GetZ();
    }
}
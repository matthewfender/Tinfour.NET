/*
 * Copyright 2015-2025 Gary W. Lucas.
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
 * 02/2013  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

/// <summary>
///     Defines an interface for classes that implement a polyline feature
///     (a polygon or chain of connected line segments).
/// </summary>
public interface IPolyline : IEnumerable<IVertex>
{
    /// <summary>
    ///     Adds a vertex to the polyline.
    /// </summary>
    /// <param name="v">The vertex to add.</param>
    void Add(IVertex v);

    /// <summary>
    ///     Marks the polyline as complete, computing derived values like length and bounds.
    /// </summary>
    void Complete();

    /// <summary>
    ///     Inserts new vertices into the polyline segments to meet a specified spacing.
    /// </summary>
    /// <param name="threshold">The maximum distance between vertices.</param>
    void Densify(double threshold);

    /// <summary>
    ///     Gets the bounds of the polyline.
    /// </summary>
    /// <returns>A tuple representing the bounding box.</returns>
    (double Left, double Top, double Width, double Height) GetBounds();

    /// <summary>
    ///     Gets the total length of the polyline.
    /// </summary>
    /// <returns>A positive floating-point value.</returns>
    double GetLength();

    /// <summary>
    ///     Gets the nominal spacing between points.
    /// </summary>
    /// <returns>A positive floating-point value.</returns>
    double GetNominalPointSpacing();

    /// <summary>
    ///     Gets the number of vertices in the polyline.
    /// </summary>
    /// <returns>A non-negative integer.</returns>
    int GetVertexCount();

    /// <summary>
    ///     Gets the vertices for this feature.
    /// </summary>
    /// <returns>A valid list of two or more unique vertices.</returns>
    IList<IVertex> GetVertices();

    /// <summary>
    ///     Indicates if the polyline represents a closed polygon.
    /// </summary>
    /// <returns>True if it is a polygon; otherwise, false.</returns>
    bool IsPolygon();

    /// <summary>
    ///     Indicates if the polyline is valid (has at least 2 vertices).
    /// </summary>
    /// <returns>True if valid; otherwise, false.</returns>
    bool IsValid();

    /// <summary>
    ///     Creates a new polyline feature with the specified geometry
    ///     and transfers any data elements from the current object to the new one.
    /// </summary>
    /// <param name="geometry">A list or other iterable of vertices.</param>
    /// <returns>A new instance of the implementing class.</returns>
    IPolyline Refactor(IEnumerable<IVertex> geometry);
}
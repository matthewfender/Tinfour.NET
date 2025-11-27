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

namespace Tinfour.Core.Common;

/// <summary>
///     Defines the interface for vertex-like objects in the triangulation.
///     This allows for both value-type Vertex structs and reference-type VertexMergerGroup classes
///     to be used polymorphically while maintaining memory efficiency.
/// </summary>
public interface IVertex : ISamplePoint
{
    /// <summary>
    ///     Returns a null vertex reference.
    /// </summary>
    /// <returns>A null vertex</returns>
    IVertex NullVertex { get; }

    /// <summary>
    ///     Gets the x coordinate of the vertex.
    /// </summary>
    double X { get; }

    /// <summary>
    ///     Gets the y coordinate of the vertex.
    /// </summary>
    double Y { get; }

    /// <summary>
    ///     Gets this vertex as a Vertex struct for computational operations.
    ///     For merger groups, this returns the representative vertex.
    /// </summary>
    /// <returns>A Vertex struct representing this vertex</returns>
    Vertex AsVertex();

    /// <summary>
    ///     Tests if this vertex contains the specified vertex.
    ///     For regular vertices, this tests for equality.
    ///     For merger groups, this tests for membership.
    /// </summary>
    /// <param name="vertex">The vertex to test</param>
    /// <returns>True if this vertex contains the specified vertex</returns>
    bool Contains(Vertex vertex);

    /// <summary>
    ///     Gets the distance to the specified coordinates.
    /// </summary>
    /// <param name="x">X coordinate for distance calculation</param>
    /// <param name="y">Y coordinate for distance calculation</param>
    /// <returns>A positive floating-point value</returns>
    double GetDistance(double x, double y);

    /// <summary>
    ///     Gets the distance to another vertex.
    /// </summary>
    /// <param name="v">A valid vertex</param>
    /// <returns>The distance to the vertex</returns>
    double GetDistance(IVertex v);

    /// <summary>
    ///     Gets the index of the vertex.
    /// </summary>
    int GetIndex();

    /// <summary>
    ///     Gets a string intended for labeling the vertex.
    /// </summary>
    /// <returns>A valid, non-empty string</returns>
    string GetLabel();

    /// <summary>
    ///     Checks if this represents a null vertex (ghost vertex).
    /// </summary>
    /// <returns>True if this is a null vertex; otherwise, false</returns>
    bool IsNullVertex();

    /// <summary>
    ///     Indicates whether this vertex is synthetic.
    /// </summary>
    /// <returns>True if synthetic; otherwise, false</returns>
    bool IsSynthetic();
}
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
 * 03/2014  G. Lucas     Created (Java)
 * 08/2025  M. Fender    Ported to C# for Tinfour.Net
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     A synthetic vertex used to handle cases when multiple vertices
///     occupy coincident locations.
/// </summary>
public class VertexMergerGroup : IVertex
{
    private readonly int _index;

    private readonly List<Vertex> _list = new();

    private bool _isConstraintMember;

    private bool _isSynthetic;

    private ResolutionRule _rule = ResolutionRule.MeanValue;

    private double _zRule;

    /// <summary>
    ///     Constructs a coincident vertex using the specified vertex
    ///     for initialization.
    /// </summary>
    /// <param name="firstVertex">A valid instance</param>
    public VertexMergerGroup(Vertex firstVertex)
    {
        X = firstVertex.X;
        Y = firstVertex.Y;
        _zRule = firstVertex.GetZ();
        _index = firstVertex.GetIndex();
        _isSynthetic = firstVertex.IsSynthetic();
        _isConstraintMember = firstVertex.IsConstraintMember();
        _list.Add(firstVertex);
    }

    /// <summary>
    ///     Specifies a rule for determining a z value based on the collection
    ///     of coincident vertices. The selection of rules may be made to
    ///     reflect lidar return type (i.e. MaxValue for first-return processing),
    ///     or classification (min or average value used for ground-classified
    ///     vertices).
    /// </summary>
    public enum ResolutionRule
    {
        /// <summary>
        ///     use the minimum z value
        /// </summary>
        MinValue,

        /// <summary>
        ///     use the mean z value
        /// </summary>
        MeanValue,

        /// <summary>
        ///     use the maximum z value
        /// </summary>
        MaxValue
    }

    /// <summary>
    ///     Returns a null vertex reference.
    /// </summary>
    /// <returns>A null vertex</returns>
    public IVertex NullVertex => Vertex.Null;

    /// <summary>
    ///     Gets the x coordinate of the vertex.
    /// </summary>
    public double X { get; }

    /// <summary>
    ///     Gets the y coordinate of the vertex.
    /// </summary>
    public double Y { get; }

    /// <summary>
    ///     Add a new vertex to the coincident collection. Recompute z value using
    ///     current rule.
    /// </summary>
    /// <param name="vertex">A valid, unique instance</param>
    /// <returns>True if added to collection; otherwise false</returns>
    public bool AddVertex(IVertex vertex)
    {
        // Handle constraint member and synthetic flags
        if (vertex is Vertex v && v.IsConstraintMember()) _isConstraintMember = true;
        if (vertex is Vertex vSynth && !vSynth.IsSynthetic()) _isSynthetic = false;

        if (vertex is VertexMergerGroup group)
        {
            // Put the content of the added group into
            // the existing group. It's the only way to
            // ensure that the resolution rules behave properly.
            var added = false;
            foreach (var a in group._list)
                if (!_list.Contains(a))
                {
                    _list.Add(a);
                    added = true;
                }

            if (added) ApplyRule();
            return added;
        }

        if (vertex is Vertex vertexStruct)
        {
            if (_list.Contains(vertexStruct)) return false;

            _list.Add(vertexStruct);
            ApplyRule();
            return true;
        }

        // For other IVertex implementations, convert to Vertex
        var asVertex = vertex.AsVertex();
        if (_list.Contains(asVertex)) return false;

        _list.Add(asVertex);
        ApplyRule();
        return true;
    }

    /// <summary>
    ///     Gets this vertex as a Vertex struct for computational operations.
    ///     For merger groups, this returns the representative vertex.
    /// </summary>
    /// <returns>A Vertex struct representing this vertex</returns>
    public Vertex AsVertex()
    {
        // Return the first vertex in the list as the representative
        if (_list.Count > 0) return _list[0];
        return Vertex.Null.AsVertex();
    }

    /// <summary>
    ///     Tests if this vertex contains the specified vertex.
    ///     For merger groups, this tests for membership.
    /// </summary>
    /// <param name="vertex">The vertex to test</param>
    /// <returns>True if this vertex contains the specified vertex</returns>
    public bool Contains(Vertex vertex)
    {
        return _list.Contains(vertex);
    }

    /// <summary>
    ///     Gets the distance to the specified coordinates.
    /// </summary>
    /// <param name="x">X coordinate for distance calculation</param>
    /// <param name="y">Y coordinate for distance calculation</param>
    /// <returns>A positive floating-point value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDistance(double x, double y)
    {
        return Math.Sqrt(GetDistanceSq(x, y));
    }

    /// <summary>
    ///     Gets the distance to another vertex.
    /// </summary>
    /// <param name="v">A valid vertex</param>
    /// <returns>The distance to the vertex</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDistance(IVertex v)
    {
        return Math.Sqrt(GetDistanceSq(v.X, v.Y));
    }

    /// <summary>
    ///     Gets the square of the distance to the specified coordinates.
    /// </summary>
    /// <param name="x">X coordinate for distance calculation</param>
    /// <param name="y">Y coordinate for distance calculation</param>
    /// <returns>A positive floating-point value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDistanceSq(double x, double y)
    {
        var dx = X - x;
        var dy = Y - y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    ///     Gets the index of the vertex.
    /// </summary>
    /// <returns>An integer value.</returns>
    public int GetIndex()
    {
        return _index;
    }

    /// <summary>
    ///     Gets a string intended for labeling the vertex.
    /// </summary>
    /// <returns>A valid, non-empty string</returns>
    public string GetLabel()
    {
        return _isSynthetic ? $"S{_index}" : $"{_index}";
    }

    /// <summary>
    ///     Gets the number of vertices grouped together in the collection
    /// </summary>
    /// <returns>
    ///     Normally, a value of 1 or greater; but if the last vertex
    ///     in the group has been removed, a value of zero.
    /// </returns>
    public int GetSize()
    {
        return _list.Count;
    }

    /// <summary>
    ///     Gets an array of the coincident vertices. Each invocation of this method
    ///     results in a new instance of the array.
    /// </summary>
    /// <returns>A valid array of size 1 or greater.</returns>
    public Vertex[] GetVertices()
    {
        return _list.ToArray();
    }

    /// <summary>
    ///     Gets the X coordinate of the vertex.
    /// </summary>
    /// <returns>A valid floating point value.</returns>
    public double GetX()
    {
        return X;
    }

    /// <summary>
    ///     Gets the Y coordinate of the vertex.
    /// </summary>
    /// <returns>A valid floating point value.</returns>
    public double GetY()
    {
        return Y;
    }

    /// <summary>
    ///     Gets the Z coordinate of the vertex based on the resolution rule.
    /// </summary>
    /// <returns>A floating point value or Double.NaN if z value is null.</returns>
    public double GetZ()
    {
        return _zRule;
    }

    /// <summary>
    ///     Checks if this represents a null vertex (ghost vertex).
    ///     VertexMergerGroup is never a null vertex.
    /// </summary>
    /// <returns>Always false for VertexMergerGroup</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNullVertex()
    {
        return false;
    }

    /// <summary>
    ///     Indicates whether this vertex is synthetic.
    /// </summary>
    /// <returns>True if synthetic; otherwise, false</returns>
    public bool IsSynthetic()
    {
        return _isSynthetic;
    }

    /// <summary>
    ///     Removes the specified vertex from the group. If the vertex is
    ///     not currently a member of the group, this operation will be ignored.
    /// </summary>
    /// <param name="v">The vertex to be removed.</param>
    /// <returns>
    ///     True if the vertex was a member of the group and was removed;
    ///     otherwise, false.
    /// </returns>
    public bool RemoveVertex(Vertex v)
    {
        var removed = _list.Remove(v);
        if (removed) ApplyRule();
        return removed;
    }

    /// <summary>
    ///     Sets the auxiliary index for all vertices in the merger group.
    /// </summary>
    /// <param name="auxiliaryIndex">A value in the range 0 to 255</param>
    public void SetAuxiliaryIndex(int auxiliaryIndex)
    {
        for (var i = 0; i < _list.Count; i++)
        {
            var v = _list[i];
            _list[i] = v.WithAuxiliaryIndex(auxiliaryIndex);
        }
    }

    /// <summary>
    ///     Sets the rule for resolving coincident vertices; recalculates
    ///     value for vertex if necessary
    /// </summary>
    /// <param name="rule">A valid member of the enumeration</param>
    public void SetResolutionRule(ResolutionRule rule)
    {
        if (_rule == rule) return;

        _rule = rule;
        ApplyRule();
    }

    /// <summary>
    ///     Gets a string representation of this vertex merger group.
    /// </summary>
    /// <returns>A string with vertex coordinates and group size.</returns>
    public override string ToString()
    {
        return $"VertexMergerGroup({X:F1},{Y:F1}, size={_list.Count}) [{_index}]";
    }

    /// <summary>
    ///     Applies the resolution rule to compute the z value.
    /// </summary>
    private void ApplyRule()
    {
        // Guard against empty list to prevent division by zero
        if (_list.Count == 0)
        {
            _zRule = double.NaN;
            return;
        }

        switch (_rule)
        {
            case ResolutionRule.MeanValue:
                double zSum = 0;
                foreach (var m in _list) zSum += m.GetZ();

                _zRule = zSum / _list.Count;
                break;

            case ResolutionRule.MinValue:
                var zMin = double.PositiveInfinity;
                foreach (var m in _list)
                {
                    if (double.IsNaN(m.GetZ()))
                    {
                        zMin = double.NaN;
                        break;
                    }

                    if (m.GetZ() < zMin) zMin = m.GetZ();
                }

                _zRule = zMin;
                break;

            case ResolutionRule.MaxValue:
                var zMax = double.NegativeInfinity;
                foreach (var m in _list)
                {
                    if (double.IsNaN(m.GetZ()))
                    {
                        zMax = double.NaN;
                        break;
                    }

                    if (m.GetZ() > zMax) zMax = m.GetZ();
                }

                _zRule = zMax;
                break;
        }
    }
}
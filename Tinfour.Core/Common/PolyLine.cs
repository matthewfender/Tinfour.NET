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

using System.Collections;
using System.Collections.ObjectModel;

/// <summary>
///     A base class for features that are represented by a series of vertices.
/// </summary>
public class Polyline : IPolyline
{
    /// <summary>
    ///     The list of vertices defining the polyline.
    /// </summary>
    protected readonly List<IVertex> Vertices;

    private bool _isComplete;

    private double _length;

    private double _minX;

    private double _minY;

    private double _maxX;

    private double _maxY;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Polyline" /> class.
    /// </summary>
    public Polyline()
    {
        Vertices = new List<IVertex>();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Polyline" /> class with the specified vertices.
    /// </summary>
    /// <param name="vertices">A collection of vertices.</param>
    public Polyline(IEnumerable<IVertex> vertices)
    {
        Vertices = new List<IVertex>(vertices);
    }

    /// <inheritdoc />
    public void Add(IVertex v)
    {
        if (_isComplete) throw new InvalidOperationException("Cannot add vertices to a completed polyline.");
        if (Vertices.Count > 0 && Vertices[^1].Equals(v)) return; // Do not add duplicate consecutive vertices

        Vertices.Add(v);
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (_isComplete) return;

        _isComplete = true;
        if (Vertices.Count == 0) return;

        _minX = double.PositiveInfinity;
        _minY = double.PositiveInfinity;
        _maxX = double.NegativeInfinity;
        _maxY = double.NegativeInfinity;

        var p = Vertices[0];
        _minX = p.X;
        _maxX = p.X;
        _minY = p.Y;
        _maxY = p.Y;

        for (var i = 1; i < Vertices.Count; i++)
        {
            var c = Vertices[i];
            _length += p.GetDistance(c);
            if (c.X < _minX) _minX = c.X;
            if (c.X > _maxX) _maxX = c.X;
            if (c.Y < _minY) _minY = c.Y;
            if (c.Y > _maxY) _maxY = c.Y;
            p = c;
        }

        if (IsPolygon() && Vertices.Count > 1)
            _length += Vertices[^1].GetDistance(Vertices[0]);
    }

    /// <inheritdoc />
    public void Densify(double threshold)
    {
        if (threshold <= 0) return;
        var threshold2 = threshold * threshold;

        var newVertices = new List<IVertex>();
        if (Vertices.Count == 0) return;

        newVertices.Add(Vertices[0]);
        for (var i = 0; i < Vertices.Count - 1; i++)
        {
            var v0 = Vertices[i];
            var v1 = Vertices[i + 1];
            var d2 = v0.GetDistanceSq(v1.X, v1.Y);
            if (d2 > threshold2)
            {
                var n = (int)(Math.Sqrt(d2) / threshold + 1);
                var nRec = 1.0 / n;
                for (var j = 1; j < n; j++)
                {
                    var t = j * nRec;
                    var x = v0.X + t * (v1.X - v0.X);
                    var y = v0.Y + t * (v1.Y - v0.Y);
                    var z = v0.GetZ() + t * (v1.GetZ() - v0.GetZ());
                    var vSynth = new Vertex(x, y, z, -1);
                    newVertices.Add(vSynth);
                }
            }

            newVertices.Add(v1);
        }

        Vertices.Clear();
        Vertices.AddRange(newVertices);
        _isComplete = false; // Force re-computation of length, etc.
    }

    /// <inheritdoc />
    public (double Left, double Top, double Width, double Height) GetBounds()
    {
        if (!_isComplete) Complete();
        return (_minX, _minY, _maxX - _minX, _maxY - _minY);
    }

    /// <inheritdoc />
    public IEnumerator<IVertex> GetEnumerator()
    {
        return Vertices.GetEnumerator();
    }

    /// <inheritdoc />
    public double GetLength()
    {
        if (!_isComplete) Complete();
        return _length;
    }

    /// <inheritdoc />
    public double GetNominalPointSpacing()
    {
        if (!_isComplete) Complete();
        if (Vertices.Count < 2) return double.NaN;
        return _length / (Vertices.Count - 1);
    }

    /// <inheritdoc />
    public int GetVertexCount()
    {
        return Vertices.Count;
    }

    /// <inheritdoc />
    public IList<IVertex> GetVertices()
    {
        return new ReadOnlyCollection<IVertex>(Vertices);
    }

    /// <inheritdoc />
    public virtual bool IsPolygon()
    {
        return false;
    }

    /// <inheritdoc />
    public bool IsValid()
    {
        return Vertices.Count >= 2;
    }

    /// <inheritdoc />
    public virtual IPolyline Refactor(IEnumerable<IVertex> geometry)
    {
        return new Polyline(geometry);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
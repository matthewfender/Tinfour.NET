/*
 * Copyright (C) 2019  Gary W. Lucas.
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
 * 01/2019  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     A simple container for holding coordinate pairs, often used as the result
///     of coordinate transformations. This struct provides an efficient mechanism
///     for transferring coordinate data with minimal overhead.
/// </summary>
/// <remarks>
///     This struct is designed for efficiency and provides direct access to its fields.
///     It's commonly used in coordinate transformation operations where performance
///     is critical and the overhead of property accessors is undesirable.
/// </remarks>
public struct CoordinatePair : IEquatable<CoordinatePair>
{
    /// <summary>
    ///     The X (horizontal) coordinate for the pair.
    /// </summary>
    public double X;

    /// <summary>
    ///     The Y (horizontal) coordinate for the pair.
    /// </summary>
    public double Y;

    /// <summary>
    ///     Initializes a new CoordinatePair with the specified coordinates.
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    public CoordinatePair(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    ///     Gets the distance from this coordinate pair to another.
    /// </summary>
    /// <param name="other">The other coordinate pair</param>
    /// <returns>The Euclidean distance between the two points</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double DistanceTo(CoordinatePair other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    ///     Gets the squared distance from this coordinate pair to another.
    ///     This is more efficient than DistanceTo when you only need to compare distances.
    /// </summary>
    /// <param name="other">The other coordinate pair</param>
    /// <returns>The squared Euclidean distance between the two points</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double DistanceSquaredTo(CoordinatePair other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    ///     Gets the distance from this coordinate pair to the specified coordinates.
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    /// <returns>The Euclidean distance to the specified point</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double DistanceTo(double x, double y)
    {
        var dx = X - x;
        var dy = Y - y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    ///     Gets the squared distance from this coordinate pair to the specified coordinates.
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    /// <returns>The squared Euclidean distance to the specified point</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double DistanceSquaredTo(double x, double y)
    {
        var dx = X - x;
        var dy = Y - y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    ///     Sets both coordinates at once.
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    ///     Copies coordinates from another CoordinatePair.
    /// </summary>
    /// <param name="other">The source coordinate pair</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyFrom(CoordinatePair other)
    {
        X = other.X;
        Y = other.Y;
    }

    /// <summary>
    ///     Determines whether this CoordinatePair is equal to another.
    /// </summary>
    /// <param name="other">The other CoordinatePair</param>
    /// <returns>True if the coordinates are exactly equal</returns>
    public readonly bool Equals(CoordinatePair other)
    {
        return X == other.X && Y == other.Y;
    }

    /// <summary>
    ///     Determines whether this CoordinatePair is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare</param>
    /// <returns>True if the object is a CoordinatePair with equal coordinates</returns>
    public override readonly bool Equals(object? obj)
    {
        return obj is CoordinatePair other && Equals(other);
    }

    /// <summary>
    ///     Gets the hash code for this CoordinatePair.
    /// </summary>
    /// <returns>A hash code based on the X and Y coordinates</returns>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    /// <summary>
    ///     Returns a string representation of the coordinate pair.
    /// </summary>
    /// <returns>A string in the format "(X, Y)"</returns>
    public override readonly string ToString()
    {
        return $"({X}, {Y})";
    }

    /// <summary>
    ///     Returns a string representation with the specified format.
    /// </summary>
    /// <param name="format">The format string for the coordinates</param>
    /// <returns>A formatted string representation</returns>
    public readonly string ToString(string format)
    {
        return $"({X.ToString(format)}, {Y.ToString(format)})";
    }

    /// <summary>
    ///     Equality operator for CoordinatePair.
    /// </summary>
    public static bool operator ==(CoordinatePair left, CoordinatePair right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Inequality operator for CoordinatePair.
    /// </summary>
    public static bool operator !=(CoordinatePair left, CoordinatePair right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    ///     Addition operator for CoordinatePair.
    /// </summary>
    public static CoordinatePair operator +(CoordinatePair left, CoordinatePair right)
    {
        return new CoordinatePair(left.X + right.X, left.Y + right.Y);
    }

    /// <summary>
    ///     Subtraction operator for CoordinatePair.
    /// </summary>
    public static CoordinatePair operator -(CoordinatePair left, CoordinatePair right)
    {
        return new CoordinatePair(left.X - right.X, left.Y - right.Y);
    }

    /// <summary>
    ///     Scalar multiplication operator for CoordinatePair.
    /// </summary>
    public static CoordinatePair operator *(CoordinatePair pair, double scalar)
    {
        return new CoordinatePair(pair.X * scalar, pair.Y * scalar);
    }

    /// <summary>
    ///     Scalar multiplication operator for CoordinatePair.
    /// </summary>
    public static CoordinatePair operator *(double scalar, CoordinatePair pair)
    {
        return new CoordinatePair(pair.X * scalar, pair.Y * scalar);
    }

    /// <summary>
    ///     Scalar division operator for CoordinatePair.
    /// </summary>
    public static CoordinatePair operator /(CoordinatePair pair, double scalar)
    {
        return new CoordinatePair(pair.X / scalar, pair.Y / scalar);
    }

    /// <summary>
    ///     Implicit conversion from ValueTuple to CoordinatePair.
    /// </summary>
    public static implicit operator CoordinatePair((double X, double Y) tuple)
    {
        return new CoordinatePair(tuple.X, tuple.Y);
    }

    /// <summary>
    ///     Implicit conversion from CoordinatePair to ValueTuple.
    /// </summary>
    public static implicit operator (double X, double Y)(CoordinatePair pair)
    {
        return (pair.X, pair.Y);
    }

    /// <summary>
    ///     Deconstructs the CoordinatePair into its X and Y components.
    ///     This enables tuple deconstruction syntax: var (x, y) = pair;
    /// </summary>
    /// <param name="x">The X coordinate</param>
    /// <param name="y">The Y coordinate</param>
    public readonly void Deconstruct(out double x, out double y)
    {
        x = X;
        y = Y;
    }
}
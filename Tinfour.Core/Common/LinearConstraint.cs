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
///     Represents a linear constraint for a Triangulated Irregular Network.
/// </summary>
public class LinearConstraint : Polyline, ILinearConstraint
{
    private object? _applicationData;

    private int _constraintIndex;

    private double? _defaultZ;

    private IQuadEdge? _linkingEdge;

    private IIncrementalTin? _managingTin;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinearConstraint" /> class.
    /// </summary>
    public LinearConstraint()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinearConstraint" /> class with the specified vertices.
    /// </summary>
    /// <param name="vertices">A collection of vertices defining the constraint.</param>
    public LinearConstraint(IEnumerable<IVertex> vertices)
        : base(vertices)
    {
        Complete();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="LinearConstraint" /> class with specified vertices and application
    ///     data.
    /// </summary>
    /// <param name="vertices">A collection of vertices defining the constraint.</param>
    /// <param name="applicationData">An application-specific data object.</param>
    public LinearConstraint(IEnumerable<IVertex> vertices, object? applicationData)
        : base(vertices)
    {
        _applicationData = applicationData;
        Complete();
    }

    /// <inheritdoc />
    public bool DefinesConstrainedRegion()
    {
        return false;
    }

    /// <inheritdoc />
    public object? GetApplicationData()
    {
        return _applicationData;
    }

    /// <inheritdoc />
    public int GetConstraintIndex()
    {
        return _constraintIndex;
    }

    /// <inheritdoc />
    public IQuadEdge? GetConstraintLinkingEdge()
    {
        return _linkingEdge;
    }

    /// <inheritdoc />
    public IConstraint GetConstraintWithNewGeometry(IList<IVertex> geometry)
    {
        var newConstraint = new LinearConstraint(geometry) { _applicationData = _applicationData };
        return newConstraint;
    }

    /// <inheritdoc />
    public double? GetDefaultZ()
    {
        return _defaultZ;
    }

    /// <inheritdoc />
    public IIncrementalTin? GetManagingTin()
    {
        return _managingTin;
    }

    /// <summary>
    ///     Gets the number of segments in the linear constraint.
    /// </summary>
    /// <returns>A non-negative integer.</returns>
    public int GetSegmentCount()
    {
        return Vertices.Count > 1 ? Vertices.Count - 1 : 0;
    }

    /// <inheritdoc />
    public bool IsPointInsideConstraint(double x, double y)
    {
        return false;
    }

    /// <inheritdoc />
    public void SetApplicationData(object? applicationData)
    {
        _applicationData = applicationData;
    }

    /// <inheritdoc />
    public void SetConstraintIndex(IIncrementalTin? tin, int index)
    {
        _managingTin = tin;
        _constraintIndex = index;
    }

    /// <inheritdoc />
    public void SetConstraintLinkingEdge(IQuadEdge linkingEdge)
    {
        _linkingEdge = linkingEdge;
    }

    /// <inheritdoc />
    public void SetDefaultZ(double? defaultZ)
    {
        _defaultZ = defaultZ;
    }
}
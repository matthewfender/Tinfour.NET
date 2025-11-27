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
 * Date     Name        Description
 * ------   ---------   -------------------------------------------------
 * 07/2015  G. Lucas    Created
 * 08/2025 M.Fender    Ported to C#
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Collections;

/// <summary>
///     An iterator for traversing edges connected to a vertex in a counterclockwise direction.
/// </summary>
public class PinwheelIterator : IEnumerable<IQuadEdge>
{
    private readonly IQuadEdge _initialEdge;

    /// <summary>
    ///     Constructs a pinwheel iterator for the specified edge.
    /// </summary>
    /// <param name="initialEdge">The starting edge for the traversal.</param>
    public PinwheelIterator(IQuadEdge initialEdge)
    {
        _initialEdge = initialEdge;
    }

    /// <summary>
    ///     Gets the enumerator for iterating through edges connected to the vertex.
    /// </summary>
    /// <returns>An enumerator that performs a pinwheel operation.</returns>
    public IEnumerator<IQuadEdge> GetEnumerator()
    {
        return new PinwheelEnumerator(_initialEdge);
    }

    /// <summary>
    ///     Gets the non-generic enumerator for iterating through edges connected to the vertex.
    /// </summary>
    /// <returns>An enumerator that performs a pinwheel operation.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private class PinwheelEnumerator : IEnumerator<IQuadEdge>
    {
        private readonly IQuadEdge _initialEdge;

        private IQuadEdge? _currentEdge;

        private bool _isComplete;

        private bool _isFirstItem = true;

        public PinwheelEnumerator(IQuadEdge initialEdge)
        {
            _initialEdge = initialEdge;
            _currentEdge = null;
        }

        public IQuadEdge Current => _currentEdge!;

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            // No resources to dispose
        }

        public bool MoveNext()
        {
            if (_isComplete) return false;

            if (_isFirstItem)
            {
                _currentEdge = _initialEdge;
                _isFirstItem = false;
                return true;
            }

            // The pinwheel operation traverses edges connected to a common vertex
            // in a counterclockwise fashion
            try
            {
                _currentEdge = _currentEdge!.GetDualFromReverse();

                // Check if we've gone full circle back to the initial edge
                if (ReferenceEquals(_currentEdge, _initialEdge))
                {
                    _isComplete = true;
                    return false;
                }

                return true;
            }
            catch
            {
                // Handle any null references or other issues gracefully
                _isComplete = true;
                return false;
            }
        }

        public void Reset()
        {
            _currentEdge = null;
            _isFirstItem = true;
            _isComplete = false;
        }
    }
}
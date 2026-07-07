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
 * 12/2016  G. Lucas    Introduced support for constraints
 * 11/2017  G. Lucas    Refactored for constrained regions
 * 08/2025  M. Fender   Ported to C#
 * 07/2026  M. Fender   Converted to a flyweight over EdgeStore (#832)
 * -----------------------------------------------------------------------
 * Notes:
 * In the object-based design this class stored the packed constraint bits
 * in its _index field and the base QuadEdge delegated constraint operations
 * to it through a second virtual dispatch. With EdgeStore, constraint bits
 * live in a per-pair array shared by both sides, so all behavior is
 * expressed handle-generically on the base class. The type is retained as
 * the wrapper for odd (side-1) handles so existing type tests and casts
 * remain valid.
 */

namespace Tinfour.Core.Edge;

/// <summary>
///     The dual (side-1) wrapper of an edge pair.
/// </summary>
public sealed class QuadEdgePartner : QuadEdge
{
    internal QuadEdgePartner(EdgeStore store, int handle)
        : base(store, handle)
    {
    }
}

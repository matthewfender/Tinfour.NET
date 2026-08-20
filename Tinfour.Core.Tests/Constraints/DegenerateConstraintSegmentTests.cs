/*
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

namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Regression tests for ProcessConstraint robustness (ReefMaster #518):
///     sub-tolerance constraint segments (near-duplicate consecutive vertices from
///     upstream clipping round-trips) previously sent the tunnel walk into the ghost
///     region ("Internal failure 345"), and a 20-step pinwheel cap aborted the
///     existing-edge search at high-degree vertices.
/// </summary>
public class DegenerateConstraintSegmentTests
{
    [Fact]
    public void AddConstraints_NearDuplicateConsecutiveVertices_DoesNotThrow()
    {
        // Interior data points inside a square constraint ring that contains a
        // consecutive vertex pair separated by ~1e-9 (value-distinct doubles,
        // far below the vertex tolerance).
        var tin = new IncrementalTin(1.0);
        tin.Add(new List<IVertex>
        {
            new Vertex(2, 2, -1.0),
            new Vertex(8, 2, -2.0),
            new Vertex(5, 8, -3.0),
            new Vertex(5, 4, -2.5),
        });

        var ring = new List<IVertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(10, 0, 0),
            new Vertex(10 + 1e-9, 1e-9, 0), // near-duplicate of the previous vertex
            new Vertex(10, 10, 0),
            new Vertex(0, 10, 0),
        };

        var constraint = new PolygonConstraint(ring);
        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true);

        var constraints = tin.GetConstraints();
        Assert.NotNull(constraints);
        Assert.Single(constraints);

        // The ring must be fully marked: interior region edges exist and the border
        // is closed (every data triangle inside the square is region-interior).
        var interiorCount = 0;
        var borderCount = 0;
        foreach (var e in tin.GetEdges())
        {
            if (e.IsConstraintRegionInterior()) interiorCount++;
            if (e.IsConstraintRegionBorder()) borderCount++;
        }

        Assert.True(borderCount >= 4, $"expected a closed border ring, found {borderCount} border edges");
        Assert.True(interiorCount > 0, "expected interior edges inside the constraint region");
    }

    [Fact]
    public void AddConstraints_HighDegreeConstraintVertex_DoesNotThrow()
    {
        // A star of 40 points around a centre vertex gives the centre a degree far
        // above the removed 20-step pinwheel cap. The constraint runs through the
        // centre, so segment processing must pinwheel the full star.
        var tin = new IncrementalTin(1.0);
        var vertices = new List<IVertex> { new Vertex(0, 0, -1.0) };
        for (var i = 0; i < 40; i++)
        {
            var angle = 2 * Math.PI * i / 40;
            vertices.Add(new Vertex(10 * Math.Cos(angle), 10 * Math.Sin(angle), -2.0));
        }

        tin.Add(vertices);

        // Linear constraint passing through the centre vertex.
        var constraint = new LinearConstraint(new List<IVertex>
        {
            new Vertex(10, 0, 0),
            new Vertex(0, 0, 0),
            new Vertex(10 * Math.Cos(2 * Math.PI * 20 / 40), 10 * Math.Sin(2 * Math.PI * 20 / 40), 0),
        });

        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true);

        var constrainedEdges = 0;
        foreach (var e in tin.GetEdges())
        {
            if (e.IsConstrained()) constrainedEdges++;
        }

        Assert.True(constrainedEdges >= 2, $"expected the constraint path to be marked, found {constrainedEdges} constrained edges");
    }
}

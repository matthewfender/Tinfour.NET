/*
 * Copyright 2025 G.W. Lucas
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

using static Tinfour.Visualiser.Services.CoordinateConverter;

namespace Tinfour.Visualiser.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

/// <summary>
///     Provides methods for loading constraint polygons from files and adding them to a TIN.
/// </summary>
public class ConstraintFileLoader
{
    /// <summary>
    ///     Adds polygon constraints to an existing TIN.
    /// </summary>
    /// <param name="tin">The TIN to add the constraints to</param>
    /// <param name="constraints">The constraints to add</param>
    /// <returns>True if the constraints were added successfully; otherwise, false</returns>
    public static bool AddConstraintsToTin(IncrementalTin tin, IEnumerable<IConstraint> constraints)
    {
        if (tin == null || constraints == null || !tin.IsBootstrapped())
        {
            Debug.WriteLine("AddConstraintsToTin: Invalid input - null tin, constraints, or unbootstrapped tin");
            return false;
        }

        try
        {
            // Convert IEnumerable to IList for AddConstraints method
            var constraintList = constraints.ToList();

            Debug.WriteLine($"AddConstraintsToTin: Starting with {constraintList.Count} constraints");

            // Filter out any constraints with empty vertex lists to prevent ArgumentOutOfRangeException
            var validConstraints = constraintList.Where((IConstraint c) =>
                {
                    var vertices = c.GetVertices().ToList();
                    var isValid = vertices.Count >= 3;

                    if (!isValid)
                        Debug.WriteLine($"Removing invalid constraint with {vertices.Count} vertices");

                    return isValid;
                }).ToList();

            Debug.WriteLine($"AddConstraintsToTin: {validConstraints.Count} valid constraints after filtering");

            if (validConstraints.Count == 0)
            {
                Debug.WriteLine("AddConstraintsToTin: No valid constraints after filtering");
                return false;
            }

            // Validate each constraint has valid coordinates
            foreach (var constraint in validConstraints)
            {
                var vertices = constraint.GetVertices().ToList();
                for (var i = 0; i < vertices.Count; i++)
                {
                    var v = vertices[i];
                    if (double.IsNaN(v.X) || double.IsNaN(v.Y) || double.IsInfinity(v.X) || double.IsInfinity(v.Y))
                        Debug.WriteLine($"Found invalid coordinates in constraint: ({v.X}, {v.Y})");
                }
            }

            // Add the constraints to the TIN
            tin.AddConstraints(validConstraints, true);
            Debug.WriteLine("AddConstraintsToTin: Successfully added constraints");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AddConstraintsToTin: Exception - {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Loads constraint points from a file and creates polygon constraints.
    /// </summary>
    /// <param name="filePath">Path to the CSV/TXT file</param>
    /// <param name="transformationType">Type of coordinate transformation to apply</param>
    /// <returns>Result containing the constraint vertices and statistics</returns>
    public static async Task<LoadResult> LoadFromFileAsync(string filePath, TransformationType transformationType)
    {
        // Open the file as a stream and use the stream-based loader
        using var fileStream = File.OpenRead(filePath);
        return await StreamBasedLoader.LoadConstraintsFromStreamAsync(fileStream, transformationType);
    }

    /// <summary>
    ///     Result from loading a constraint file.
    /// </summary>
    public class LoadResult : FileLoadResultBase
    {
        /// <summary>Number of constraints loaded</summary>
        public int ConstraintCount => this.Constraints.Count;

        /// <summary>List of polygon constraints created from the vertices</summary>
        public List<IConstraint> Constraints { get; set; } = new();

        /// <summary>Dictionary of constraint vertices by constraint index</summary>
        public Dictionary<int, List<IVertex>> ConstraintVertices { get; set; } = new();

        /// <summary>Total number of vertices across all constraints</summary>
        public int TotalPointCount => this.ConstraintVertices.Values.Sum((List<IVertex> v) => v.Count);

        public override string ToString()
        {
            var constraintDetails = string.Join(
                "\n",
                this.ConstraintVertices.Select((KeyValuePair<int, List<IVertex>> kvp) => $"Constraint {kvp.Key}: {kvp.Value.Count} points"));

            return $"Constraints: {this.ConstraintCount}\n" + $"Total Points: {this.TotalPointCount}\n"
                                                            + $"Projection: {this.ProjectionDescription}\n"
                                                            + $"X range: {this.MinX:F2} to {this.MaxX:F2}\n"
                                                            + $"Y range: {this.MinY:F2} to {this.MaxY:F2}\n"
                                                            + (this.ConstraintCount <= 5 ? constraintDetails : string.Empty);
        }
    }
}
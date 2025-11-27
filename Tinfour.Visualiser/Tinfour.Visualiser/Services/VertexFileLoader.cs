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

using System.IO;
using System.Threading.Tasks;

using Tinfour.Core.Standard;

/// <summary>
///     Provides methods for loading vertices from files and creating TINs.
/// </summary>
public class VertexFileLoader
{
    /// <summary>
    ///     Loads vertices from a file and creates a TIN.
    /// </summary>
    /// <param name="filePath">Path to the CSV/TXT file</param>
    /// <param name="transformationType">Type of coordinate transformation to apply</param>
    /// <returns>Result containing the TIN and statistics</returns>
    public static async Task<LoadResult> LoadFromFileAsync(string filePath, TransformationType transformationType)
    {
        // Open the file as a stream and use the stream-based loader
        using var fileStream = File.OpenRead(filePath);
        return await StreamBasedLoader.LoadVerticesFromStreamAsync(fileStream, transformationType);
    }

    /// <summary>
    ///     Result from loading a vertex file.
    /// </summary>
    public class LoadResult : FileLoadResultBase
    {
        public double MaxDepth { get; set; }

        /// <summary>Min/max depth values</summary>
        public double MinDepth { get; set; }

        /// <summary>The created triangulation</summary>
        public required IncrementalTin Tin { get; set; }

        /// <summary>Number of vertices loaded</summary>
        public int VertexCount { get; set; }

        public override string ToString()
        {
            return $"Vertices: {this.VertexCount}\n" + $"Projection: {this.ProjectionDescription}\n"
                                                     + $"X range: {this.MinX:F2} to {this.MaxX:F2}\n"
                                                     + $"Y range: {this.MinY:F2} to {this.MaxY:F2}\n"
                                                     + $"Depth range: {this.MinDepth:F2} to {this.MaxDepth:F2} m";
        }
    }
}
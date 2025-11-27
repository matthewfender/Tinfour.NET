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

namespace Tinfour.Visualiser.Services;

using System;

/// <summary>
///     Provides methods for converting between coordinate systems.
/// </summary>
public static class CoordinateConverter
{
    // WGS84 ellipsoid parameters
    private const double WGS84_a = 6378137.0; // semi-major axis in meters

    private const double WGS84_b = WGS84_a * (1.0 - WGS84_f); // semi-minor axis

    private const double WGS84_f = 1.0 / 298.257223563; // flattening

    /// <summary>
    ///     Enum defining coordinate transformation types.
    /// </summary>
    public enum TransformationType
    {
        /// <summary>No transformation, use original lat/lon values as x,y</summary>
        None,

        /// <summary>Web Mercator projection (EPSG:3857)</summary>
        WebMercator,

        /// <summary>Universal Transverse Mercator projection</summary>
        UTM
    }

    /// <summary>
    ///     Approximates UTM zone for the given longitude
    /// </summary>
    /// <param name="longitude">Longitude in decimal degrees</param>
    /// <returns>UTM zone number (1-60)</returns>
    public static int GetUTMZone(double longitude)
    {
        return (int)Math.Floor((longitude + 180) / 6) + 1;
    }

    /// <summary>
    ///     Converts WGS84 latitude/longitude to UTM coordinates.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees</param>
    /// <param name="longitude">Longitude in decimal degrees</param>
    /// <returns>X,Y coordinates in UTM projection (meters) and the UTM zone</returns>
    public static (double X, double Y, int Zone) LatLonToUTM(double latitude, double longitude)
    {
        // Determine the UTM zone
        var zone = GetUTMZone(longitude);

        // Central meridian for the zone
        double centralMeridian = (zone - 1) * 6 - 180 + 3; // +3 puts origin in middle of zone

        // Convert to radians
        var latRad = latitude * Math.PI / 180.0;
        var lonRad = longitude * Math.PI / 180.0;
        var centralMeridianRad = centralMeridian * Math.PI / 180.0;

        // UTM scale factor
        var k0 = 0.9996;

        // Calculate eccentricity
        var e = Math.Sqrt(2 * WGS84_f - WGS84_f * WGS84_f);
        var e2 = e * e;
        var e4 = e2 * e2;
        var e6 = e2 * e4;

        // Calculate N, T, C, A, M
        var N = WGS84_a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
        var T = Math.Tan(latRad) * Math.Tan(latRad);
        var C = e2 * Math.Cos(latRad) * Math.Cos(latRad) / (1 - e2);
        var A = Math.Cos(latRad) * (lonRad - centralMeridianRad);

        // Calculate M (meridional arc)
        var M = WGS84_a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latRad
                           - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latRad)
                           + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latRad)
                           - 35 * e6 / 3072 * Math.Sin(6 * latRad));

        // Calculate UTM coordinates
        var x = k0 * N * (A + (1 - T + C) * A * A * A / 6
                            + (5 - 18 * T + T * T + 72 * C - 58 * e2) * A * A * A * A * A / 120);

        // Add false easting
        x += 500000;

        // Calculate y coordinate
        var y = k0 * (M + N * Math.Tan(latRad) * (A * A / 2 + (5 - T + 9 * C + 4 * C * C) * A * A * A * A / 24
                                                            + (61 - 58 * T + T * T + 600 * C - 330 * e2) * A * A * A * A
                                                            * A * A / 720));

        // Handle southern hemisphere
        if (latitude < 0) y += 10000000; // Add false northing for southern hemisphere

        return (x, y, zone);
    }

    /// <summary>
    ///     Converts WGS84 latitude/longitude coordinates to Web Mercator (EPSG:3857) coordinates.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees</param>
    /// <param name="longitude">Longitude in decimal degrees</param>
    /// <returns>X,Y coordinates in Web Mercator projection (meters)</returns>
    public static (double X, double Y) LatLonToWebMercator(double latitude, double longitude)
    {
        // Ensure latitude is within valid range (-85.06, 85.06)
        latitude = Math.Max(Math.Min(latitude, 85.06), -85.06);

        // Convert to radians
        var latRad = latitude * Math.PI / 180.0;
        var lonRad = longitude * Math.PI / 180.0;

        // Calculate x coordinate
        var x = WGS84_a * lonRad;

        // Calculate y coordinate
        var y = WGS84_a * Math.Log(Math.Tan(Math.PI / 4 + latRad / 2));

        return (x, y);
    }
}
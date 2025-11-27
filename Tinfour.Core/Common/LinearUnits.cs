/*
 * Copyright 2016 Gary W. Lucas.
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
 * 06/2016  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

/// <summary>
///     An enumeration for specifying linear units of measure.
/// </summary>
public enum LinearUnits
{
    /// <summary>
    ///     Unknown units of measure.
    /// </summary>
    Unknown,

    /// <summary>
    ///     Meters (SI base unit).
    /// </summary>
    Meters,

    /// <summary>
    ///     Feet (Imperial/US customary).
    /// </summary>
    Feet,

    /// <summary>
    ///     Fathoms (nautical unit).
    /// </summary>
    Fathoms,

    /// <summary>
    ///     Kilometers.
    /// </summary>
    Kilometers,

    /// <summary>
    ///     Miles (statute miles).
    /// </summary>
    Miles,

    /// <summary>
    ///     Nautical miles.
    /// </summary>
    NauticalMiles,

    /// <summary>
    ///     Inches.
    /// </summary>
    Inches,

    /// <summary>
    ///     Centimeters.
    /// </summary>
    Centimeters,

    /// <summary>
    ///     Millimeters.
    /// </summary>
    Millimeters
}

/// <summary>
///     Extension methods for LinearUnits enumeration.
/// </summary>
public static class LinearUnitsExtensions
{
    private static readonly Dictionary<LinearUnits, LinearUnitInfo> UnitInfoMap = new()
                                                                                      {
                                                                                          { LinearUnits.Unknown, new LinearUnitInfo("unknown", 1.0, "Unknown") },
                                                                                          { LinearUnits.Meters, new LinearUnitInfo("m", 1.0, "Meters") },
                                                                                          { LinearUnits.Feet, new LinearUnitInfo("ft", 0.3048, "Feet") },
                                                                                          { LinearUnits.Fathoms, new LinearUnitInfo("fathoms", 1.8288, "Fathoms") },
                                                                                          { LinearUnits.Kilometers, new LinearUnitInfo("km", 1000.0, "Kilometers") },
                                                                                          { LinearUnits.Miles, new LinearUnitInfo("mi", 1609.344, "Miles") },
                                                                                          { LinearUnits.NauticalMiles, new LinearUnitInfo("nmi", 1852.0, "Nautical Miles") },
                                                                                          { LinearUnits.Inches, new LinearUnitInfo("in", 0.0254, "Inches") },
                                                                                          { LinearUnits.Centimeters, new LinearUnitInfo("cm", 0.01, "Centimeters") },
                                                                                          { LinearUnits.Millimeters, new LinearUnitInfo("mm", 0.001, "Millimeters") }
                                                                                      };

    /// <summary>
    ///     Convert a value from one unit to another.
    /// </summary>
    /// <param name="fromUnit">The source unit</param>
    /// <param name="value">The value in the source unit</param>
    /// <param name="toUnit">The target unit</param>
    /// <returns>The value converted to the target unit</returns>
    public static double Convert(this LinearUnits fromUnit, double value, LinearUnits toUnit)
    {
        if (fromUnit == toUnit) return value;

        var meters = fromUnit.ToMeters(value);
        return toUnit.FromMeters(meters);
    }

    /// <summary>
    ///     Convert a value specified in meters to the unit system indicated
    ///     by this enumeration instance.
    /// </summary>
    /// <param name="unit">The target unit</param>
    /// <param name="value">A value in meters</param>
    /// <returns>A value in the specified unit system</returns>
    public static double FromMeters(this LinearUnits unit, double value)
    {
        return value / UnitInfoMap[unit].MetersConversion;
    }

    /// <summary>
    ///     Get the abbreviation for the unit of measure. Where appropriate
    ///     this will be an SI abbreviation given in lower case.
    /// </summary>
    /// <param name="unit">The linear unit</param>
    /// <returns>A valid string</returns>
    public static string GetAbbreviation(this LinearUnits unit)
    {
        return UnitInfoMap[unit].Abbreviation;
    }

    /// <summary>
    ///     Gets all available linear units.
    /// </summary>
    /// <returns>An enumerable of all linear units</returns>
    public static IEnumerable<LinearUnits> GetAllUnits()
    {
        return Enum.GetValues<LinearUnits>();
    }

    /// <summary>
    ///     Gets the conversion factor from the source unit to the target unit.
    /// </summary>
    /// <param name="fromUnit">The source unit</param>
    /// <param name="toUnit">The target unit</param>
    /// <returns>The multiplication factor to convert from source to target</returns>
    public static double GetConversionFactor(this LinearUnits fromUnit, LinearUnits toUnit)
    {
        if (fromUnit == toUnit) return 1.0;

        return UnitInfoMap[fromUnit].MetersConversion / UnitInfoMap[toUnit].MetersConversion;
    }

    /// <summary>
    ///     Gets the name of the units in a form suitable for user interface display.
    /// </summary>
    /// <param name="unit">The linear unit</param>
    /// <returns>A valid string</returns>
    public static string GetName(this LinearUnits unit)
    {
        return UnitInfoMap[unit].Name;
    }

    /// <summary>
    ///     Determines if the unit represents an imperial/US customary unit.
    /// </summary>
    /// <param name="unit">The linear unit</param>
    /// <returns>True if the unit is imperial, false otherwise</returns>
    public static bool IsImperial(this LinearUnits unit)
    {
        return unit switch
            {
                LinearUnits.Feet or LinearUnits.Inches or LinearUnits.Miles => true,
                _ => false
            };
    }

    /// <summary>
    ///     Determines if the unit represents a metric system unit.
    /// </summary>
    /// <param name="unit">The linear unit</param>
    /// <returns>True if the unit is metric, false otherwise</returns>
    public static bool IsMetric(this LinearUnits unit)
    {
        return unit switch
            {
                LinearUnits.Meters or LinearUnits.Kilometers or LinearUnits.Centimeters
                    or LinearUnits.Millimeters => true,
                _ => false
            };
    }

    /// <summary>
    ///     Convert the specified value to meters.
    /// </summary>
    /// <param name="unit">The source unit</param>
    /// <param name="value">A valid numeric value in the specified unit system</param>
    /// <returns>A valid floating-point value, in meters</returns>
    public static double ToMeters(this LinearUnits unit, double value)
    {
        return value * UnitInfoMap[unit].MetersConversion;
    }

    /// <summary>
    ///     Tries to parse a unit from its abbreviation or name.
    /// </summary>
    /// <param name="text">The text to parse</param>
    /// <param name="unit">The parsed unit, if successful</param>
    /// <returns>True if parsing was successful, false otherwise</returns>
    public static bool TryParse(string text, out LinearUnits unit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            unit = LinearUnits.Unknown;
            return false;
        }

        text = text.Trim();

        // Try exact abbreviation match (case-sensitive)
        foreach (var kvp in UnitInfoMap)
            if (kvp.Value.Abbreviation.Equals(text, StringComparison.Ordinal))
            {
                unit = kvp.Key;
                return true;
            }

        // Try case-insensitive name match
        foreach (var kvp in UnitInfoMap)
            if (kvp.Value.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                unit = kvp.Key;
                return true;
            }

        // Try case-insensitive abbreviation match
        foreach (var kvp in UnitInfoMap)
            if (kvp.Value.Abbreviation.Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                unit = kvp.Key;
                return true;
            }

        unit = LinearUnits.Unknown;
        return false;
    }

    /// <summary>
    ///     Information about a linear unit.
    /// </summary>
    /// <param name="Abbreviation">The unit abbreviation</param>
    /// <param name="MetersConversion">Conversion factor to meters</param>
    /// <param name="Name">The full name of the unit</param>
    public readonly record struct LinearUnitInfo(string Abbreviation, double MetersConversion, string Name);
}
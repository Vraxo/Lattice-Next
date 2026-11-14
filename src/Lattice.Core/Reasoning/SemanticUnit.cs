namespace Lattice.Core.Reasoning;

public static class SemanticUnit
{
    public static bool TryGetDimension(string? unit, out string dimension)
    {
        dimension = unit switch
        {
            "N" or "kN" => "force",
            "m" or "cm" or "mm" => "length",
            "m2" or "cm2" or "mm2" => "area",
            "Pa" or "kPa" or "MPa" or "GPa" => "pressure",
            "kg" => "mass",
            "s" => "time",
            _ => string.Empty
        };
        return dimension.Length > 0;
    }

    public static string GetBaseUnit(string dimension) => dimension switch
    {
        "force" => "N",
        "length" => "m",
        "area" => "m2",
        "pressure" => "Pa",
        "mass" => "kg",
        "time" => "s",
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };

    public static bool TryConvertToBase(decimal value, string? unit, out string dimension, out decimal baseValue)
    {
        baseValue = 0;
        if (!TryGetDimension(unit, out dimension))
            return false;
        var factor = unit switch
        {
            "kN" => 1000m,
            "cm" => 0.01m,
            "mm" => 0.001m,
            "cm2" => 0.0001m,
            "mm2" => 0.000001m,
            "kPa" => 1000m,
            "MPa" => 1000000m,
            "GPa" => 1000000000m,
            _ => 1m
        };
        try
        {
            baseValue = value * factor;
            return true;
        }
        catch (OverflowException)
        {
            dimension = string.Empty;
            return false;
        }
    }
}

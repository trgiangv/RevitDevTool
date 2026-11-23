using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Parameters;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Parameters;
using System.Globalization;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal readonly record struct RuleNumber(double Value, double Tolerance);

internal sealed class RuleValueParser(Units units)
{
    public const double Tolerance = 1e-6;
    private const double TieShare = 1e-6;

    public static RuleValueParser For(Units units)
    {
        return new RuleValueParser(units);
    }

    public static RuleNumber Picked(NumberRange range)
    {
        return new RuleNumber(range.Middle, range.HalfSpan + Tolerance);
    }

    public static string? Example(Units units, string specId)
    {
        var spec = new ForgeTypeId(specId);
        if (!SpecInfo.IsMeasurable(spec))
        {
            return null;
        }

        try
        {
            var unit = units.GetFormatOptions(spec).GetUnitTypeId();
            var shown = UnitUtils.ConvertFromInternalUnits(1, unit);
            if (!(Math.Abs(shown) > 0))
            {
                return null;
            }

            var step = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(shown))));
            var rounded = Math.Round(shown / step) * step;
            return UnitFormatUtils.Format(units, spec, UnitUtils.ConvertToInternalUnits(rounded, unit), false);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Example value", specId);
            return null;
        }
    }

    public static bool TryInteger(string text, out int value)
    {
        var trimmed = text.Trim();
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
               || int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public bool TryNumber(ResolvedParameter parameter, string text, out RuleNumber number)
    {
        var trimmed = text.Trim();
        if (parameter.Spec is { } spec && SpecInfo.IsMeasurable(spec))
        {
            try
            {
                if (UnitFormatUtils.TryParse(units, spec, trimmed, out var measured))
                {
                    number = Displayed(spec, measured);
                    return true;
                }
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Parse number", parameter.Key.Name);
            }
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var plain)
            || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out plain))
        {
            number = new RuleNumber(plain, Tolerance);
            return true;
        }

        number = default;
        return false;
    }

    private RuleNumber Displayed(ForgeTypeId spec, double value)
    {
        var options = units.GetFormatOptions(spec);
        var unit = options.GetUnitTypeId();
        var step = Math.Abs(UnitUtils.ConvertToInternalUnits(options.Accuracy, unit) - UnitUtils.ConvertToInternalUnits(0, unit));
        if (!(step > Tolerance))
        {
            return new RuleNumber(value, Tolerance);
        }

        var half = step / 2;
        var tie = half * TieShare;
        return options.RoundingMethod switch
        {
            RoundingMethod.Up => new RuleNumber(value - half + tie, half),
            RoundingMethod.Down => new RuleNumber(value + half - tie, half),
            _ when value > 0 => new RuleNumber(value - tie, half),
            _ when value < 0 => new RuleNumber(value + tie, half),
            _ => new RuleNumber(value, half - tie)
        };
    }
}

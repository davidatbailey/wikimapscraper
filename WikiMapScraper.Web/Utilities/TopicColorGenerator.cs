namespace WikiMapScraper.Web.Utilities;

public static class TopicColorGenerator
{
    public static string GenerateHexColor(string topic)
    {
        var normalized = topic.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "#2A7F62";
        }

        unchecked
        {
            var hash = 17;
            foreach (var c in normalized)
            {
                hash = (hash * 31) + c;
            }

            var hue = Math.Abs(hash % 360);
            return HslToHex(hue, 68, 46);
        }
    }

    private static string HslToHex(int h, int sPercent, int lPercent)
    {
        var s = sPercent / 100.0;
        var l = lPercent / 100.0;

        var c = (1 - Math.Abs((2 * l) - 1)) * s;
        var x = c * (1 - Math.Abs(((h / 60.0) % 2) - 1));
        var m = l - (c / 2);

        double rPrime;
        double gPrime;
        double bPrime;

        if (h < 60)
        {
            rPrime = c;
            gPrime = x;
            bPrime = 0;
        }
        else if (h < 120)
        {
            rPrime = x;
            gPrime = c;
            bPrime = 0;
        }
        else if (h < 180)
        {
            rPrime = 0;
            gPrime = c;
            bPrime = x;
        }
        else if (h < 240)
        {
            rPrime = 0;
            gPrime = x;
            bPrime = c;
        }
        else if (h < 300)
        {
            rPrime = x;
            gPrime = 0;
            bPrime = c;
        }
        else
        {
            rPrime = c;
            gPrime = 0;
            bPrime = x;
        }

        var r = (int)Math.Round((rPrime + m) * 255);
        var g = (int)Math.Round((gPrime + m) * 255);
        var b = (int)Math.Round((bPrime + m) * 255);

        return $"#{r:X2}{g:X2}{b:X2}";
    }
}

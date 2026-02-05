using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Globalization;



namespace StarMap2010
{
    public static class ColorUtil
    {
        // Accepts: "RRGGBB", "#RRGGBB", "AARRGGBB", "#AARRGGBB", optionally "RGB", "ARGB"
        // Returns fallback if invalid.
        public static Color FromSqliteHex(object sqliteValue, Color fallback)
        {
            if (sqliteValue == null || sqliteValue == DBNull.Value)
                return fallback;

            string hex = sqliteValue.ToString();
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);

            // Optional: support shorthand #RGB / #ARGB
            if (hex.Length == 3) // RGB -> RRGGBB
            {
                hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            }
            else if (hex.Length == 4) // ARGB -> AARRGGBB
            {
                hex = new string(new[] {
                hex[0], hex[0],
                hex[1], hex[1],
                hex[2], hex[2],
                hex[3], hex[3]
            });
            }

            try
            {
                if (hex.Length == 6)
                {
                    int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(r, g, b); // alpha = 255
                }
                else if (hex.Length == 8)
                {
                    int a = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    int r = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    int g = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    int b = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // fall through to fallback
            }

            return fallback;
        }

        // Convenience overload: defaults to white if missing/invalid
        public static Color FromSqliteHex(object sqliteValue)
        {
            return FromSqliteHex(sqliteValue, Color.White);
        }

        public static string HexWithTransparency(string hex, int transparencyPercent)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "FFFFFFFF"; // fallback white, opaque

            hex = hex.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);

            if (transparencyPercent < 0) transparencyPercent = 0;
            if (transparencyPercent > 100) transparencyPercent = 100;

            // Convert transparency % → alpha
            // 0% = fully opaque (255), 100% = fully transparent (0)
            int alpha = 255 - (int)Math.Round(255 * (transparencyPercent / 100.0));

            // If hex already has alpha, strip it
            if (hex.Length == 8)
                hex = hex.Substring(2);

            // Now hex must be RRGGBB
            if (hex.Length != 6)
                return "FFFFFFFF";

            return string.Format(
                "{0:X2}{1}",
                alpha,
                hex.ToUpper()
            );
        }
    }
}

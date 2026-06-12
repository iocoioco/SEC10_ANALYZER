using System;
using System.Drawing;

namespace New_Tradegy.Library.Utils
{
    public static class ThemeUtils
    { 

        public static Color BuyColor => Color.LightGreen;
        public static Color SellColor => Color.LightCoral;
        public static Color IndexColor => Color.LightSkyBlue;
        public static Color WarningColor => Color.Orange;
        public static Color ErrorColor => Color.Red;
        public static Color Background => Color.White;
        public static Color Foreground => Color.Black;

        

        // Grid Highlight Colors
        public static Color Highlight => Color.Yellow;
        public static Color BookBidBackground => Color.WhiteSmoke;

        

        // 🔁 Toggle intensity (light/dark) if needed
        public static Color Lighten(Color color, int amount = 30)
        {
            return Color.FromArgb(
                color.A,
                Math.Min(color.R + amount, 255),
                Math.Min(color.G + amount, 255),
                Math.Min(color.B + amount, 255));
        }

        public static Color Darken(Color color, int amount = 30)
        {
            return Color.FromArgb(
                color.A,
                Math.Max(color.R - amount, 0),
                Math.Max(color.G - amount, 0),
                Math.Max(color.B - amount, 0));
        }

        // 👁️ Check if color is bright enough to use dark text
        public static bool IsBright(Color color)
        {
            int brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            return brightness > 125;
        }

        // 🅰️ Contrast check: Black or White foreground?
        public static Color BestTextColor(Color background)
        {
            return IsBright(background) ? Color.Black : Color.White;
        }

        // Fonts
        public static Font DefaultFont => new Font("Segoe UI", 9, FontStyle.Regular);
        public static Font TitleFont => new Font("Segoe UI", 11, FontStyle.Bold);
        public static Font SmallFont => new Font("Segoe UI", 8);

        // Optional padding/margin constants
        public const int CellPadding = 4;
    }
}




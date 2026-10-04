using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    // Added Weight format
    public enum SliderFormat { Fixed0, Fixed1, Fixed2, Multiplier, Percent, PercentDecimal, Weight }

    public static class SettingsUI
    {
        public const float StandardGap = 12f;

        public static float LabeledSlider(Listing_Standard l, string label, float val, float min, float max, string tooltip = null, float step = 0.1f, SliderFormat format = SliderFormat.Fixed1)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, tooltip);

            string displaySuffix = GetFormattedValue(val, format);
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {displaySuffix.Colorize(Color.cyan)}");
            return Widgets.HorizontalSlider(r.RightPart(0.5f), val, min, max, false, null, null, null, step);
        }

        public static float WeightSlider(Listing_Standard l, string label, float val, float totalPool, float min, float max, string tooltip = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, tooltip);

            float pct = (totalPool > 0) ? (val / totalPool) : 0;
            // Matches original look: "Weight: 50 (12.5%)"
            string displaySuffix = $"{val:F0} ({pct:P1})";

            Widgets.Label(r.LeftPart(0.5f), $"{label}: {displaySuffix.Colorize(Color.cyan)}");
            return Mathf.Round(Widgets.HorizontalSlider(r.RightPart(0.5f), val, min, max, false, null, null, null, 1f));
        }

        public static void MultiColumnSlider(Listing_Standard l, string[] labels, float[] values, Vector2 minMax, string[] tooltips = null, float step = 0.1f, SliderFormat format = SliderFormat.Fixed1, float height = 44f)
        {
            l.Gap(4f);
            Rect rowRect = l.GetRect(height);
            int count = labels.Length;
            float colWidth = rowRect.width / count;

            for (int i = 0; i < count; i++)
            {
                Rect colRect = new Rect(rowRect.x + (colWidth * i), rowRect.y, colWidth - 10f, rowRect.height);
                if (tooltips != null && i < tooltips.Length && !tooltips[i].NullOrEmpty())
                    TooltipHandler.TipRegion(colRect, tooltips[i]);

                string suffix = GetFormattedValue(values[i], format);
                Widgets.Label(colRect.TopPart(0.6f), $"<b>{labels[i]}</b>: {suffix.Colorize(Color.cyan)}");
                values[i] = Widgets.HorizontalSlider(colRect.BottomPart(0.4f), values[i], minMax.x, minMax.y, false, null, null, null, step);
            }
        }

        public static void DrawHeader(Listing_Standard l, string label, Color? color = null)
        {
            l.Gap(StandardGap);
            Text.Font = GameFont.Small;
            string colorHex = color.HasValue ? ColorUtility.ToHtmlStringRGBA(color.Value) : "FFFFFF";
            string styledLabel = $"<size=15><b><color=#{colorHex}>{label}</color></b></size>";
            l.Label(styledLabel);
            l.GapLine(4f);
            l.Gap(StandardGap);
        }

        private static string GetFormattedValue(float val, SliderFormat format)
        {
            switch (format)
            {
                case SliderFormat.Fixed0: return val.ToString("F0");
                case SliderFormat.Fixed1: return val.ToString("F1");
                case SliderFormat.Fixed2: return val.ToString("F2");
                case SliderFormat.Multiplier: return val.ToString("F2") + "x";
                case SliderFormat.Percent: return (val * 100f).ToString("F0") + " %";
                case SliderFormat.PercentDecimal: return (val * 100f).ToString("F1") + " %"; 
                default: return val.ToString();
            }
        }
    }
}
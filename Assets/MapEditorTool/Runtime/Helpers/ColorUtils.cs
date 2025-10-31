using Unity.Mathematics;
using UnityEngine;

namespace MapEditorTool.Runtime.Helpers
{
    public static class ColorUtils
    {
        public static Color Lerp(Color a, Color b, float t)
        {
            t = math.clamp(t, 0, 1);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
        
        public static int ColorToInt(Color color)
        {
            color = Color.Lerp(Color.clear, color, color.a); // 预乘alpha
            return ((int)(color.r * 255) << 24) |
                   ((int)(color.g * 255) << 16) |
                   ((int)(color.b * 255) << 8) |
                   (int)(color.a * 255);
        }
        
        public static Color IntToColor(int colorInt)
        {
            if (colorInt == 0) return Color.clear;

            return new Color(
                ((colorInt >> 24) & 0xFF) / 255f,
                ((colorInt >> 16) & 0xFF) / 255f,
                ((colorInt >> 8) & 0xFF) / 255f,
                (colorInt & 0xFF) / 255f
            );
        }

        public static Color32 IntToColor32(int colorInt)
        {
            return new Color32(
                (byte)((colorInt >> 24) & 0xFF),
                (byte)((colorInt >> 16) & 0xFF),
                (byte)((colorInt >> 8) & 0xFF),
                (byte)(colorInt & 0xFF)
            );
        }
    }
}
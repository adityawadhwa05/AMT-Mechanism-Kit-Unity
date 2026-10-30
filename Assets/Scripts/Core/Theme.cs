using UnityEngine;

namespace AMT.AR.Core
{
    // Design tokens shared by the UI, overlays and 3D highlight colours.
    public static class Theme
    {
        // Surfaces (dark industrial)
        public static readonly Color Canvas = Hex(0x0B0E12);
        public static readonly Color Surface = Hex(0x12161C, .94f);
        public static readonly Color SurfaceRaised = Hex(0x1A2028, .97f);
        public static readonly Color SurfaceHover = Hex(0x232B35);
        public static readonly Color Stroke = Hex(0x2B3440);
        public static readonly Color Scrim = Hex(0x05070A, .72f);

        // Text
        public static readonly Color Text = Hex(0xE9EEF3);
        public static readonly Color TextMuted = Hex(0x9AA7B4);
        public static readonly Color TextFaint = Hex(0x66727E);
        public static readonly Color TextOnAccent = Hex(0x14110A);

        // Accents: safety amber for primary actions/selection, cyan for information and motion.
        public static readonly Color Accent = Hex(0xF5A524);
        public static readonly Color AccentSoft = Hex(0xF5A524, .16f);
        public static readonly Color Info = Hex(0x46C2CB);
        public static readonly Color InfoSoft = Hex(0x46C2CB, .16f);
        public static readonly Color Success = Hex(0x3DD68C);
        public static readonly Color SuccessSoft = Hex(0x3DD68C, .16f);
        public static readonly Color Warning = Hex(0xF2C14E);
        public static readonly Color WarningSoft = Hex(0xF2C14E, .14f);
        public static readonly Color Danger = Hex(0xF2555A);
        public static readonly Color DangerSoft = Hex(0xF2555A, .16f);

        // 3D
        public static readonly Color Highlight = Hex(0xFFA51F);
        public static readonly Color Related = Hex(0x46C2CB);
        public static readonly Color ViewerBackground = Hex(0x0E1217);

        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        public static Color CategoryColor(string category)
        {
            switch (category)
            {
                case "Motion": return Accent;
                case "Sensing": return Info;
                case "Actuation": return Hex(0xE07BE0);
                case "Product": return Success;
                case "Electrical": return Hex(0x8C9EFF);
                case "Fasteners": return Hex(0xB0BEC5);
                default: return Hex(0x9AA7B4);
            }
        }
    }
}

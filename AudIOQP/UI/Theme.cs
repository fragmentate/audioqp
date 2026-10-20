using Microsoft.Win32;

namespace AudIOQP.UI;

internal static class Theme
{
    public static bool IsDark { get; }
    public static Color Background { get; }
    public static Color Surface { get; }
    public static Color Text { get; }
    public static Color Muted { get; }
    public static Color Accent { get; }
    public static Color AccentText { get; }
    public static Color Selection { get; }
    public static Color SelectionText { get; }
    public static Color Hover { get; }
    public static Color Border { get; }
    public static Color Danger { get; }
    public static Color Overlay { get; }
    public static Color OverlayText { get; }
    public static Color OverlayMuted { get; }

    static Theme()
    {
        if (SystemInformation.HighContrast)
        {
            Background = SystemColors.Window;
            Surface = SystemColors.Window;
            Text = SystemColors.WindowText;
            Muted = SystemColors.GrayText;
            Accent = SystemColors.Highlight;
            AccentText = SystemColors.HighlightText;
            Selection = SystemColors.Highlight;
            SelectionText = SystemColors.HighlightText;
            Hover = SystemColors.ControlLight;
            Border = SystemColors.WindowFrame;
            Danger = SystemColors.WindowText;
            Overlay = SystemColors.Window;
            OverlayText = SystemColors.WindowText;
            OverlayMuted = SystemColors.GrayText;
            return;
        }

        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1);
        IsDark = value is int mode && mode == 0;
        if (IsDark)
        {
            Background = Color.FromArgb(32, 32, 32);
            Surface = Color.FromArgb(43, 43, 43);
            Text = Color.FromArgb(242, 242, 242);
            Muted = Color.FromArgb(176, 176, 176);
            Accent = Color.FromArgb(96, 205, 255);
            AccentText = Color.FromArgb(16, 16, 16);
            Selection = Color.FromArgb(0, 103, 192);
            SelectionText = Color.White;
            Hover = Color.FromArgb(55, 55, 55);
            Border = Color.FromArgb(72, 72, 72);
            Danger = Color.FromArgb(255, 153, 164);
            Overlay = Color.FromArgb(28, 28, 28);
            OverlayText = Color.White;
            OverlayMuted = Color.FromArgb(186, 214, 232);
        }
        else
        {
            Background = Color.White;
            Surface = Color.FromArgb(248, 248, 248);
            Text = Color.FromArgb(28, 28, 28);
            Muted = Color.FromArgb(96, 96, 96);
            Accent = Color.FromArgb(0, 120, 212);
            AccentText = Color.White;
            Selection = Color.FromArgb(0, 120, 212);
            SelectionText = Color.White;
            Hover = Color.FromArgb(243, 246, 250);
            Border = Color.FromArgb(220, 220, 220);
            Danger = Color.FromArgb(164, 38, 44);
            Overlay = Color.FromArgb(28, 28, 28);
            OverlayText = Color.White;
            OverlayMuted = Color.FromArgb(186, 214, 232);
        }
    }
}

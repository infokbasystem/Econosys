using QuestPDF.Drawing;

namespace Econosys.Api.Services;

public static class PdfFontRegistration
{
    public static void Register()
    {
        QuestPDF.Settings.UseEnvironmentFonts = false;
        var fontsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
        RegisterFont(fontsDirectory, "DMSans-Regular.ttf");
        RegisterFont(fontsDirectory, "DMSans-Italic.ttf");
        RegisterFont(fontsDirectory, "DMSans-Bold.ttf");
        RegisterFont(fontsDirectory, "DMSans-BoldItalic.ttf");
        RegisterFont(fontsDirectory, "DMSans-SemiBold.ttf");
        RegisterFont(fontsDirectory, "DMSans-SemiBoldItalic.ttf");
    }

    private static void RegisterFont(string fontsDirectory, string fileName)
    {
        using var font = File.OpenRead(Path.Combine(fontsDirectory, fileName));
        FontManager.RegisterFont(font);
    }
}

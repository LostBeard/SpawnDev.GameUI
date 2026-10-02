namespace SpawnDev.GameUI.Rendering;

/// <summary>
/// The characters the font atlases (<see cref="SDFFontAtlas"/>, <see cref="FontAtlas"/>) render. A character outside this set
/// draws as a space.
/// </summary>
/// <remarks>
/// 🔴 Until 0.1.0-rc.6 the atlases covered ASCII 32-126 only, so every other character an app wrote - a middle dot
/// separator, an em dash, a degree sign, an accented letter in a file name - silently drew as nothing. SpawnScene's project
/// header "24 photos · 0 scenes · 60 MB" rendered as "24 photos  0 scenes  60 MB" (2026-10-02).
/// </remarks>
public static class GlyphSet
{
    /// <summary>ASCII 32-126, Latin-1 Supplement 160-255, and the punctuation and symbols UI text uses.</summary>
    public static readonly string Characters = Build();

    /// <summary>Characters from General Punctuation and other blocks that UI text commonly needs.</summary>
    public const string UiSymbols =
        "–—"             // en dash, em dash
        + "‘’“”" // curly quotes
        + "•…"           // bullet, ellipsis
        + "‹›"           // single angle quotes
        + "€™"           // euro, trade mark
        + "←↑→↓" // arrows
        + "−"                 // minus sign
        + "✓✕"           // check mark, multiplication x
        + "▲▶▼◀"; // triangles (expanders, play)

    static string Build()
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c <= 126; c++) sb.Append((char)c);
        for (int c = 160; c <= 255; c++) sb.Append((char)c);
        sb.Append(UiSymbols);
        return sb.ToString();
    }
}

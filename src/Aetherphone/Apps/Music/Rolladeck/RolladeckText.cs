namespace Aetherphone.Apps.Music.Rolladeck;

// Maps Unicode mathematical alphanumeric symbols (U+1D400–U+1D7FF) back to plain ASCII
// so ImGui can render DJ names and stream titles that use "bold" or "italic" Unicode fonts.
// Supplementary-plane characters that don't map to ASCII (emoji etc.) are stripped.
internal static class RolladeckText
{
    // Uppercase-start codepoint for each mathematical letter style (26 upper + 26 lower each).
    private static readonly int[] StyleBases =
    [
        0x1D400, // Mathematical Bold
        0x1D434, // Mathematical Italic
        0x1D468, // Mathematical Bold Italic
        0x1D49C, // Mathematical Script
        0x1D4D0, // Mathematical Bold Script
        0x1D504, // Mathematical Fraktur
        0x1D538, // Mathematical Double-Struck
        0x1D56C, // Mathematical Bold Fraktur
        0x1D5A0, // Mathematical Sans-Serif
        0x1D5D4, // Mathematical Sans-Serif Bold
        0x1D608, // Mathematical Sans-Serif Italic
        0x1D63C, // Mathematical Sans-Serif Bold Italic
        0x1D670, // Mathematical Monospace
    ];

    // Base codepoint for each mathematical digit style (10 digits 0–9 each).
    private static readonly int[] DigitBases =
    [
        0x1D7CE, // Bold
        0x1D7D8, // Double-Struck
        0x1D7E2, // Sans-Serif
        0x1D7EC, // Sans-Serif Bold
        0x1D7F6, // Monospace
    ];

    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Fast path: no surrogate pairs means no supplementary-plane chars.
        var hasSurrogate = false;
        foreach (var c in input)
        {
            if (char.IsSurrogate(c)) { hasSurrogate = true; break; }
        }
        if (!hasSurrogate) return input;

        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            var v = rune.Value;
            if (v <= 0xFFFF)
            {
                sb.Append((char)v);
            }
            else
            {
                var mapped = MapSupplementary(v);
                if (mapped != '\0') sb.Append(mapped);
                // else: emoji or other unmapped supplementary char — strip
            }
        }
        return sb.ToString().Trim();
    }

    private static char MapSupplementary(int v)
    {
        foreach (var b in StyleBases)
        {
            var off = v - b;
            if (off >= 0 && off < 26) return (char)('A' + off);
            if (off >= 26 && off < 52) return (char)('a' + off - 26);
        }
        foreach (var b in DigitBases)
        {
            var off = v - b;
            if (off >= 0 && off < 10) return (char)('0' + off);
        }
        return '\0';
    }
}

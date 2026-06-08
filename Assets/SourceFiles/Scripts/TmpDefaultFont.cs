using TMPro;
using UnityEngine;

/// <summary>
/// Assigns a font to runtime-created <see cref="TMP_Text"/> when <see cref="TMP_Settings.defaultFontAsset"/> is null or not yet initialized.
/// </summary>
public static class TmpDefaultFont
{
    const string LiberationSansResourcesPath = "Fonts & Materials/LiberationSans SDF";

    public static string LiberationSansSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s
            .Replace("\u2328", string.Empty)
            .Replace("\u2605", "*")
            .Replace("\u2606", "*")
            .Replace("\u2728\uFE0F", "*")
            .Replace("\u2728", "*");
    }

    public static void AssignIfEmpty(TMP_Text text)
    {
        if (text == null || text.font != null)
            return;

        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>(LiberationSansResourcesPath);
        if (font != null)
            text.font = font;
    }
}

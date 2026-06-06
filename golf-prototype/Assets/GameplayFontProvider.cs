using UnityEngine;
using UnityEngine.UI;

public static class GameplayFontProvider
{
    private const string FredokaResourcePath = "Fonts/FredokaOne-Regular";
    private static Font _cachedFont;

    public static Font GetFont()
    {
        if (_cachedFont == null)
            _cachedFont = Resources.Load<Font>(FredokaResourcePath);

        if (IsUsableFont(_cachedFont))
            return _cachedFont;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static bool IsUsableFont(Font font)
    {
        if (font == null) return false;
        return font.fontNames != null && font.fontNames.Length > 0;
    }

    public static void ApplyToActiveSceneText(bool excludeStartScreen = true)
    {
        Font font = GetFont();
        if (font == null) return;

        Text[] allText = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Text t in allText)
        {
            if (t == null) continue;
            if (excludeStartScreen && IsUnderStartScreenOverlay(t.transform))
                continue;
            t.font = font;
        }
    }

    private static bool IsUnderStartScreenOverlay(Transform tr)
    {
        while (tr != null)
        {
            if (tr.name == "Start Screen Overlay")
                return true;
            tr = tr.parent;
        }

        return false;
    }
}

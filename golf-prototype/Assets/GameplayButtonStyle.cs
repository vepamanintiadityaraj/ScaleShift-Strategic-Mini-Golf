using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared UI button visuals (default / hover sprites) and label color for readable text on the cream panel.
/// Sprites live in Resources/UI/ButtonDefault and ButtonHover.
/// </summary>
public static class GameplayButtonStyle
{
    private const string DefaultResourcePath = "UI/ButtonDefault";
    private const string HoverResourcePath = "UI/ButtonHover";

    /// <summary>Dark brown for strong contrast on pale yellow buttons (~#3a2e2c).</summary>
    public static readonly Color LabelColor = new Color(0.227f, 0.180f, 0.173f, 1f);

    /// <summary>
    /// Extra inset from the bottom of the label rect so text sits higher in the cream panel (sprite frame is heavier below).
    /// </summary>
    public const float LabelVerticalNudgePixels = 8f;

    private static Sprite _defaultSprite;
    private static Sprite _hoverSprite;

    public static Sprite DefaultSprite =>
        _defaultSprite != null ? _defaultSprite : (_defaultSprite = LoadFirstSprite(DefaultResourcePath));

    public static Sprite HoverSprite =>
        _hoverSprite != null ? _hoverSprite : (_hoverSprite = LoadFirstSprite(HoverResourcePath));

    public static bool SpritesAvailable => DefaultSprite != null;

    private static Sprite LoadFirstSprite(string resourcePath)
    {
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        if (all != null && all.Length > 0)
            return all[0];

        return Resources.Load<Sprite>(resourcePath);
    }

    /// <summary>
    /// Applies sliced sprite button styling and sprite-swap hover. Falls back to a flat tinted image if sprites are missing.
    /// </summary>
    public static void Apply(Image image, Button button, Color fallbackImageTint)
    {
        Sprite def = DefaultSprite;
        Sprite hov = HoverSprite;

        if (def != null)
        {
            image.sprite = def;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState ss = button.spriteState;
            ss.highlightedSprite = hov != null ? hov : def;
            ss.pressedSprite = hov != null ? hov : def;
            ss.selectedSprite = def;
            ss.disabledSprite = def;
            button.spriteState = ss;
        }
        else
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fallbackImageTint;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = button.colors;
            cb.normalColor = fallbackImageTint;
            cb.highlightedColor = fallbackImageTint * 1.08f;
            cb.pressedColor = fallbackImageTint * 0.92f;
            cb.selectedColor = fallbackImageTint;
            cb.disabledColor = new Color(fallbackImageTint.r, fallbackImageTint.g, fallbackImageTint.b, 0.5f);
            button.colors = cb;
        }
    }

    /// <param name="fullStretchRect">When the label uses anchor stretch on the button, pass its RectTransform to nudge text upward.</param>
    /// <param name="verticalNudgePx">Bottom inset in pixels; default uses <see cref="LabelVerticalNudgePixels"/>.</param>
    public static void ApplyLabel(Text text, RectTransform fullStretchRect = null, float verticalNudgePx = -1f)
    {
        if (text != null)
            text.color = LabelColor;

        float nudge = verticalNudgePx >= 0f ? verticalNudgePx : LabelVerticalNudgePixels;
        if (fullStretchRect != null && nudge > 0f)
        {
            Vector2 min = fullStretchRect.offsetMin;
            min.y = nudge;
            fullStretchRect.offsetMin = min;
        }
    }
}

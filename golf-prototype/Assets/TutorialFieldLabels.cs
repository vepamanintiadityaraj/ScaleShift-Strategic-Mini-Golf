using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// World-space callouts for the Tutorial scene. Hole-size legend appears only on the rim-tip overlay
/// (after sinking the ball or pressing Skip).
/// </summary>
[DisallowMultipleComponent]
public class TutorialFieldLabels : MonoBehaviour
{
    [Header("Find by name if references are empty")]
    [SerializeField] private string blueZoneObjectName = "Blue Zone Center-Left (1)";
    [SerializeField] private string redZoneObjectName = "Red Zone Center-Right (3)";
    [SerializeField] private string lethalSpikesObjectName = "Top-Left Spikes";
    [SerializeField] private string finalHoleObjectName = "Final Hole";
    [SerializeField] private string powerUpObjectName = "Triangle (1)";

    [Tooltip("If set, labels this portal. If empty, uses the CapsulePortal with the highest Y (top capsule).")]
    [SerializeField] private string topPortalCapsuleObjectName = "";

    [Header("World offset from target transform (XY)")]
    [SerializeField] private Vector2 blueLabelOffset = new Vector2(-5.2f, -2.35f);
    [SerializeField] private Vector2 redLabelOffset = new Vector2(0f, 6.0f);
    [SerializeField] private Vector2 lethalLabelOffset = new Vector2(8.5f, -1.8f);
    [SerializeField] private Vector2 holeLabelWorldOffset = new Vector2(0.5f, -2.0f);
    [SerializeField] private Vector2 powerUpLabelOffset = new Vector2(0f, 2.5f);
    [SerializeField] private Vector2 topPortalLabelOffset = new Vector2(-3.8f, 0.35f);
    [SerializeField] private Vector2 topPortalLabelSize = new Vector2(300f, 70f);
    [SerializeField] private int topPortalLabelFontSize = 13;

    [Tooltip("Arrow + callout; keep this to a short 5-6 word phrase.")]
    [SerializeField] private string topPortalLabelText = "Portal teleports ball\nto glowing portal \u2192";

    [Header("Layout")]
    [SerializeField] private int fontSize = 20;
    [SerializeField] private Vector2 labelSize = new Vector2(340f, 140f);
    [SerializeField] private Vector2 holeLabelSize = new Vector2(280f, 100f);
    [SerializeField] private int holeLabelFontSize = 15;

    [Header("Transparency (multiplies all label colors)")]
    [SerializeField, Range(0.05f, 1f)] private float textAlpha = 0.72f;
    [SerializeField, Range(0f, 1f)] private float outlineAlpha = 0.5f;

    [Header("Rim tip overlay (after goal or Skip)")]
    [SerializeField] private string rimTipTitle =
        "Keep an eye on the hole's rim color in each level it shows which ball size fits.";
    [SerializeField] private int rimTipTitleFontSize = 24;
    [SerializeField] private int rimOverlayLegendFontSize = 18;
    [SerializeField] private float rimOverlayLegendLineSpacing = 1.25f;
    [SerializeField] private float rimOverlayLegendAnchorY = 0.46f;
    [SerializeField] private Vector2 rimOverlayLegendSize = new Vector2(520f, 230f);
    [SerializeField] private string rimOverlayAdvantagesHeading = "Advantages along the game";
    [SerializeField] private string rimOverlayPowerUpsLine = "look out for power ups";
    [SerializeField] private string rimTipContinueLabel = "Continue";

    

    private readonly List<GameObject> _spawnedLabelRoots = new List<GameObject>();
    private GameObject _rimTipOverlayRoot;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "Tutorial") return;
        StartCoroutine(BuildLabelsNextFrame());
    }

    private IEnumerator BuildLabelsNextFrame()
    {
        yield return null;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) yield break;

        Camera worldCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? Camera.main
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        if (worldCam == null) yield break;

        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCam;

        Font font = GameplayFontProvider.GetFont();
        if (font == null) yield break;

        Transform blue = ResolveTarget(blueZoneObjectName);
        Transform red = ResolveTarget(redZoneObjectName);
        Transform lethal = ResolveTarget(lethalSpikesObjectName);
        Transform powerUp = ResolveTarget(powerUpObjectName);

        if (blue != null)
            CreateCallout(canvas, worldCam, uiCam, font, "TutorialLabel_Blue", blue.position + (Vector3)blueLabelOffset,
                "Enlarge zone  →\nBall <b>enlarges</b>");

        if (red != null)
            CreateCallout(canvas, worldCam, uiCam, font, "TutorialLabel_Red", red.position + (Vector3)redLabelOffset,
                "  Shrink zone\nBall <b>shrinks</b> \n ↓");

        if (lethal != null)
            CreateCallout(canvas, worldCam, uiCam, font, "TutorialLabel_Lethal", lethal.position + (Vector3)lethalLabelOffset,
                "Lethal walls\n<size=17>Avoid lethal walls!</size>\n<color=#ff3333><b>!! death</b></color>");
        
        if (powerUp != null)
            {
                CreateCallout(
                    canvas,
                    worldCam,
                    uiCam,
                    font,
                    "TutorialLabel_PowerUp",
                    powerUp.position + (Vector3)powerUpLabelOffset,
                    "Power-Up\n<size=17>Use it to block your opponent</size>"
                );
            }

        Transform topPortal = ResolveTopPortalTransform();
        if (topPortal != null && !string.IsNullOrEmpty(topPortalLabelText))
        {
            CreateCallout(
                canvas,
                worldCam,
                uiCam,
                font,
                "TutorialLabel_TopPortal",
                topPortal.position + (Vector3)topPortalLabelOffset,
                topPortalLabelText,
                topPortalLabelSize,
                topPortalLabelFontSize);
        }

        Transform hole = ResolveTarget(finalHoleObjectName);
        if (hole != null)
        {
            CreateCallout(canvas, worldCam, uiCam, font, "TutorialLabel_Hole", hole.position + (Vector3)holeLabelWorldOffset,
                "↑ \n Ball size should be\nthe same as hole size",
                holeLabelSize,
                holeLabelFontSize);
        }
    }

    /// <summary>Full-screen overlay: rim tip + hole-size legend, then <paramref name="onContinue"/>.</summary>
    public void ShowRimColorTipOverlay(Action onContinue)
    {
        if (SceneManager.GetActiveScene().name != "Tutorial" || onContinue == null) return;

        HideSpawnedFieldLabels();

        if (_rimTipOverlayRoot != null)
            Destroy(_rimTipOverlayRoot);

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            onContinue.Invoke();
            return;
        }

        Font font = GameplayFontProvider.GetFont();
        if (font == null)
        {
            onContinue.Invoke();
            return;
        }

        _rimTipOverlayRoot = new GameObject("Tutorial Rim Tip Overlay");
        _rimTipOverlayRoot.transform.SetParent(canvas.transform, false);
        _rimTipOverlayRoot.transform.SetAsLastSibling();

        RectTransform panelRt = _rimTipOverlayRoot.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image panelBg = _rimTipOverlayRoot.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.1f, 0.94f);
        panelBg.raycastTarget = true;

        GameObject titleGo = new GameObject("Rim Tip Title");
        titleGo.transform.SetParent(_rimTipOverlayRoot.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.08f, 0.58f);
        titleRt.anchorMax = new Vector2(0.92f, 0.88f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        Text titleText = titleGo.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = rimTipTitleFontSize;
        titleText.supportRichText = false;
        titleText.text = rimTipTitle;
        titleText.color = new Color(1f, 1f, 1f, 0.95f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.raycastTarget = false;
        Outline titleOutline = titleGo.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        titleOutline.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject legendGo = new GameObject("Rim Tip Legend");
        legendGo.transform.SetParent(_rimTipOverlayRoot.transform, false);
        RectTransform legendRt = legendGo.AddComponent<RectTransform>();
        legendRt.anchorMin = new Vector2(0.5f, rimOverlayLegendAnchorY);
        legendRt.anchorMax = new Vector2(0.5f, rimOverlayLegendAnchorY);
        legendRt.pivot = new Vector2(0.5f, 0.5f);
        legendRt.anchoredPosition = Vector2.zero;
        legendRt.sizeDelta = rimOverlayLegendSize;
        Text legendText = legendGo.AddComponent<Text>();
        legendText.font = font;
        legendText.fontSize = rimOverlayLegendFontSize;
        legendText.lineSpacing = rimOverlayLegendLineSpacing;
        legendText.supportRichText = true;
        legendText.text = BuildLegendRichText(rimOverlayPowerUpsLine, rimOverlayAdvantagesHeading);
        legendText.color = new Color(1f, 1f, 1f, 0.88f);
        legendText.alignment = TextAnchor.MiddleCenter;
        legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
        legendText.verticalOverflow = VerticalWrapMode.Overflow;
        legendText.raycastTarget = false;
        Outline legendOutline = legendGo.AddComponent<Outline>();
        legendOutline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        legendOutline.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject btnGo = new GameObject("Rim Tip Continue");
        btnGo.transform.SetParent(_rimTipOverlayRoot.transform, false);
        RectTransform btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.12f);
        btnRt.anchorMax = new Vector2(0.5f, 0.12f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = Vector2.zero;
        btnRt.sizeDelta = new Vector2(220f, 52f);
        Image btnImg = btnGo.AddComponent<Image>();
        Button btn = btnGo.AddComponent<Button>();
        GameplayButtonStyle.Apply(btnImg, btn, new Color(0.22f, 0.55f, 0.38f, 1f));
        btn.onClick.AddListener(() =>
        {
            if (_rimTipOverlayRoot != null)
            {
                Destroy(_rimTipOverlayRoot);
                _rimTipOverlayRoot = null;
            }
            onContinue.Invoke();
        });

        GameObject btnTxtGo = new GameObject("Text");
        btnTxtGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTxtRt = btnTxtGo.AddComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = Vector2.zero;
        btnTxtRt.offsetMax = Vector2.zero;
        Text btnTxt = btnTxtGo.AddComponent<Text>();
        btnTxt.font = font;
        btnTxt.fontSize = 22;
        btnTxt.text = rimTipContinueLabel;
        GameplayButtonStyle.ApplyLabel(btnTxt, btnTxtRt);
        btnTxt.alignment = TextAnchor.MiddleCenter;
    }

    private string BuildLegendRichText(string powerUpsCaption, string advantagesHeading)
    {
        return
            "<size=24><color=#FF5C9E>o</color></size>  small ball size\n" +
            "<size=24><color=#F5D547>o</color></size>  normal ball size\n" +
            "<size=24><color=#5599FF>o</color></size>  large ball size\n" +
            "\n<b>" + advantagesHeading + "</b>\n" +
            "<size=18><color=#FF5C9E>^</color></size>  " + powerUpsCaption;
    }

    private void HideSpawnedFieldLabels()
    {
        for (int i = _spawnedLabelRoots.Count - 1; i >= 0; i--)
        {
            if (_spawnedLabelRoots[i] != null)
                _spawnedLabelRoots[i].SetActive(false);
        }
    }

    private static Transform ResolveTarget(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
    }

    /// <summary>Top portal for the tutorial callout: explicit name, else highest <see cref="CapsulePortal"/> by Y.</summary>
    private Transform ResolveTopPortalTransform()
    {
        if (!string.IsNullOrWhiteSpace(topPortalCapsuleObjectName))
        {
            Transform byName = ResolveTarget(topPortalCapsuleObjectName.Trim());
            if (byName != null)
                return byName;
        }

        CapsulePortal[] portals = FindObjectsByType<CapsulePortal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (portals == null || portals.Length == 0)
            return null;

        CapsulePortal top = portals[0];
        for (int i = 1; i < portals.Length; i++)
        {
            if (portals[i].transform.position.y > top.transform.position.y)
                top = portals[i];
        }

        return top.transform;
    }

    private void CreateCallout(Canvas canvas, Camera worldCam, Camera uiCam, Font labelFont, string objectName, Vector3 worldPos, string richText, Vector2? sizeOverride = null, int? textFontSize = null)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPoint,
            uiCam,
            out Vector2 localPoint);
        CreateCalloutAtCanvasLocal(canvas, uiCam, labelFont, objectName, localPoint, sizeOverride ?? labelSize, richText, textFontSize);
    }

    private void CreateCalloutAtCanvasLocal(Canvas canvas, Camera uiCam, Font labelFont, string objectName, Vector2 anchoredPosition, Vector2 size, string richText, int? textFontSize = null)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetSiblingIndex(0);
        _spawnedLabelRoots.Add(root);

        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        Text text = root.AddComponent<Text>();
        text.font = labelFont;
        text.fontSize = textFontSize ?? fontSize;
        text.supportRichText = true;
        text.text = richText;
        text.color = new Color(1f, 1f, 1f, textAlpha);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, outlineAlpha);
        outline.effectDistance = new Vector2(1.6f, -1.6f);
    }

    public void ShowTryBreakItHint(Vector3 worldPos)
{
    if (SceneManager.GetActiveScene().name != "Tutorial") return;

    Canvas canvas = FindFirstObjectByType<Canvas>();
    if (canvas == null) return;

    Camera worldCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
        ? Camera.main
        : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

    if (worldCam == null) return;

    Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCam;

    Font font = GameplayFontProvider.GetFont();
    if (font == null) return;

    CreateCallout(
        canvas,
        worldCam,
        uiCam,
        font,
        "TutorialLabel_TryBreakIt",
        worldPos + new Vector3(0f, 1.8f, 0f),
        "Try to break it",
        new Vector2(260f, 70f),
        18
    );
}

    private void OnDestroy()
    {
        if (_rimTipOverlayRoot != null)
            Destroy(_rimTipOverlayRoot);
    }

    public void HidePowerUpLabel()
{
    GameObject obj = GameObject.Find("TutorialLabel_PowerUp");
    if (obj != null)
        Destroy(obj);
}

public void HideTryBreakItHint()
{
    GameObject obj = GameObject.Find("TutorialLabel_TryBreakIt");
    if (obj != null)
        Destroy(obj);
}
}

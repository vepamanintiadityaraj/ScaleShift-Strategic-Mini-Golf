using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: after collecting the triangle, the player must click to place a static wall
/// before their turn can end. In two-player mode, the first confirmed wall shows an overlay once
/// per session; later walls skip it.
/// </summary>
public class WallPlacementPowerUp : MonoBehaviour
{
    public static WallPlacementPowerUp Instance { get; private set; }
    private const string PlacedWallSpriteResourcePath = "PlacedWallSprite";
    private const float placedWallVisualWidth = 0.25f;
    private const float placedWallVisualHeight = 2f;
    private static Sprite _placedWallSprite;

    [SerializeField] private float wallWidth = 0.7f;
    [SerializeField] private float wallHeight = 2.2f;
    [SerializeField] private Color wallColor = new Color(0.35f, 0.72f, 0.95f, 1f);
    [SerializeField] private int wallSortingOrder = 50;

    [Header("After placement (two-player)")]
    [SerializeField] private string postPlaceBreakHintText = "Hint: Hit the wall to break it.";
    [SerializeField] private string postPlaceBreakHintSubtext = "Next player's turn after you continue.";
    [SerializeField] private string postPlaceBreakHintButtonLabel = "Continue";

    private enum PowerUpState { Inactive, AwaitPlace, Editing, PostPlaceBreakHint }
    private PowerUpState state = PowerUpState.Inactive;
    private int targetPlayer;
    private GameObject hintRoot;
    private Text hintText;
    private GameObject editingWall;
    private bool dragging;
    private Vector2 dragOffset;
    private SendToGoogle sender;
    private GameObject breakHintOverlayRoot;

    /// <summary>At most one break-hint overlay per run — first two-player wall placement only.</summary>
    private static bool postPlaceBreakHintShownThisSession;

    [SerializeField] private float rotateSpeedDegreesPerSecond = 140f;

    private void Update()
    {
        if (state != PowerUpState.Editing || editingWall == null) return;

        float rot = GetWallRotationInput();
        if (Mathf.Abs(rot) <= 0.001f) return;

        editingWall.transform.Rotate(0f, 0f, rot);
        if (WallOverlapsOpponentBall(editingWall.transform.position, editingWall.transform.eulerAngles.z))
            editingWall.transform.Rotate(0f, 0f, -rot);
    }

    /// <summary>Q/E and mouse wheel; works in Update so rotation still applies when UI blocks <see cref="HandleInput"/>.</summary>
    private float GetWallRotationInput()
    {
        float step = rotateSpeedDegreesPerSecond * Time.deltaTime;
        float rot = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed) rot += step;
            if (Keyboard.current.eKey.isPressed) rot -= step;
        }
        else
        {
            if (Input.GetKey(KeyCode.Q)) rot += step;
            if (Input.GetKey(KeyCode.E)) rot -= step;
        }

        if (Mouse.current != null)
            rot += -Mouse.current.scroll.ReadValue().y * 12f;
        else
            rot += -Input.mouseScrollDelta.y * 12f;

        return rot;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        sender = FindFirstObjectByType<SendToGoogle>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (hintRoot != null)
            Destroy(hintRoot);
        DestroyBreakHintOverlay();
    }

    /// <summary>
    /// While true, TurnManager will not advance the turn for this player after their ball stops.
    /// </summary>
    public static bool BlocksTurnSwitchFor(int playerIndex)
    {
        return Instance != null && Instance.state != PowerUpState.Inactive && Instance.targetPlayer == playerIndex;
    }

    public bool IsPendingFor(int playerIndex)
    {
        if (state == PowerUpState.Inactive || state == PowerUpState.PostPlaceBreakHint) return false;
        return targetPlayer == playerIndex;
    }

    public void Begin(int playerIndex)
    {
        state = PowerUpState.AwaitPlace;
        targetPlayer = playerIndex;
        ShowHint();
    }

    private void Clear()
    {
        state = PowerUpState.Inactive;
        targetPlayer = 0;
        editingWall = null;
        dragging = false;
        HideHint();
        DestroyBreakHintOverlay();
    }

    /// <summary>
    /// Cancels the current power-up flow (used when a player wins/eliminates/game ends).
    /// If a wall is currently being edited (placed but not confirmed), it gets destroyed.
    /// </summary>
    public void CancelPowerUp()
    {
        if (state == PowerUpState.Inactive)
            return;

        if (editingWall != null)
            Destroy(editingWall);

        Clear();
    }

    /// <summary>
    /// Called by BallController while it's the owner's turn and the power-up is pending.
    /// </summary>
    public void HandleInput()
    {
        if (state == PowerUpState.Inactive) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = Input.mousePosition;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        Vector2 pos = new Vector2(w.x, w.y);

        if (state == PowerUpState.AwaitPlace)
        {
            if (Input.GetMouseButtonDown(0))
            {
                SpawnWall(pos);
                state = PowerUpState.Editing;
                SetHintText("Drag to move • Q / E or scroll to rotate • Right-click / Enter to confirm");
            }
            return;
        }

        if (state == PowerUpState.Editing && editingWall != null)
        {
            if (Input.GetMouseButtonDown(0))
            {
                dragging = true;
                dragOffset = (Vector2)editingWall.transform.position - pos;
            }
            if (Input.GetMouseButton(0) && dragging)
            {
                Vector2 proposed = pos + dragOffset;
                float ang = editingWall.transform.eulerAngles.z;
                if (!WallOverlapsOpponentBall(proposed, ang))
                    editingWall.transform.position = new Vector3(proposed.x, proposed.y, 0f);
                else
                    dragOffset = (Vector2)editingWall.transform.position - pos;
            }
            if (Input.GetMouseButtonUp(0))
                dragging = false;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (WallOverlapsOpponentBall(editingWall.transform.position, editingWall.transform.eulerAngles.z))
                {
                    SetHintText("Move the wall off the other player's ball before confirming");
                    return;
                }
                BoxCollider2D col = editingWall.GetComponent<BoxCollider2D>();
                if (col != null) col.enabled = true;

                Vector3 placedWallPos = editingWall.transform.position;

                if (sender != null)
                {
                    sender.Send(
                        "PowerUpUsage",
                        targetPlayer.ToString(),
                        "N/A",
                        "N/A",
                        "N/A",
                        "N/A",
                        "PlacedWall",
                        "N/A",
                        "Used"
                    );
                }


                TutorialFieldLabels labels = FindFirstObjectByType<TutorialFieldLabels>();
                if (labels != null)
                {
                    labels.ShowTryBreakItHint(placedWallPos);
                }

                editingWall = null;
                dragging = false;
                HideHint();

                bool isTutorial = SceneManager.GetActiveScene().name == "Tutorial";

                if (isTutorial)
                {
                    Clear();
                }
                else if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
                {
                    if (!postPlaceBreakHintShownThisSession)
                    {
                        postPlaceBreakHintShownThisSession = true;
                        state = PowerUpState.PostPlaceBreakHint;
                        CreatePostPlaceBreakHintOverlay();
                    }
                    else
                    {
                        Clear();
                    }
                }
                else
                {
                    Clear();
                }
            }
        }
    }

    private void CreatePostPlaceBreakHintOverlay()
    {
        DestroyBreakHintOverlay();

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            DismissPostPlaceBreakHint();
            return;
        }

        Font font = GameplayFontProvider.GetFont();
        if (font == null)
        {
            DismissPostPlaceBreakHint();
            return;
        }

        breakHintOverlayRoot = new GameObject("WallBreakHintOverlay");
        breakHintOverlayRoot.transform.SetParent(canvas.transform, false);
        breakHintOverlayRoot.transform.SetAsLastSibling();

        RectTransform panelRt = breakHintOverlayRoot.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image panelBg = breakHintOverlayRoot.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        panelBg.raycastTarget = true;

        GameObject textGo = new GameObject("HintText");
        textGo.transform.SetParent(breakHintOverlayRoot.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.08f, 0.42f);
        textRt.anchorMax = new Vector2(0.92f, 0.72f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        Text mainText = textGo.AddComponent<Text>();
        mainText.font = font;
        mainText.fontSize = 26;
        mainText.supportRichText = false;
        mainText.text = postPlaceBreakHintText;
        mainText.color = new Color(1f, 1f, 1f, 0.96f);
        mainText.alignment = TextAnchor.MiddleCenter;
        mainText.horizontalOverflow = HorizontalWrapMode.Wrap;
        mainText.verticalOverflow = VerticalWrapMode.Overflow;
        mainText.raycastTarget = false;
        Outline o1 = textGo.AddComponent<Outline>();
        o1.effectColor = new Color(0f, 0f, 0f, 0.55f);
        o1.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject subGo = new GameObject("SubText");
        subGo.transform.SetParent(breakHintOverlayRoot.transform, false);
        RectTransform subRt = subGo.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.08f, 0.30f);
        subRt.anchorMax = new Vector2(0.92f, 0.42f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        Text subText = subGo.AddComponent<Text>();
        subText.font = font;
        subText.fontSize = 17;
        subText.text = postPlaceBreakHintSubtext;
        subText.color = new Color(0.9f, 0.9f, 0.92f, 0.75f);
        subText.alignment = TextAnchor.MiddleCenter;
        subText.horizontalOverflow = HorizontalWrapMode.Wrap;
        subText.verticalOverflow = VerticalWrapMode.Overflow;
        subText.raycastTarget = false;

        GameObject btnGo = new GameObject("Continue");
        btnGo.transform.SetParent(breakHintOverlayRoot.transform, false);
        RectTransform btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.14f);
        btnRt.anchorMax = new Vector2(0.5f, 0.14f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = Vector2.zero;
        btnRt.sizeDelta = new Vector2(220f, 50f);
        Image btnImg = btnGo.AddComponent<Image>();
        Button btn = btnGo.AddComponent<Button>();
        GameplayButtonStyle.Apply(btnImg, btn, new Color(0.22f, 0.52f, 0.38f, 1f));
        btn.onClick.AddListener(DismissPostPlaceBreakHint);

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
        btnTxt.text = postPlaceBreakHintButtonLabel;
        GameplayButtonStyle.ApplyLabel(btnTxt, btnTxtRt);
        btnTxt.alignment = TextAnchor.MiddleCenter;
    }

    private void DismissPostPlaceBreakHint()
    {
        state = PowerUpState.Inactive;
        targetPlayer = 0;
        DestroyBreakHintOverlay();
    }

    private void DestroyBreakHintOverlay()
    {
        if (breakHintOverlayRoot != null)
        {
            Destroy(breakHintOverlayRoot);
            breakHintOverlayRoot = null;
        }
    }

    private void SpawnWall(Vector2 position)
    {
        GameObject wall = new GameObject("PlacedWall");
        wall.transform.position = new Vector3(position.x, position.y, 0f);
        editingWall = wall;

        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = GetPlacedWallSprite();
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(placedWallVisualWidth, placedWallVisualHeight);
        sr.sortingOrder = wallSortingOrder;

        BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
        box.size = new Vector2(wallWidth, wallHeight);
        box.enabled = false;

        wall.AddComponent<BreakablePlacedWall>();
    }

    private static Sprite GetPlacedWallSprite()
    {
        if (_placedWallSprite == null)
            _placedWallSprite = Resources.Load<Sprite>(PlacedWallSpriteResourcePath);

        return _placedWallSprite != null ? _placedWallSprite : CreateFallbackWallSprite();
    }

    /// <summary>
    /// True if a wall at <paramref name="center"/> with <paramref name="angleDegrees"/> would intersect the opponent's ball
    /// (real overlap), not merely sit close or tangent.
    /// </summary>
    private bool WallOverlapsOpponentBall(Vector2 center, float angleDegrees)
    {
        if (TurnManager.Instance == null || !TurnManager.Instance.IsTwoPlayerMode)
            return false;

        int opponentIndex = targetPlayer == 1 ? 2 : 1;
        BallController opponent = TurnManager.Instance.GetPlayerBall(opponentIndex);
        if (opponent == null || opponent.IsEliminated)
            return false;

        Collider2D ballCol = opponent.BallCollider;
        if (ballCol == null)
            return false;

        Bounds b = ballCol.bounds;
        Vector2 circleCenter = b.center;
        float radius = Mathf.Max(b.extents.x, b.extents.y);

        Vector2 half = new Vector2(wallWidth * 0.5f, wallHeight * 0.5f);
        return CirclePenetratesOrientedRect(circleCenter, radius, center, half, angleDegrees);
    }

    /// <summary>
    /// Penetration beyond a small skin so grazing / edge contact still allows placement; blocks when the wall covers the ball.
    /// </summary>
    private static bool CirclePenetratesOrientedRect(
        Vector2 circleCenter,
        float circleRadius,
        Vector2 rectCenter,
        Vector2 rectHalfExtents,
        float rectAngleDeg)
    {
        float rad = rectAngleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(-rad);
        float sin = Mathf.Sin(-rad);
        Vector2 d = circleCenter - rectCenter;
        float lx = d.x * cos - d.y * sin;
        float ly = d.x * sin + d.y * cos;
        float qx = Mathf.Clamp(lx, -rectHalfExtents.x, rectHalfExtents.x);
        float qy = Mathf.Clamp(ly, -rectHalfExtents.y, rectHalfExtents.y);
        float dx = lx - qx;
        float dy = ly - qy;
        float distSq = dx * dx + dy * dy;

        float limit = circleRadius * 0.4f;
        return distSq < limit * limit;
    }

    private static Sprite CreateFallbackWallSprite()
    {
        int w = 8;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        Color c = new Color(0.35f, 0.72f, 0.95f, 1f);
        for (int i = 0; i < px.Length; i++)
            px[i] = c;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    private void ShowHint()
    {
        HideHint();
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        hintRoot = new GameObject("WallPlacementHint");
        hintRoot.transform.SetParent(canvas.transform, false);
        RectTransform rt = hintRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 20f);
        rt.sizeDelta = new Vector2(560f, 56f);

        Image bg = hintRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("HintText");
        textObj.transform.SetParent(hintRoot.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 5f);
        textRt.offsetMax = new Vector2(-10f, -5f);

        hintText = textObj.AddComponent<Text>();
        hintText.font = GameplayFontProvider.GetFont();
        hintText.fontSize = 16;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = Color.white;
        hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;
        hintText.raycastTarget = false;
        hintText.text = "Click to place your wall • After placing: Q / E or scroll to rotate, then confirm";
        hintRoot.transform.SetAsLastSibling();
    }

    private void SetHintText(string text)
    {
        if (hintText != null)
            hintText.text = text;
    }

    private void HideHint()
    {
        if (hintRoot != null)
        {
            Destroy(hintRoot);
            hintRoot = null;
            hintText = null;
        }
    }
}

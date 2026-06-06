using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class HoleController : MonoBehaviour
{
    [Header("Hole Settings")]
    [SerializeField] private BallSize requiredSize = BallSize.Normal;
    [SerializeField] private float rejectForce = 5f;
    [SerializeField] private float sinkDuration = 1f;
    [SerializeField] private float sinkRotationSpeed = 360f;

    // Hole scales are NOT stored here — they are read at runtime from
    // BallSizeController.GetScaleFor(requiredSize) so that the ball and the
    // hole can never get out of sync. This also means designers only have one
    // place to tune sizing (on the ball).

    [Header("Size Ring Visual")]
    // Ring is a proportional multiplier of the hole scale (not additive), so a
    // tiny hole gets a tiny ring.
    [SerializeField] private float ringScaleMultiplier = 1.2f;
    [SerializeField] private float ringPulseSpeed = 1.5f;
    [SerializeField] private float ringPulseAmount = 0.08f;

    private bool ballIsSinking = false;

    private GameUI gameUI;
    private SendToGoogle analytics;
    private GameObject sizeRingObject;
    private SpriteRenderer sizeRingRenderer;
    private float ringBaseScale;
    private float holeScale;

    private static readonly Color SmallRingColor = new Color(1f, 0.25f, 0.2f, 0.85f);
    private static readonly Color NormalRingColor = new Color(0.92f, 0.75f, 0.2f, 0.85f);
    private static readonly Color LargeRingColor = new Color(0.2f, 0.55f, 1f, 0.85f);

    private void Start()
    {
        gameUI = FindFirstObjectByType<GameUI>();
        analytics = FindObjectOfType<SendToGoogle>();
        ApplyLevelRequiredSize();
        RebuildHoleVisual();
        CreateSizeRing();
        StartCoroutine(ForceHoleSpriteMatchesBall());
    }

    private void Update()
    {
        if (sizeRingObject != null && sizeRingObject.activeSelf)
        {
            float pulse = 1f + Mathf.Sin(Time.time * ringPulseSpeed) * ringPulseAmount;
            sizeRingObject.transform.localScale = Vector3.one * ringBaseScale * pulse;
        }
    }

    private void ApplyLevelRequiredSize()
    {
        string scene = SceneManager.GetActiveScene().name;
        switch (scene)
        {
            case "Level1": requiredSize = BallSize.Normal; break;
            case "Level2": requiredSize = BallSize.Small;  break;
            case "Level3": requiredSize = BallSize.Large;  break;
        }
    }

    private void RebuildHoleVisual()
    {
        // Source of truth: the ball. The hole uses the exact same scale the
        // ball will have when it reaches the required size. Fallbacks only
        // apply if no BallSizeController exists yet (shouldn't happen once
        // the scene is set up, but the defaults keep the hole visible).
        BallSizeController anyBall = FindFirstObjectByType<BallSizeController>();
        holeScale = anyBall != null
            ? anyBall.GetScaleFor(requiredSize)
            : FallbackScaleFor(requiredSize);

        transform.localScale = Vector3.one * holeScale;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Use the ball's own sprite (tinted black) for the hole. Since both
            // objects now share the same sprite AND the same localScale when
            // the ball is at the required size, the visible diameters are
            // guaranteed to be pixel-identical, no matter what padding the
            // ball sprite has. Fall back to a generated circle only if no ball
            // exists yet (first frame); the coroutine below will retry.
            Sprite ballSprite = FindBallSprite();
            sr.sprite = ballSprite != null ? ballSprite : CreateFilledCircleSprite();
            sr.color = Color.black;
            sr.sortingOrder = 4;
        }
    }

    private static float FallbackScaleFor(BallSize size)
    {
        switch (size)
        {
            case BallSize.Small: return 0.5f;
            case BallSize.Large: return 1.5f;
            default:             return 1.0f;
        }
    }

    private Sprite FindBallSprite()
    {
        BallSizeController anyBall = FindFirstObjectByType<BallSizeController>();
        if (anyBall == null) return null;
        SpriteRenderer ballSr = anyBall.GetComponent<SpriteRenderer>();
        if (ballSr == null) ballSr = anyBall.GetComponentInChildren<SpriteRenderer>();
        return ballSr != null ? ballSr.sprite : null;
    }

    private IEnumerator ForceHoleSpriteMatchesBall()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        for (int i = 0; i < 10; i++)
        {
            Sprite bs = FindBallSprite();
            if (bs != null)
            {
                sr.sprite = bs;
                sr.color = Color.black;
                sr.sortingOrder = 4;
                yield break;
            }
            yield return null;
        }
    }

    private void CreateSizeRing()
    {
        ringBaseScale = holeScale * ringScaleMultiplier;

        sizeRingObject = new GameObject("HoleSizeRing");
        sizeRingObject.transform.position = transform.position;

        sizeRingRenderer = sizeRingObject.AddComponent<SpriteRenderer>();
        sizeRingRenderer.sprite = CreateRingSprite();
        sizeRingRenderer.sortingOrder = 3;

        switch (requiredSize)
        {
            case BallSize.Small:  sizeRingRenderer.color = SmallRingColor;  break;
            case BallSize.Large:  sizeRingRenderer.color = LargeRingColor;  break;
            default:              sizeRingRenderer.color = NormalRingColor;  break;
        }

        sizeRingObject.transform.localScale = Vector3.one * ringBaseScale;
    }

    private Sprite CreateFilledCircleSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float edge = Mathf.Clamp01((radius - dist) * 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, edge);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateRingSprite()
    {
        const int size = 128;
        const float innerFrac = 0.55f;
        const float outerFrac = 0.95f;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float center = size * 0.5f;
        float innerR = center * innerFrac;
        float outerR = center * outerFrac;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 0f;
                if (dist > innerR && dist < outerR)
                {
                    float mid = (innerR + outerR) * 0.5f;
                    float halfW = (outerR - innerR) * 0.5f;
                    float t = 1f - Mathf.Abs(dist - mid) / halfW;
                    alpha = Mathf.Clamp01(t * t);
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (ballIsSinking)
            return;

        BallSizeController ballSize = collision.GetComponent<BallSizeController>();
        if (ballSize == null)
            return;

        BallController ballController = collision.GetComponent<BallController>();
        if (ballController == null)
            return;

        if (ballSize.CurrentSize == requiredSize)
        {
            ballIsSinking = true;

            if (sizeRingObject != null)
                sizeRingObject.SetActive(false);

            if (analytics != null)
            {
                Debug.Log($"Analytics: BallSunk - Player {ballController.PlayerIndex}, Size {ballSize.CurrentSize}");
                analytics.Send("BallSunk", ballController.PlayerIndex.ToString(), ballSize.CurrentSize.ToString(), ballController.BallsRemaining.ToString(), "Sunk");
            }

            StartCoroutine(SinkBallAnimation(collision.gameObject, ballController));
        }
        else
        {
            Vector2 rejectDirection = (collision.transform.position - transform.position).normalized;
            Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.AddForce(rejectDirection * rejectForce, ForceMode2D.Impulse);
            }

            ShowRejectionHint(ballSize.CurrentSize);

            if (analytics != null)
            {
                Debug.Log($"Analytics: BallRejected - Player {ballController.PlayerIndex}, CurrentSize {ballSize.CurrentSize}, Required {requiredSize}");
                analytics.Send("BallRejected", ballController.PlayerIndex.ToString(), ballSize.CurrentSize.ToString(), ballController.BallsRemaining.ToString(), "RejectedSizeMismatch");
            }
        }
    }

    private void ShowRejectionHint(BallSize currentBallSize)
    {
        string hint;
        if (currentBallSize > requiredSize)
            hint = "Too big!\nHit a red wall to shrink";
        else
            hint = "Too small!\nHit a blue wall to grow";

        StartCoroutine(FloatingHint(hint));
    }

    private IEnumerator FloatingHint(string text)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) yield break;

        GameObject root = new GameObject("RejectHint");
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = root.AddComponent<RectTransform>();
        Camera cam = Camera.main;
        Vector3 worldPos = transform.position + Vector3.up * 1.5f;
        if (cam != null)
        {
            Vector2 screen = cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                out Vector2 local);
            rootRt.anchoredPosition = local;
        }
        rootRt.sizeDelta = new Vector2(200f, 70f);

        CanvasGroup cg = root.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        GameObject bubbleObj = new GameObject("Bubble");
        bubbleObj.transform.SetParent(root.transform, false);
        RectTransform bubbleRt = bubbleObj.AddComponent<RectTransform>();
        bubbleRt.anchorMin = Vector2.zero;
        bubbleRt.anchorMax = Vector2.one;
        bubbleRt.offsetMin = Vector2.zero;
        bubbleRt.offsetMax = Vector2.zero;
        UnityEngine.UI.Image bubbleBg = bubbleObj.AddComponent<UnityEngine.UI.Image>();
        bubbleBg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        bubbleBg.raycastTarget = false;

        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(root.transform, false);
        RectTransform arrowRt = arrowObj.AddComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(0.5f, 0f);
        arrowRt.anchorMax = new Vector2(0.5f, 0f);
        arrowRt.pivot = new Vector2(0.5f, 1f);
        arrowRt.anchoredPosition = Vector2.zero;
        arrowRt.sizeDelta = new Vector2(18f, 12f);
        UnityEngine.UI.Image arrowImg = arrowObj.AddComponent<UnityEngine.UI.Image>();
        arrowImg.sprite = CreateTriangleDownSprite();
        arrowImg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        arrowImg.raycastTarget = false;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(bubbleObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 5f);
        textRt.offsetMax = new Vector2(-10f, -5f);

        UnityEngine.UI.Text t = textObj.AddComponent<UnityEngine.UI.Text>();
        t.text = text;
        t.font = GameplayFontProvider.GetFont();
        t.fontSize = 16;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        float duration = 2.8f;
        float elapsed = 0f;
        Vector2 startPos = rootRt.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            rootRt.anchoredPosition = startPos + Vector2.up * (25f * progress);
            cg.alpha = progress < 0.7f ? 1f : 1f - ((progress - 0.7f) / 0.3f);
            yield return null;
        }

        Destroy(root);
    }

    private Sprite CreateTriangleDownSprite()
    {
        const int w = 32, h = 24;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float rowFrac = 1f - (float)y / h;
            float halfWidth = (w * 0.5f) * rowFrac;
            float cx = w * 0.5f;
            for (int x = 0; x < w; x++)
            {
                float dist = Mathf.Abs(x - cx);
                px[y * w + x] = dist <= halfWidth ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 32f);
    }

    private IEnumerator SinkBallAnimation(GameObject ball, BallController ballController)
    {
        ballController.NotifySinking();
        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        Vector3 startPos = ball.transform.position;
        Vector3 targetPos = transform.position;
        Vector3 startScale = ball.transform.localScale;
        Vector3 targetScale = ball.transform.localScale * 0.1f;

        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkDuration;

            // eased interpolation
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            ball.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);

            ball.transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

            ball.transform.Rotate(0, 0, sinkRotationSpeed * Time.deltaTime);

            yield return null;
        }

        ball.SetActive(false);
        ballIsSinking = false;

        // Any ball reaching the hole ends the relevance of map wall power-ups (all levels).
        CollectibleWallTriangle.DespawnAll();
        WallPlacementPowerUp.Instance?.CancelPowerUp();

        int sunkPlayer = ballController.PlayerIndex;
        if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
        {
            int maxShots = GameManager.GetMaxShotsPerPlayerForScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            int shotsTaken = maxShots - ballController.BallsRemaining;
            TurnManager.Instance.RecordPlayerSunk(sunkPlayer, shotsTaken);
        }
        else if (gameUI != null)
        {
            gameUI.ShowWin(sunkPlayer);
        }
    }

    public void SetRequiredSize(BallSize size)
    {
        requiredSize = size;
    }
}
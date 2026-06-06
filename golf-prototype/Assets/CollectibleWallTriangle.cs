using UnityEngine;

/// <summary>
/// Attach to your triangle object (SpriteRenderer is enough). A trigger collider is added automatically if missing.
/// When a ball touches it, that player gets one wall placement. WallPlacementPowerUp is auto-created if missing.
/// </summary>
public class CollectibleWallTriangle : MonoBehaviour
{
    private const string PowerUpSpriteResourcePath = "PowerUpStar";
    private static Sprite _powerUpSprite;
    private bool collected;
    private SpriteRenderer glowRenderer;
    private float glowTime;

    [Header("Collectible visuals")]
    [SerializeField] private float rotationSpeedDeg = 60f;
    [SerializeField] private float glowScale = 1.14f;
    [SerializeField] private float glowPulseSpeed = 2f;
    [SerializeField] private float glowMinAlpha = 0.20f;
    [SerializeField] private float glowMaxAlpha = 0.45f;

    public static void DespawnAll()
    {
        // FindObjectsByType can miss some scene instances (e.g. certain prefab setups).
        // FindObjectsOfTypeAll + scene filter removes every in-play collectible without touching assets.
        CollectibleWallTriangle[] all = Resources.FindObjectsOfTypeAll<CollectibleWallTriangle>();
        foreach (CollectibleWallTriangle c in all)
        {
            if (c == null) continue;
            GameObject go = c.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded)
                continue;
            Destroy(go);
        }
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            ApplyPowerUpSprite(sr);
            sr.color = Color.white;
            SetupGlow(sr);
        }

        EnsureTriggerCollider();

        if (WallPlacementPowerUp.Instance == null)
        {
            GameObject go = new GameObject("WallPlacementPowerUp");
            go.AddComponent<WallPlacementPowerUp>();
        }
    }

    private void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeedDeg * Time.deltaTime);

        if (glowRenderer == null) return;
        glowTime += Time.deltaTime * glowPulseSpeed;
        float t = (Mathf.Sin(glowTime) + 1f) * 0.5f;
        Color c = glowRenderer.color;
        c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t);
        glowRenderer.color = c;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        BallController ball = other.GetComponent<BallController>();
        if (ball == null || ball.IsEliminated || ball.IsSinking) return;

        collected = true;
        int player = ball.PlayerIndex;

        if (WallPlacementPowerUp.Instance != null)
            WallPlacementPowerUp.Instance.Begin(player);

        TutorialFieldLabels labels = FindFirstObjectByType<TutorialFieldLabels>();
        if (labels != null)
        {
            labels.HidePowerUpLabel();
        }

        Destroy(gameObject);
    }

    private void EnsureTriggerCollider()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Vector2 s = sr.sprite.bounds.size;
                Vector3 ls = transform.lossyScale;
                box.size = new Vector2(s.x * Mathf.Abs(ls.x), s.y * Mathf.Abs(ls.y));
            }
            else
                box.size = new Vector2(1f, 1f);
            col = box;
        }
        col.isTrigger = true;
    }

    private static void ApplyPowerUpSprite(SpriteRenderer sr)
    {
        if (sr == null) return;
        if (_powerUpSprite == null)
            _powerUpSprite = Resources.Load<Sprite>(PowerUpSpriteResourcePath);
        if (_powerUpSprite != null)
            sr.sprite = _powerUpSprite;
    }

    private void SetupGlow(SpriteRenderer source)
    {
        if (source == null || source.sprite == null) return;
        Transform existing = transform.Find("CollectibleGlow");
        if (existing != null)
            glowRenderer = existing.GetComponent<SpriteRenderer>();

        if (glowRenderer == null)
        {
            GameObject glowObj = new GameObject("CollectibleGlow");
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localRotation = Quaternion.identity;
            glowObj.transform.localScale = Vector3.one * glowScale;
            glowRenderer = glowObj.AddComponent<SpriteRenderer>();
        }

        glowRenderer.sprite = source.sprite;
        glowRenderer.sortingLayerID = source.sortingLayerID;
        glowRenderer.sortingOrder = source.sortingOrder - 1;
        glowRenderer.color = new Color(1f, 0.95f, 0.35f, glowMinAlpha);
    }
}

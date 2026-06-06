using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Assigns a tiled brick sprite (<c>WallBrickSeamless</c>) from Resources to structural walls (Tutorial + Levels).
/// Matches names like Left Wall, Right Wall, Top Wall, Bottom Wall.
/// Shared tiling behavior is in <see cref="ApplyTiledWallSprite"/> (also used for red zones / diagonal red walls).
/// </summary>
public static class StoneWallApplier
{
    private const string TileResourcePath = "StoneWall/WallBrickSeamless";

    /// <summary>
    /// Same tiling pipeline as Left/Right/Top/Bottom walls: capture world footprint from current bounds,
    /// reset scale to 1, tiled sprite fill, sync <see cref="BoxCollider2D"/>.
    /// </summary>
    /// <param name="preserveMirrorFromNegativeScale">
    /// If true, negative <see cref="Transform.localScale"/> is turned into <see cref="SpriteRenderer.flipX"/> / <c>flipY</c>
    /// before scaling to 1 (needed for some diagonal red walls).
    /// </param>
    public static void ApplyTiledWallSprite(Transform t, Sprite tile, Color tint, bool preserveMirrorFromNegativeScale = false)
    {
        if (t == null || tile == null)
            return;

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        BoxCollider2D box = t.GetComponent<BoxCollider2D>();

        Vector2 worldSize = new Vector2(sr.bounds.size.x, sr.bounds.size.y);

        bool flipX = false;
        bool flipY = false;
        if (preserveMirrorFromNegativeScale)
        {
            Vector3 ls = t.localScale;
            flipX = ls.x < 0f;
            flipY = ls.y < 0f;
        }

        t.localScale = Vector3.one;

        sr.sprite = tile;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.color = tint;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.flipX = flipX;
        sr.flipY = flipY;
        sr.size = worldSize;

        if (box != null)
        {
            box.size = worldSize;
            box.offset = Vector2.zero;
        }
    }

    /// <summary>
    /// Tiled sprite for rotated / diagonal walls. Resets scale to 1 (so tiles are not stretched)
    /// but sets <see cref="SpriteRenderer.size"/> from <c>|localScale| × BoxCollider2D.size</c>
    /// so the rendered area stays the same local footprint and follows the object's rotation.
    /// Negative scale components become <see cref="SpriteRenderer.flipX"/> / <c>flipY</c>.
    /// Use for red zones / diagonal walls.
    /// </summary>
    public static void ApplyTiledWallSpritePreserveOrientation(Transform t, Sprite tile, Color tint)
    {
        if (t == null || tile == null)
            return;

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        BoxCollider2D box = t.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            ApplyTiledWallSprite(t, tile, tint, preserveMirrorFromNegativeScale: true);
            return;
        }

        Vector3 ls = t.localScale;
        bool flipX = ls.x < 0f;
        bool flipY = ls.y < 0f;

        Vector2 effectiveSize = new Vector2(
            Mathf.Abs(ls.x) * box.size.x,
            Mathf.Abs(ls.y) * box.size.y);

        t.localScale = Vector3.one;

        sr.sprite = tile;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.color = tint;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.flipX = flipX;
        sr.flipY = flipY;
        sr.size = effectiveSize;

        box.size = effectiveSize;
        box.offset = Vector2.zero;
    }

    /// <summary>Returns true for GameObjects that should get the stone texture.</summary>
    public static bool IsStructuralWallName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;
        return objectName.StartsWith("Left Wall")
               || objectName.StartsWith("Right Wall")
               || objectName.StartsWith("Top Wall")
               || objectName.StartsWith("Bottom Wall");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        ApplyToScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToScene();
    }

    private static void ApplyToScene()
    {
        Sprite tile = Resources.Load<Sprite>(TileResourcePath);
        if (tile == null)
            return;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t == null) continue;
            if (!IsStructuralWallName(t.name))
                continue;

            ApplyTiledWallSprite(t, tile, Color.white);
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lethal spiked wall. Behaviour:
///   1. Rigid — ball cannot pass through it (kinematic Rigidbody2D + Continuous CCD).
///   2. Lethal — any contact with any part of the wall immediately eliminates the player.
///   3. Consistent — identical behaviour in every scene, every rotation, every ball size.
///
/// On Awake this script:
///   a) Resizes the PolygonCollider2D so it covers the FULL visual sprite extent.
///      The original scene polygon only covered a 1×1 local-space square, but the
///      spike sprites are 1×24 and 10×1 local units — leaving 96 % of the visual
///      spike with no collider at all. Renderer.localBounds gives the exact local
///      extents for any sprite, automatically, without hard-coded values.
///   b) Adds a Kinematic Rigidbody2D with useFullKinematicContacts so that:
///      - Box2D runs CCD between the dynamic ball and this kinematic body →
///        the ball is physically stopped regardless of speed or angle.
///      - OnCollisionEnter2D fires directly on this GameObject, not just on the ball.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class LethalWall : MonoBehaviour
{
    // Kept for external reference (e.g. editor tools, analytics) — not used for detection.
    public static readonly List<LethalWall> ActiveWalls = new List<LethalWall>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStaticState() { ActiveWalls.Clear(); }

    public Collider2D WallCollider { get; private set; }

    private GameUI gameUI;

    private void Awake()
    {
        gameUI       = FindFirstObjectByType<GameUI>();
        WallCollider = GetComponent<Collider2D>();
        ActiveWalls.Add(this);

        ResizeColliderToSprite();
        SetupRigidbody();
    }

    // ── Resize ───────────────────────────────────────────────────────────────────
    // Overwrites the PolygonCollider2D path with a clean 4-vertex rectangle that
    // exactly matches the SpriteRenderer's local-space bounding box.
    // Renderer.localBounds is independent of Transform.scale/rotation, so this
    // works correctly for every Sawtooth orientation in every scene.
    private void ResizeColliderToSprite()
    {
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        Renderer          rend = GetComponent<Renderer>();
        if (poly == null || rend == null) return;

        Bounds lb = rend.localBounds;            // local-space sprite bounds

        poly.pathCount = 1;
        poly.SetPath(0, new Vector2[]
        {
            new Vector2(lb.min.x, lb.min.y),    // bottom-left
            new Vector2(lb.max.x, lb.min.y),    // bottom-right
            new Vector2(lb.max.x, lb.max.y),    // top-right
            new Vector2(lb.min.x, lb.max.y),    // top-left
        });
    }

    // ── Rigidbody ────────────────────────────────────────────────────────────────
    // A Kinematic Rigidbody2D on the wall body guarantees:
    //   • Box2D treats dynamic-vs-kinematic contacts with full CCD sweep.
    //   • OnCollisionEnter2D is delivered to scripts on this GameObject.
    //   • The wall never moves (FreezeAll constraints + gravityScale 0).
    private void SetupRigidbody()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType              = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.gravityScale          = 0f;
        rb.constraints           = RigidbodyConstraints2D.FreezeAll;
    }

    private void OnDestroy() { ActiveWalls.Remove(this); }

    // ── Contact → Elimination ────────────────────────────────────────────────────
    // OnCollisionEnter2D  : first frame of contact.
    // OnCollisionStay2D   : every subsequent frame of contact (slow roll catch).
    // Both route to the same elimination logic on the ball.
    private void OnCollisionEnter2D(Collision2D col) => HandleContact(col.gameObject);
    private void OnCollisionStay2D (Collision2D col) => HandleContact(col.gameObject);

    private void HandleContact(GameObject obj)
    {
        if (gameUI == null) gameUI = FindFirstObjectByType<GameUI>();
        if (gameUI != null && gameUI.IsRoundComplete) return;

        BallController ball = obj.GetComponent<BallController>();
        if (ball == null || ball.IsEliminated) return;

        // All elimination logic (analytics, TurnManager routing, game-over UI)
        // lives in BallController.EliminateFromWall so it is centralised.
        ball.EliminateFromWall();
    }
}

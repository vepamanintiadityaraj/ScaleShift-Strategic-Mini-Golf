using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Placed walls break after two ball impacts: first hit halves thickness, second removes the wall.
/// Needs a kinematic Rigidbody2D so <see cref="OnCollisionEnter2D"/> is received.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BreakablePlacedWall : MonoBehaviour
{
    [Tooltip("Contacts from resting or nudging the wall (placement, bumping while nearly still) are ignored; only real strikes count.")]
    [SerializeField] private float minImpactSpeed = 0.45f;

    private int hits;
    private int requiredHits = -1;
    private Vector3 initialScale = Vector3.one;
    private SpriteRenderer sr;
    private Color baseColor = Color.white;
    private bool breaking;

    private void Awake()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.simulated = true;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        initialScale = transform.localScale;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        BallController ball = collision.collider.GetComponent<BallController>()
            ?? collision.collider.GetComponentInParent<BallController>();
        if (ball == null || ball.IsEliminated) return;

        if (collision.relativeVelocity.magnitude < minImpactSpeed)
            return;
        if (breaking)
            return;

        if (requiredHits < 0)
            requiredHits = GetRequiredHitsForBall(ball);

        hits++;

        if (hits >= requiredHits)
        {
            StartCoroutine(BreakAndDestroy());
            return;
        }

        // Shrink thickness proportionally (narrow dimension = local X for vertical wall)
        float remainingFrac = Mathf.Clamp01((requiredHits - hits) / (float)requiredHits);
        transform.localScale = new Vector3(initialScale.x * remainingFrac, initialScale.y, initialScale.z);
        StartCoroutine(HitFeedback());
    }

    private IEnumerator HitFeedback()
    {
        Vector3 startScale = transform.localScale;
        Vector3 punch = new Vector3(startScale.x * 1.06f, startScale.y * 1.015f, startScale.z);
        float duration = 0.10f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin((t / duration) * Mathf.PI);
            transform.localScale = Vector3.Lerp(startScale, punch, k);
            if (sr != null)
                sr.color = Color.Lerp(baseColor, Color.white, 0.2f + 0.6f * k);
            yield return null;
        }

        transform.localScale = startScale;
        if (sr != null) sr.color = baseColor;
    }

    private IEnumerator BreakAndDestroy()
    {
        breaking = true;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector3 startScale = transform.localScale;
        float duration = 0.18f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
            if (sr != null)
            {
                Color c = Color.Lerp(baseColor, Color.white, p * 0.5f);
                c.a = 1f - p;
                sr.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    private static int GetRequiredHitsForBall(BallController ball)
    {
        BallSizeController size = ball.GetComponent<BallSizeController>();
        BallSize current = size != null ? size.CurrentSize : BallSize.Normal;
        switch (current)
        {
            case BallSize.Large: return 1;
            case BallSize.Small: return 3;
            case BallSize.Normal:
            default:
                return 2;
        }
    }

    private void OnDestroy()
{
    if (SceneManager.GetActiveScene().name != "Tutorial")
        return;

    TutorialFieldLabels labels = FindFirstObjectByType<TutorialFieldLabels>();
    if (labels != null)
    {
        labels.HideTryBreakItHint();
    }
}
}

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CapsulePortal : MonoBehaviour
{
    [Header("Exit Selection")]
    [SerializeField] private float exitSwitchInterval = 2f;

    [Header("Teleport Settings")]
    [SerializeField] private float teleportCooldown = 0.2f;

    [Header("Exit Glow")]
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 1f;
    [SerializeField] private float glowPulseSpeed = 2.3f;

    private static readonly List<CapsulePortal> portals = new List<CapsulePortal>();
    private static readonly Dictionary<int, float> ballTeleportCooldowns = new Dictionary<int, float>();

    private static CapsulePortal currentExitPortal;
    private static float nextExitSwitchTime;

    private SpriteRenderer glowRenderer;
    private Transform glowTransform;
    private bool isExitPortal;
    private float glowPulseClock;

    private void OnEnable()
    {
        if (!portals.Contains(this))
            portals.Add(this);

        EnsurePortalColliderIsTrigger();
        EnsureGlowVisual();

        if (currentExitPortal == null)
            SetRandomExitPortal(excludeCurrent: false);
    }

    private void OnDisable()
    {
        portals.Remove(this);

        if (currentExitPortal == this)
        {
            currentExitPortal = null;
            SetRandomExitPortal(excludeCurrent: false);
        }
    }

    private void Update()
    {
        if (!IsUpdateDriver())
        {
            UpdateGlowPulse();
            return;
        }

        if (portals.Count > 0 && Time.time >= nextExitSwitchTime)
        {
            SetRandomExitPortal(excludeCurrent: true);
            nextExitSwitchTime = Time.time + Mathf.Max(0.1f, exitSwitchInterval);
        }

        UpdateGlowPulse();
    }

    private bool IsUpdateDriver()
    {
        return portals.Count > 0 && portals[0] == this;
    }

    private static void SetRandomExitPortal(bool excludeCurrent)
    {
        int availableCount = portals.Count;
        if (availableCount == 0)
        {
            currentExitPortal = null;
            return;
        }

        if (availableCount == 1)
        {
            SetCurrentExit(portals[0]);
            return;
        }

        CapsulePortal previous = currentExitPortal;
        CapsulePortal selected = previous;
        int guard = 0;
        while (selected == null || (excludeCurrent && selected == previous))
        {
            selected = portals[Random.Range(0, availableCount)];
            guard++;
            if (guard > 12)
                break;
        }

        if (selected == null)
            selected = portals[0];

        if (excludeCurrent && selected == previous)
            selected = GetFirstDifferentPortal(previous) ?? selected;

        SetCurrentExit(selected);
    }

    private static CapsulePortal GetFirstDifferentPortal(CapsulePortal comparedPortal)
    {
        for (int i = 0; i < portals.Count; i++)
        {
            if (portals[i] != comparedPortal)
                return portals[i];
        }
        return null;
    }

    private static void SetCurrentExit(CapsulePortal newExit)
    {
        if (currentExitPortal != null)
            currentExitPortal.SetGlow(false);

        currentExitPortal = newExit;

        if (currentExitPortal != null)
            currentExitPortal.SetGlow(true);
    }

    private void SetGlow(bool active)
    {
        if (glowRenderer == null)
            EnsureGlowVisual();

        if (glowRenderer != null)
        {
            isExitPortal = active;
            glowRenderer.enabled = active;
            Color glowColor = new Color(1f, 1f, 1f, glowAlpha);
            glowRenderer.color = glowColor;
        
        }
    }

    private void EnsurePortalColliderIsTrigger()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void EnsureGlowVisual()
{
    if (glowRenderer != null)
        return;

    Transform existing = transform.Find("PortalExitGlow");
    GameObject glowObject = existing != null ? existing.gameObject : new GameObject("PortalExitGlow");

    glowObject.transform.SetParent(transform, false);
    glowObject.transform.localPosition = Vector3.zero;

    glowObject.transform.localScale = Vector3.one * 1.15f;
    glowTransform = glowObject.transform;

    glowRenderer = glowObject.GetComponent<SpriteRenderer>();
    if (glowRenderer == null)
        glowRenderer = glowObject.AddComponent<SpriteRenderer>();

    SpriteRenderer portalRenderer = GetComponent<SpriteRenderer>();

    if (portalRenderer != null)
    {
        glowRenderer.sprite = portalRenderer.sprite;

        glowRenderer.sortingLayerID = portalRenderer.sortingLayerID;
        glowRenderer.sortingOrder = portalRenderer.sortingOrder - 1;
    }

    glowRenderer.color = new Color(1f, 1f, 0.5f, glowAlpha);
    glowRenderer.enabled = false;
}


   private void UpdateGlowPulse()
{
    if (!isExitPortal || glowRenderer == null || !glowRenderer.enabled)
        return;

    glowPulseClock += Time.deltaTime * Mathf.Max(0.1f, glowPulseSpeed);

    float pulse = (Mathf.Sin(glowPulseClock) + 1f) * 0.5f;

    float alpha = glowAlpha * (0.7f + 0.3f * pulse);
    glowRenderer.color = new Color(1f, 1f, 0.5f, alpha);
}

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTeleport(other != null ? other.gameObject : null);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryTeleport(collision != null ? collision.gameObject : null);
    }

    private void TryTeleport(GameObject contactedObject)
    {
        if (contactedObject == null)
            return;

        BallController ball = contactedObject.GetComponent<BallController>();
        if (ball == null)
            ball = contactedObject.GetComponentInParent<BallController>();
        if (ball == null)
            return;

        if (currentExitPortal == null || currentExitPortal == this)
            return;

        int ballId = ball.GetInstanceID();
        if (ballTeleportCooldowns.TryGetValue(ballId, out float nextAllowed) && Time.time < nextAllowed)
            return;

        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        Vector2 velocityBeforeTeleport = rb != null ? rb.linearVelocity : Vector2.zero;

        ball.transform.position = currentExitPortal.transform.position;

        if (rb != null)
            rb.linearVelocity = velocityBeforeTeleport;

        float nextCooldown = Time.time + Mathf.Max(0.05f, teleportCooldown);
        ballTeleportCooldowns[ballId] = nextCooldown;
    }
}

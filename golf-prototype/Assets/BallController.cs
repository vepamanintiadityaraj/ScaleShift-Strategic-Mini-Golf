using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class BallController : MonoBehaviour
{
    [Header("Wall Trap Detection")]
    [SerializeField] private float repeatHitWindow = 1.5f;

    private readonly Dictionary<int, float> lastWallHitTime = new Dictionary<int, float>();
    private readonly Dictionary<int, int> wallHitCount = new Dictionary<int, int>();
    [Header("Movement Settings")]
    [SerializeField] private float maxPower = 50f;
    [SerializeField] private float powerBarSpeed = 25f;
    [SerializeField] private float minVelocityToShoot = 0.1f;
    [SerializeField] private float rotationSpeed = 60f;

    [Header("Rolling visual")]
    [Tooltip("Spin direction flip if finger holes appear to roll backward.")]
    [SerializeField] private float rollVisualSign = -1f;

    [Header("Aim Settings")]
    [SerializeField] private Transform aimArrow;
    [SerializeField] private float aimArrowLength = 3f;
    [SerializeField] private int dotCount = 7;

    [Header("Mouse Pull (Pull back & release to launch)")]
    [SerializeField] private float pullRadius = 2f;
    [SerializeField] private float pullToPowerScale = 15f;
    [SerializeField] private float minPullToShoot = 0.3f;

    [Header("Two-Player (set 1 or 2 when using TurnManager)")]
    [SerializeField] private int playerIndex = 1;
    [Tooltip("Ball tint when Resources bowling sprites are missing; also drives glow color.")]
    [SerializeField] private Color player1BallColor = new Color(1f, 0.88f, 0.15f, 1f);
    [SerializeField] private Color player2BallColor = new Color(0.22f, 0.58f, 1f, 1f);

    private static Sprite _bowlingSpriteP1;
    private static Sprite _bowlingSpriteP2;
    [Tooltip("Assign if the ball sprite is on a child object; otherwise left empty.")]
    [SerializeField] private SpriteRenderer ballVisual;

    [Header("Active-Turn Glow")]
    [SerializeField] private float glowPulseSpeed = 2.5f;
    [SerializeField] private float glowMinScale   = 1.5f;
    [SerializeField] private float glowMaxScale   = 2.0f;
    [SerializeField] private float glowMinAlpha   = 0.55f;
    [SerializeField] private float glowMaxAlpha   = 0.95f;

    private SendToGoogle analytics;

    private Rigidbody2D rb;
    private float aimAngle = 0f;

    // Glow halo runtime state
    private SpriteRenderer glowRenderer;
    private GameObject glowObject;
    private Material glowMaterial;
    private float glowPulseTime;
    private bool glowActive;
    private float currentPower = 0f;
    private bool isCharging = false;
    private bool canShoot = true;
    private int ballsRemaining;
    private PowerBarUI powerBarUI;
    private GameUI gameUI;
    private bool powerBarGoingUp = true;
    private bool wasMovingLastFrame = false;
    private Vector3 startPosition;
    private bool isPulling = false;
    private Camera mainCam;
    private CircleCollider2D circleCol;

    public static readonly List<BallController> ActiveBalls = new List<BallController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearActiveBalls() { ActiveBalls.Clear(); }

    public int BallsRemaining => ballsRemaining;
    public bool IsMoving => rb.linearVelocity.magnitude > minVelocityToShoot;
    public int PlayerIndex => playerIndex;
    public bool IsEliminated { get; private set; }
    public bool IsSinking { get; private set; }
    public Collider2D BallCollider => circleCol;

    /// <summary>Called by HoleController when the sink animation begins.</summary>
    public void NotifySinking()
    {
        IsSinking = true;
        StopBall();
    }

    public void Eliminate()
    {
        if (IsEliminated) return;
        IsEliminated = true;
        ActiveBalls.Remove(this);
        StopBall();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ActiveBalls.Remove(this);
        if (glowMaterial != null) Destroy(glowMaterial);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circleCol = GetComponent<CircleCollider2D>();
        ActiveBalls.Add(this);

        rb.linearDamping = 2f;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        ballsRemaining = GameManager.GetMaxShotsPerPlayerForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        if (aimArrow == null)
        {
            GameObject arrowObj = new GameObject("Aim Arrow");
            arrowObj.transform.SetParent(transform);
            arrowObj.transform.localPosition = Vector3.zero;
            aimArrow = arrowObj.transform;

            for (int i = 0; i < dotCount; i++)
            {
                GameObject dot = new GameObject($"Dot {i}");
                dot.transform.SetParent(arrowObj.transform);

                float t = (i + 1) / (float)dotCount;
                float distance = aimArrowLength * t;
                dot.transform.localPosition = Vector3.right * distance;

                float sizeMultiplier = 1f - (t * 0.7f);
                float dotSize = 0.25f * sizeMultiplier;
                dot.transform.localScale = Vector3.one * dotSize;

                SpriteRenderer dotSprite = dot.AddComponent<SpriteRenderer>();
                dotSprite.sprite = CreateCircleSprite(Color.white);
                dotSprite.sortingOrder = 10;

                Color dotColor = Color.white;
                dotColor.a = 0.7f + (0.3f * (1f - t));
                dotSprite.color = dotColor;
            }
        }
    }

    private Sprite CreateCircleSprite(Color color)
    {
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 16;
                float dy = y - 16;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance <= 15)
                {
                    pixels[y * 32 + x] = color;
                }
                else
                {
                    pixels[y * 32 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
    }

    private Sprite CreateTriangleSprite(Color color)
    {
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float normalizedY = (y - 16) / 16f;
                float normalizedX = x / 32f;

                if (normalizedX >= 0 && Mathf.Abs(normalizedY) <= (1f - normalizedX))
                {
                    pixels[y * 32 + x] = color;
                }
                else
                {
                    pixels[y * 32 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0, 0.5f), 32);
    }

    private float GetWorldBallRadius()
    {
        if (circleCol == null) return 0.5f;
        float s = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        return Mathf.Max(circleCol.radius * s, 1e-4f);
    }

    /// <summary>
    /// Pick CW vs CCW so diagonal motion keeps |ω| = |v|/r (rolling without slipping).
    /// Falls back when vx ≈ vy so pure diagonal still spins.
    /// </summary>
    private static float RollDirectionSign(Vector2 v)
    {
        float vx = v.x;
        float vy = v.y;
        float d = vx - vy;
        if (Mathf.Abs(d) > 1e-5f)
            return Mathf.Sign(d);
        d = vx + vy;
        if (Mathf.Abs(d) > 1e-5f)
            return Mathf.Sign(d);
        return Mathf.Sign(vx != 0f ? vx : vy);
    }

    private void FixedUpdate()
    {
        if (rb == null || !rb.simulated || IsEliminated || IsSinking)
            return;

        Vector2 v = rb.linearVelocity;
        float speed = v.magnitude;
        float r = GetWorldBallRadius();

        if (speed < minVelocityToShoot)
        {
            rb.angularVelocity = 0f;
            return;
        }

        float omegaDegPerSec = Mathf.Rad2Deg * (speed / r) * RollDirectionSign(v) * rollVisualSign;
        rb.angularVelocity = omegaDegPerSec;
    }

    private void Start()
    {
        mainCam = Camera.main;
        powerBarUI = FindFirstObjectByType<PowerBarUI>();
        gameUI = FindFirstObjectByType<GameUI>();
        analytics = FindObjectOfType<SendToGoogle>(); // <--- analytics init
        startPosition = transform.position;

        ApplyBallVisual();
        CreateGlowHalo();

        if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
        {
            if (gameUI != null)
                gameUI.UpdatePlayerBalls(playerIndex, ballsRemaining);
        }
        else if (gameUI != null)
        {
            gameUI.UpdateBallsRemaining(ballsRemaining);
        }
    }

    private static Sprite LoadBowlingSprite(string resourcePath)
    {
        Sprite s = Resources.Load<Sprite>(resourcePath);
        if (s != null) return s;
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    private static bool TryGetBowlingSprites(out Sprite p1, out Sprite p2)
    {
        if (_bowlingSpriteP1 != null && _bowlingSpriteP2 != null)
        {
            p1 = _bowlingSpriteP1;
            p2 = _bowlingSpriteP2;
            return true;
        }

        _bowlingSpriteP1 = LoadBowlingSprite("BowlingBalls/Ball_Player1");
        _bowlingSpriteP2 = LoadBowlingSprite("BowlingBalls/Ball_Player2");
        p1 = _bowlingSpriteP1;
        p2 = _bowlingSpriteP2;
        return p1 != null && p2 != null;
    }

    private void ApplyBallVisual()
    {
        SpriteRenderer sr = ballVisual != null ? ballVisual : GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (TryGetBowlingSprites(out Sprite sp1, out Sprite sp2))
        {
            sr.sprite = playerIndex == 1 ? sp1 : sp2;
            sr.color = playerIndex == 1 ? player1BallColor : player2BallColor;
            return;
        }

        sr.color = playerIndex == 1 ? player1BallColor : player2BallColor;
    }

    // ── Glow Halo ────────────────────────────────────────────────────────────

    private void CreateGlowHalo()
    {
        SpriteRenderer ballSr = ballVisual != null ? ballVisual : GetComponent<SpriteRenderer>();
        if (ballSr == null) return;

        glowObject = new GameObject("GlowHalo");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = Vector3.zero;

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = CreateGlowSprite();

        // Standard alpha-transparent material — always works in any Unity pipeline.
        // Additive blend via SetInt is unreliable across Unity versions, so we skip it.
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            glowMaterial = new Material(spriteShader);
            glowRenderer.material = glowMaterial;
        }

        // Render ABOVE the ball so it is never buried by background/environment sprites.
        // The ring sprite is hollow in the centre so the ball remains fully visible.
        glowRenderer.sortingLayerID = ballSr.sortingLayerID;
        glowRenderer.sortingOrder   = ballSr.sortingOrder + 1;

        Color c = playerIndex == 1 ? player1BallColor : player2BallColor;
        glowRenderer.color = new Color(c.r, c.g, c.b, 0f);

        glowObject.SetActive(false);
    }

    /// <summary>
    /// Generates a ring-shaped glow texture: transparent in the centre (ball shows through),
    /// bright at the ring band, fading to transparent at the outer edge.
    /// </summary>
    private Sprite CreateGlowSprite()
    {
        const int size        = 128;
        const float innerFrac = 0.40f; // inner transparent hole (fraction of half-size)
        const float outerFrac = 0.98f; // outer edge of the ring

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels   = new Color[size * size];
        float center     = size * 0.5f;
        float innerR     = center * innerFrac;
        float outerR     = center * outerFrac;
        float peakR      = (innerR + outerR) * 0.5f;
        float halfWidth  = peakR - innerR;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 0f;
                if (dist > innerR && dist < outerR)
                {
                    float t = 1f - Mathf.Abs(dist - peakR) / halfWidth;
                    alpha = Mathf.Clamp01(t * t);
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void UpdateGlow()
    {
        if (glowObject == null) return;

        if (gameUI != null && gameUI.IsRoundComplete)
        {
            if (glowActive)
            {
                glowObject.SetActive(false);
                glowActive = false;
                glowPulseTime = 0f;
            }
            return;
        }

        // Show glow only when idle and waiting for the player to aim.
        // Hide as soon as the player starts pulling/charging or the ball is moving.
        // When there is no TurnManager (tutorial / single-player) it is always this player's turn.
        bool isMyTurn = TurnManager.Instance == null || TurnManager.Instance.IsMyTurn(playerIndex);
        bool shouldGlow = !IsEliminated &&
                          !isPulling &&
                          !isCharging &&
                          !IsMoving &&
                          isMyTurn;

        if (!shouldGlow)
        {
            if (glowActive)
            {
                glowObject.SetActive(false);
                glowActive = false;
                glowPulseTime = 0f;
            }
            return;
        }

        if (!glowActive)
        {
            glowObject.SetActive(true);
            glowActive = true;
        }

        glowPulseTime += Time.deltaTime * glowPulseSpeed;
        float t = (Mathf.Sin(glowPulseTime) + 1f) * 0.5f; // 0 → 1 oscillation

        glowObject.transform.localScale = Vector3.one * Mathf.Lerp(glowMinScale, glowMaxScale, t);

        Color c = playerIndex == 1 ? player1BallColor : player2BallColor;
        glowRenderer.color = new Color(c.r, c.g, c.b, Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t));
    }

    private void Update()
    {
        UpdateGlow();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            RestartGame();
            return;
        }

        if (gameUI != null && !gameUI.IsGameStarted)
        {
            if (aimArrow != null && aimArrow.gameObject != null)
                aimArrow.gameObject.SetActive(false);
            return;
        }

        if (gameUI != null && gameUI.IsRoundComplete)
        {
            if (aimArrow != null && aimArrow.gameObject != null)
                aimArrow.gameObject.SetActive(false);
            return;
        }

        if (TurnManager.Instance != null && !TurnManager.Instance.IsMyTurn(playerIndex))
        {
            if (aimArrow != null && aimArrow.gameObject != null)
                aimArrow.gameObject.SetActive(false);
            return;
        }

        if (IsSinking)
        {
            if (aimArrow != null && aimArrow.gameObject != null)
                aimArrow.gameObject.SetActive(false);
            return;
        }

        canShoot = !IsMoving;

        if (wasMovingLastFrame && canShoot && !IsSinking && ballsRemaining <= 0)
        {
            wasMovingLastFrame = false;
            if (TurnManager.Instance == null || !TurnManager.Instance.IsTwoPlayerMode)
            {
                if (gameUI != null)
                    gameUI.ShowGameOver();
                return;
            }
        }
        wasMovingLastFrame = IsMoving;

        if (WallPlacementPowerUp.Instance != null && WallPlacementPowerUp.Instance.IsPendingFor(playerIndex))
        {
            if (TurnManager.Instance == null || TurnManager.Instance.IsMyTurn(playerIndex))
            {
                if (aimArrow != null && aimArrow.gameObject != null)
                    aimArrow.gameObject.SetActive(false);
                EventSystem es = EventSystem.current;
                if (es == null || !es.IsPointerOverGameObject())
                    WallPlacementPowerUp.Instance.HandleInput();
                return;
            }
        }

        if (!canShoot || isCharging)
        {
            if (aimArrow != null && aimArrow.gameObject != null && !isPulling)
            {
                aimArrow.gameObject.SetActive(false);
            }

            if (!canShoot)
                return;
        }

        if (canShoot && !isCharging && !isPulling)
        {
            if (aimArrow != null && aimArrow.gameObject != null)
            {
                aimArrow.gameObject.SetActive(true);
            }
        }
        if (isPulling && aimArrow != null && aimArrow.gameObject != null)
        {
            aimArrow.gameObject.SetActive(true);
        }

        HandleAimInput();

        HandleMousePullInput();
        if (!isPulling)
            HandleShootInput();

        UpdateAimArrow();
    }

    private void HandleAimInput()
    {
        if (isPulling) return;
        if (Input.GetKey(KeyCode.A))
        {
            aimAngle += rotationSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            aimAngle -= rotationSpeed * Time.deltaTime;
        }

        aimAngle = aimAngle % 360f;
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCam == null) return (Vector2)transform.position;
        Vector3 mousePos = Input.mousePosition;
        if (float.IsInfinity(mousePos.x) || float.IsInfinity(mousePos.y) ||
            float.IsNaN(mousePos.x) || float.IsNaN(mousePos.y))
            return (Vector2)transform.position;
        Vector3 p = mainCam.ScreenToWorldPoint(mousePos);
        return new Vector2(p.x, p.y);
    }

    private void HandleMousePullInput()
    {
        Vector2 ballPos = transform.position;
        Vector2 mouseWorld = GetMouseWorldPosition();

        if (Input.GetMouseButtonDown(0))
        {
            if (canShoot && !isCharging && !isPulling && (mouseWorld - ballPos).sqrMagnitude <= pullRadius * pullRadius)
            {
                isPulling = true;
            }
        }

        if (isPulling)
        {
            Vector2 pullVector = ballPos - mouseWorld;
            float pullDistance = pullVector.magnitude;
            if (pullDistance > 0.001f)
            {
                Vector2 direction = pullVector / pullDistance;
                currentPower = Mathf.Min(pullDistance * pullToPowerScale, maxPower);
                aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (powerBarUI != null)
                {
                    powerBarUI.Show();
                    powerBarUI.SetPower(currentPower / maxPower);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                float releasePullDistance = (ballPos - GetMouseWorldPosition()).magnitude;
                if (releasePullDistance >= minPullToShoot)
                {
                    ShootWithPull();
                }
                if (powerBarUI != null)
                    powerBarUI.Hide();
                isPulling = false;
            }
        }
    }

    private void HandleShootInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isCharging)
            {
                isCharging = true;
                currentPower = 0f;
                powerBarGoingUp = true;
                if (powerBarUI != null)
                    powerBarUI.Show();
            }
            else
            {
                Shoot();
            }
        }

        if (isCharging)
        {
            if (powerBarGoingUp)
            {
                currentPower += powerBarSpeed * Time.deltaTime;
                if (currentPower >= maxPower)
                {
                    currentPower = maxPower;
                    powerBarGoingUp = false;
                }
            }
            else
            {
                currentPower -= powerBarSpeed * Time.deltaTime;
                if (currentPower <= 0f)
                {
                    currentPower = 0f;
                    powerBarGoingUp = true;
                }
            }

            if (powerBarUI != null)
            {
                powerBarUI.SetPower(currentPower / maxPower);
            }
        }
    }

    private void Shoot()
    {
        if (!canShoot || !isCharging)
            return;

        if (ballsRemaining <= 0)
        {
            if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
            {
                // Analytics: out of balls before attempting to shoot
                if (analytics != null)
                {
                    Debug.Log($"Analytics: OutOfBalls - Player {playerIndex}");
                    analytics.Send("OutOfBalls", "Player " + playerIndex, "", "0", "");
                }

                TurnManager.Instance.SwitchTurnBecauseCurrentOut();
            }
            else if (gameUI != null)
                gameUI.ShowGameOver();
            return;
        }

        ApplyShot();
        isCharging = false;
        currentPower = 0f;

        if (powerBarUI != null)
            powerBarUI.Hide();

        AfterShot();
    }

    private void ShootWithPull()
    {
        if (!canShoot || ballsRemaining <= 0)
        {
            if (ballsRemaining <= 0)
            {
                // Analytics: out of balls when pulling
                if (analytics != null)
                {
                    Debug.Log($"Analytics: OutOfBalls - Player {playerIndex}");
                    analytics.Send("OutOfBalls", "Player " + playerIndex, "", "0", "");
                }

                if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
                    TurnManager.Instance.SwitchTurnBecauseCurrentOut();
                else if (gameUI != null)
                    gameUI.ShowGameOver();
            }
            return;
        }

        ApplyShot();
        currentPower = 0f;

        if (powerBarUI != null)
            powerBarUI.Hide();

        AfterShot();
    }

    private void ApplyShot()
    {
        float angleRad = aimAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        rb.AddForce(direction * currentPower, ForceMode2D.Impulse);
        ballsRemaining--;
        if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
            TurnManager.Instance.NotifyCurrentPlayerShot();

        // Analytics: shot taken
        if (analytics != null)
        {
            Debug.Log($"Analytics: ShotTaken - Player {playerIndex}, BallsLeft {ballsRemaining}");
            analytics.Send("ShotTaken", playerIndex.ToString(), "", ballsRemaining.ToString(), "");
        }
    }

    private void AfterShot()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
        {
            TurnManager.Instance.NotifyBallCountChanged(playerIndex, ballsRemaining);
        }
        else if (gameUI != null)
        {
            gameUI.UpdateBallsRemaining(ballsRemaining);
        }
    }

    private void UpdateAimArrow()
    {
        if (aimArrow == null)
            return;
        if (!canShoot && !isPulling)
            return;
        if (isCharging && !isPulling)
            return;

        aimArrow.rotation = Quaternion.Euler(0, 0, aimAngle);

        // Counteract the ball's world scale (set by BallSizeController) so the aim
        // dots always appear at a consistent size regardless of whether the ball is
        // small (0.5×), normal (1×), or large (1.5×).
        float parentScale = transform.lossyScale.x;
        float inv = parentScale > 0.001f ? 1f / parentScale : 1f;

        if (isPulling)
        {
            float powerRatio = Mathf.Clamp01(currentPower / maxPower);
            float arrowScale = 0.2f + 0.8f * powerRatio;
            // X stretches with power (arrow reach); Y stays uniform (dot height).
            aimArrow.localScale = new Vector3(arrowScale * inv, inv, 1f);
        }
        else
        {
            aimArrow.localScale = new Vector3(inv, inv, 1f);
        }
    }

    public void StopBall()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        isCharging = false;
        isPulling = false;
        if (powerBarUI != null)
            powerBarUI.Hide();
        if (aimArrow != null && aimArrow.gameObject != null)
            aimArrow.gameObject.SetActive(false);
    }

    /// <summary>Tutorial: reset ball to start position, stop movement, set size to normal.</summary>
    public void ResetToInitialPosition()
    {
        transform.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        StopBall();
        var sizeController = GetComponent<BallSizeController>();
        if (sizeController != null)
            sizeController.SetSize(BallSize.Normal);
    }

    /// <summary>
    /// Called by LethalWall when this ball contacts any part of a spiked wall.
    /// Handles analytics, TurnManager routing and game-over UI in one place.
    /// </summary>
    public void EliminateFromWall()
    {
        Vector2 ballPos = transform.position;
        if (IsEliminated) return;

        if (analytics != null)
        {
            Debug.Log($"Analytics: HitLethalWall - Player {playerIndex}");
            analytics.Send("HitLethalWall", "Player " + playerIndex, "", ballsRemaining.ToString(), "", $"{ballPos.x:F2},{ballPos.y:F2}");
        }

        if (TurnManager.Instance != null && TurnManager.Instance.IsTwoPlayerMode)
            TurnManager.Instance.EliminatePlayer(playerIndex);
        else
        {
            Eliminate();
            gameUI?.ShowGameOver("GAME OVER!\nYou hit a lethal spiked wall!");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckWallCollision(collision.gameObject, collision.contacts);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckWallTrigger(other);
    }

    private void CheckWallCollision(GameObject other, ContactPoint2D[] contacts)
    {
        if (IsEliminated) return;
        if (gameUI != null && gameUI.IsRoundComplete) return;

        bool isWall = IsWall(other);
        LethalWall lethalWall = ResolveLethalWall(other);

        if (!isWall && lethalWall == null)
            return;

        Vector2 ballPos = transform.position;
        Vector2 hitPos = contacts != null && contacts.Length > 0 ? contacts[0].point : ballPos;

        if (isWall)
            TrackWallHit(other, "collision", ballPos, hitPos);

        if (lethalWall != null)
            EliminateFromWall();
    }

    private void CheckWallTrigger(Collider2D other)
    {
        if (IsEliminated) return;
        if (gameUI != null && gameUI.IsRoundComplete) return;

        bool isWall = IsWall(other.gameObject);
        LethalWall lethalWall = ResolveLethalWall(other.gameObject);

        if (!isWall && lethalWall == null)
            return;

        Vector2 ballPos = transform.position;
        Vector2 hitPos = other.ClosestPoint(transform.position);

        if (isWall)
            TrackWallHit(other.gameObject, "trigger", ballPos, hitPos);

        if (lethalWall != null)
            EliminateFromWall();
    }

    private bool IsWall(GameObject obj)
    {
        if (obj == null) return false;

        Transform current = obj.transform;
        while (current != null)
        {
            if (current.CompareTag("Wall"))
                return true;
            current = current.parent;
        }

        return false;
    }

    private GameObject GetWallRoot(GameObject obj)
    {
        if (obj == null) return null;

        Transform current = obj.transform;
        while (current != null)
        {
            if (current.CompareTag("Wall"))
                return current.gameObject;
            current = current.parent;
        }

        return obj;
    }

    private void TrackWallHit(GameObject wallObj, string hitType, Vector2 ballPos, Vector2 hitPos)
    {
        GameObject wallRoot = GetWallRoot(wallObj);
        if (wallRoot == null) return;

        int wallId = wallRoot.GetInstanceID();

        if (wallHitCount.ContainsKey(wallId))
            wallHitCount[wallId]++;
        else
            wallHitCount[wallId] = 1;

        int count = wallHitCount[wallId];

        if (analytics != null && count >= 2)
        {
            analytics.Send(
                "WallTrapHit",
                "Player " + playerIndex,
                "",
                "",
                "",
                $"{ballPos.x:F2},{ballPos.y:F2}",
                wallRoot.name,
                count.ToString()
            );
        }
    }
    private LethalWall ResolveLethalWall(GameObject obj)
    {
        if (obj == null) return null;

        LethalWall wall = obj.GetComponent<LethalWall>();
        if (wall != null) return wall;

        wall = obj.GetComponentInParent<LethalWall>();
        if (wall != null) return wall;

        return obj.transform.root.GetComponentInChildren<LethalWall>();
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
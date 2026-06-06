using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text ballsRemainingText;
    [SerializeField] private Text ballSizeText;
    [SerializeField] private Text instructionsText;
    [SerializeField] private Text turnText;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winMessageText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverText;
    [SerializeField] private SendToGoogle analytics; // analytics branch

    private bool isGameOver = false;
    /// <summary>Set when a level winner is decided (win UI). Distinct from game-over loss.</summary>
    private bool roundComplete = false;
    private int player1Balls;
    private int player2Balls;
    private GameObject startPanelOverlay;
    private GameObject eliminationOverlay;
    private bool endButtonsCreated = false;

    // Tracks whether the player has already chosen "Start Game" or "Start Tutorial"
    // from the main menu. Persists across scene loads so the menu only appears once
    // when the game first launches, not before every level or tutorial restart.
    private static bool hasSelectedLaunchOption = false;

    public bool IsGameOver => isGameOver;
    /// <summary>Win or loss: stop turns, glow, and lethal contacts.</summary>
    public bool IsRoundComplete => isGameOver || roundComplete;
    public bool IsGameStarted { get; private set; }

    private int maxBallsPerPlayer = 15;

    private void Awake()
    {
        roundComplete = false;
        maxBallsPerPlayer = GameManager.GetMaxShotsPerPlayerForScene(SceneManager.GetActiveScene().name);
        player1Balls = maxBallsPerPlayer;
        player2Balls = maxBallsPerPlayer;

        // Set correct balls text and hide ball size text (handles serialized references)
        if (ballsRemainingText != null)
            ballsRemainingText.text = "Balls: " + maxBallsPerPlayer + "/" + maxBallsPerPlayer;
        if (ballSizeText != null)
            ballSizeText.gameObject.SetActive(false);

        // Also find and fix any Text in scene showing "Ball Size" or "Balls: 5/5"
        Text[] allTexts = FindObjectsByType<Text>(FindObjectsSortMode.None);
        foreach (Text t in allTexts)
        {
            if (t == null) continue;
            string txt = t.text;
            if (txt != null && (txt.StartsWith("Ball Size:", System.StringComparison.OrdinalIgnoreCase) || txt == "Balls: 5/5"))
            {
                if (txt.StartsWith("Ball Size:", System.StringComparison.OrdinalIgnoreCase))
                    t.gameObject.SetActive(false);
                else
                    t.text = "Balls: " + maxBallsPerPlayer + "/" + maxBallsPerPlayer;
            }
        }

        if (ballsRemainingText != null)
            EnsureShotsTextLayout();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (SceneManager.GetActiveScene().name == "Tutorial" && IsGameStarted)
            {
                hasSelectedLaunchOption = false;
                SceneManager.LoadScene("Tutorial");
            }
            else
            {
                PlayAgain();
            }
        }
    }

    private void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // If ballsRemainingText is missing from scene, create it on the Canvas at runtime
        if (ballsRemainingText == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject ballsObj = new GameObject("Balls Remaining Text");
                ballsObj.transform.SetParent(canvas.transform, false);
                RectTransform rt = ballsObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(10f, -10f);
                rt.sizeDelta = new Vector2(720f, 50f);
                ballsRemainingText = ballsObj.AddComponent<Text>();
                ballsRemainingText.font = GameplayFontProvider.GetFont();
                ballsRemainingText.fontSize = 18;
                ballsRemainingText.color = Color.white;
                ballsRemainingText.alignment = TextAnchor.UpperLeft;
            }
        }

        EnsureShotsTextLayout();

        TurnManager turnMgr = FindFirstObjectByType<TurnManager>();
        if (turnMgr != null && turnMgr.IsTwoPlayerMode)
        {
            UpdatePlayerBalls(1, maxBallsPerPlayer);
            UpdatePlayerBalls(2, maxBallsPerPlayer);
        }
        else
            UpdateBallsRemaining(maxBallsPerPlayer);

        UpdateBallSize(BallSize.Normal);

        if (instructionsText != null)
        {
            instructionsText.text = "Mouse: Pull ball back & release to launch | ESC: Restart";
            RectTransform instructionsRect = instructionsText.GetComponent<RectTransform>();
            if (instructionsRect != null)
            {
                instructionsRect.anchorMin = new Vector2(0.5f, 1f);
                instructionsRect.anchorMax = new Vector2(0.5f, 1f);
                instructionsRect.pivot = new Vector2(0.5f, 1f);
                instructionsRect.anchoredPosition = new Vector2(0f, -40f);
            }
        }

        if (SceneManager.GetActiveScene().name == "Tutorial" && TutorialManager.Instance == null)
        {
            GameObject go = new GameObject("TutorialManager");
            go.AddComponent<TutorialManager>();
            go.AddComponent<TutorialFieldLabels>();
        }

        // Always ensure an EventSystem exists so UI buttons (win panel, game over, etc.)
        // receive clicks even when the start menu is skipped.
        EnsureEventSystem();

        if (!hasSelectedLaunchOption)
        {
            // First time the player reaches a GameUI scene this session:
            // show the main menu start screen with Start Game / Start Tutorial.
            CreateStartScreenOverlay();
        }
        else
        {
            // Player already picked from the main menu; go straight into the level.
            IsGameStarted = true;
            GameplayFontProvider.ApplyToActiveSceneText(excludeStartScreen: true);
            if (SceneManager.GetActiveScene().name == "Tutorial" && TutorialManager.Instance != null)
                TutorialManager.Instance.BeginTutorial();
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }

    // ----- START SCREEN (image-based menu with Start Game + Start Tutorial) -----
    private const float StartScreenAspect = 1024f / 559f;

    private void CreateStartScreenOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        EnsureEventSystem();

        startPanelOverlay = new GameObject("Start Screen Overlay");
        startPanelOverlay.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = startPanelOverlay.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.SetAsLastSibling();

        // Dark letterbox background behind the image (shows if screen aspect != image aspect)
        Image panelImage = startPanelOverlay.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.04f, 0.08f, 1f);
        panelImage.raycastTarget = true;

        // Inner container preserves the image's aspect ratio so button hotspots line up
        GameObject innerObj = new GameObject("Start Screen Image");
        innerObj.transform.SetParent(startPanelOverlay.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = Vector2.zero;
        innerRect.offsetMax = Vector2.zero;

        AspectRatioFitter fitter = innerObj.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = StartScreenAspect;

        Sprite bgSprite = LoadStartScreenSprite();
        Image bgImage = innerObj.AddComponent<Image>();
        if (bgSprite != null)
        {
            bgImage.sprite = bgSprite;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = false;
        }
        else
        {
            bgImage.color = new Color(0.2f, 0.55f, 0.35f, 1f);
        }
        bgImage.raycastTarget = false;

        // Invisible button over the "START GAME" button in the image
        CreateStartScreenHotspot(
            innerObj.transform, "Start Game Button",
            new Vector2(0.33f, 0.34f), new Vector2(0.69f, 0.57f),
            OnStartGameClicked);

        // Invisible button over the "START TUTORIAL" button in the image
        CreateStartScreenHotspot(
            innerObj.transform, "Start Tutorial Button",
            new Vector2(0.35f, 0.10f), new Vector2(0.67f, 0.32f),
            OnStartTutorialClicked);
    }

    private Sprite LoadStartScreenSprite()
    {
        Sprite sprite = Resources.Load<Sprite>("StartScreen");
        if (sprite != null) return sprite;

        Sprite[] all = Resources.LoadAll<Sprite>("StartScreen");
        if (all != null && all.Length > 0) return all[0];

        Texture2D tex = Resources.Load<Texture2D>("StartScreen");
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }

    private void CreateStartScreenHotspot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Fully transparent but still receives raycasts
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.22f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
    }

    private void OnStartGameClicked()
    {
        hasSelectedLaunchOption = true;
        if (SceneManager.GetActiveScene().name == "Level1")
            HideStartScreen();
        else
            SceneManager.LoadScene("Level1");
    }

    private void OnStartTutorialClicked()
    {
        hasSelectedLaunchOption = true;
        if (SceneManager.GetActiveScene().name == "Tutorial")
            HideStartScreen();
        else
            SceneManager.LoadScene("Tutorial");
    }

    public void HideStartScreen()
    {
        if (startPanelOverlay != null)
            startPanelOverlay.SetActive(false);
        IsGameStarted = true;
        GameplayFontProvider.ApplyToActiveSceneText(excludeStartScreen: true);

        if (ballsRemainingText != null) ballsRemainingText.gameObject.SetActive(true);
        if (ballSizeText != null) ballSizeText.gameObject.SetActive(false);
        if (instructionsText != null) instructionsText.gameObject.SetActive(true);
        if (turnText != null) turnText.gameObject.SetActive(true);

        if (analytics != null)
        {
            Debug.Log("Analytics: GameStarted");
            analytics.Send("GameStarted", "0", "N/A", maxBallsPerPlayer.ToString(), "Start");
        }
        if (SceneManager.GetActiveScene().name == "Tutorial" && TutorialManager.Instance != null)
            TutorialManager.Instance.BeginTutorial();
    }
    // ----- END START SCREEN -----

    public void UpdateBallsRemaining(int balls)
    {
        if (ballsRemainingText != null)
        {
            ballsRemainingText.text = "Shots remaining: " + balls + "/" + maxBallsPerPlayer;
        }
    }

    // When the other player has sunk first, the trailing player only has their
    // 1-shot draw chance left; we clamp their displayed count to 1 so the UI
    // reflects the real stakes instead of the raw balls-remaining number.
    private int drawChanceUIPlayer = 0;

    public void SetDrawChanceUI(int player)
    {
        drawChanceUIPlayer = player;
        RefreshPlayerBallsText();
    }

    public void UpdatePlayerBalls(int player, int count)
    {
        if (player == 1) player1Balls = count;
        else if (player == 2) player2Balls = count;
        RefreshPlayerBallsText();
    }

    private void RefreshPlayerBallsText()
    {
        if (ballsRemainingText == null) return;

        int displayP1 = (drawChanceUIPlayer == 1) ? 1 : player1Balls;
        int displayP2 = (drawChanceUIPlayer == 2) ? 1 : player2Balls;

        // Two lines so nothing is clipped; overflow as backup
        ballsRemainingText.text = "P1 shots remaining : " + displayP1 + "\n" + "P2 shots remaining : " + displayP2;
    }

    /// <summary>
    /// Prevents long two-line text from being clipped by a narrow RectTransform.
    /// </summary>
    private void EnsureShotsTextLayout()
    {
        if (ballsRemainingText == null) return;
        ballsRemainingText.horizontalOverflow = HorizontalWrapMode.Overflow;
        ballsRemainingText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rt = ballsRemainingText.rectTransform;
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(10f, -72f);
        rt.offsetMax = new Vector2(-10f, -10f);
    }

    public void SetTurn(int player)
    {
        if (turnText != null)
            turnText.text = "Turn: Player " + player;
    }

    public void ShowPlayerSunk(int playerWhoSunk)
    {
        int other = playerWhoSunk == 1 ? 2 : 1;
        if (turnText != null)
            turnText.text = "Player " + playerWhoSunk + " has sunk! Player " + other + "'s turn.";

        if (analytics != null)
        {
            Debug.Log("Analytics: PlayerSunk - Player " + playerWhoSunk);
            analytics.Send("PlayerSunk", playerWhoSunk.ToString(), "N/A", "N/A", "Sunk");
        }
    }

    public void ShowPlayerHitWall(int playerWhoHit)
    {
        int other = playerWhoHit == 1 ? 2 : 1;
        if (turnText != null)
            turnText.text = "Player " + playerWhoHit + " hit the wall! Player " + other + "'s turn.";

        if (analytics != null)
        {
            Debug.Log("Analytics: HitWall - Player " + playerWhoHit);
            analytics.Send("HitWall", playerWhoHit.ToString(), "N/A", "N/A", "HitWall");
        }
    }

    public void ShowPlayerEliminated(int eliminatedPlayer, int continuingPlayer)
    {
        CollectibleWallTriangle.DespawnAll();
        WallPlacementPowerUp.Instance?.CancelPowerUp();

        // main branch elimination overlay
        CreateEliminationOverlay(eliminatedPlayer, continuingPlayer);
        if (eliminationOverlay != null)
        {
            UpdateEliminationOverlayText(eliminatedPlayer, continuingPlayer);
            eliminationOverlay.SetActive(true);
        }

        // analytics tracking (kept from analytics branch)
        if (analytics != null)
        {
            Debug.Log("Analytics: HitLethalWall - Player " + eliminatedPlayer);
            analytics.Send("HitLethalWall", eliminatedPlayer.ToString(), "N/A", "N/A",
                "Eliminated, continuing: " + continuingPlayer);
        }
    }

    private void UpdateEliminationOverlayText(int eliminatedPlayer, int continuingPlayer)
    {
        if (eliminationOverlay == null) return;
        Text[] texts = eliminationOverlay.GetComponentsInChildren<Text>();
        foreach (Text t in texts)
        {
            if (t.gameObject.name == "Elimination Headline")
                t.text = "PLAYER " + eliminatedPlayer + " IS OUT";
            else if (t.gameObject.name == "Elimination Reason")
                t.text = "Hit a lethal spiked wall!";
            else if (t.gameObject.name == "Elimination Continue")
                t.text = "Press any key to continue";
        }
    }

    private void CreateEliminationOverlay(int eliminatedPlayer, int continuingPlayer)
    {
        if (eliminationOverlay != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        eliminationOverlay = new GameObject("Elimination Overlay");
        eliminationOverlay.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = eliminationOverlay.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.SetAsLastSibling();

        Image panelImage = eliminationOverlay.AddComponent<Image>();
        panelImage.color = new Color(0.5f, 0.05f, 0.05f, 0.99f);

        // Big headline: "PLAYER X IS OUT"
        GameObject headlineObj = new GameObject("Elimination Headline");
        headlineObj.transform.SetParent(eliminationOverlay.transform, false);
        RectTransform headlineRect = headlineObj.AddComponent<RectTransform>();
        headlineRect.anchorMin = new Vector2(0.5f, 0.62f);
        headlineRect.anchorMax = new Vector2(0.5f, 0.62f);
        headlineRect.pivot = new Vector2(0.5f, 0.5f);
        headlineRect.anchoredPosition = Vector2.zero;
        headlineRect.sizeDelta = new Vector2(900, 100);
        Text headlineText = headlineObj.AddComponent<Text>();
        headlineText.text = "PLAYER " + eliminatedPlayer + " IS OUT";
        headlineText.font = GameplayFontProvider.GetFont();
        headlineText.fontSize = 56;
        headlineText.fontStyle = FontStyle.Bold;
        headlineText.color = Color.white;
        headlineText.alignment = TextAnchor.MiddleCenter;

        // Reason line: "Hit a lethal spiked wall!"
        GameObject reasonObj = new GameObject("Elimination Reason");
        reasonObj.transform.SetParent(eliminationOverlay.transform, false);
        RectTransform reasonRect = reasonObj.AddComponent<RectTransform>();
        reasonRect.anchorMin = new Vector2(0.5f, 0.48f);
        reasonRect.anchorMax = new Vector2(0.5f, 0.48f);
        reasonRect.pivot = new Vector2(0.5f, 0.5f);
        reasonRect.anchoredPosition = Vector2.zero;
        reasonRect.sizeDelta = new Vector2(800, 80);
        Text reasonText = reasonObj.AddComponent<Text>();
        reasonText.text = "Hit a lethal spiked wall!";
        reasonText.font = GameplayFontProvider.GetFont();
        reasonText.fontSize = 38;
        reasonText.fontStyle = FontStyle.Bold;
        reasonText.color = new Color(1f, 0.85f, 0.85f);
        reasonText.alignment = TextAnchor.MiddleCenter;

        // Continue line: "Press any key to continue"
        GameObject continueObj = new GameObject("Elimination Continue");
        continueObj.transform.SetParent(eliminationOverlay.transform, false);
        RectTransform continueRect = continueObj.AddComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(0.5f, 0.36f);
        continueRect.anchorMax = new Vector2(0.5f, 0.36f);
        continueRect.pivot = new Vector2(0.5f, 0.5f);
        continueRect.anchoredPosition = Vector2.zero;
        continueRect.sizeDelta = new Vector2(700, 60);
        Text continueText = continueObj.AddComponent<Text>();
        continueText.text = "Press any key to continue";
        continueText.font = GameplayFontProvider.GetFont();
        continueText.fontSize = 30;
        continueText.color = new Color(0.95f, 0.95f, 0.95f);
        continueText.alignment = TextAnchor.MiddleCenter;

        eliminationOverlay.SetActive(false);
    }

    public void HideEliminationAnnouncement()
    {
        if (isGameOver) return;
        if (eliminationOverlay != null)
            eliminationOverlay.SetActive(false);
    }

    public void UpdateBallSize(BallSize size)
    {
        // Ball size text hidden per design; keep it hidden whenever this is called
        if (ballSizeText != null)
            ballSizeText.gameObject.SetActive(false);

        // analytics (from analytics branch)
        if (analytics != null)
        {
            Debug.Log("Analytics: BallSizeChanged -> " + size);
            analytics.Send("BallSizeChanged", "0", size.ToString(), "N/A", "SizeChange");
        }
    }

    public void ShowWin()
    {
        ShowWin(0);
    }

    public void ShowWin(int winningPlayer)
    {
        roundComplete = true;
        // Clear collectibles on any win path (including Tutorial before the delayed win panel).
        CollectibleWallTriangle.DespawnAll();
        WallPlacementPowerUp.Instance?.CancelPowerUp();

        if (SceneManager.GetActiveScene().name == "Tutorial" && winPanel != null)
        {
            TutorialFieldLabels labels = FindFirstObjectByType<TutorialFieldLabels>();
            if (labels != null)
            {
                labels.ShowRimColorTipOverlay(() => ShowWinImmediate(winningPlayer));
                return;
            }
        }

        ShowWinImmediate(winningPlayer);
    }

    private void ShowWinImmediate(int winningPlayer)
    {
        CollectibleWallTriangle.DespawnAll();
        WallPlacementPowerUp.Instance?.CancelPowerUp();

        if (winPanel != null)
        {
            winPanel.SetActive(true);
            winPanel.transform.SetAsLastSibling();

            if (GameManager.Instance != null)
                GameManager.Instance.RecordLevelWinner(winningPlayer);

            bool isLastLevel = GameManager.Instance != null && GameManager.Instance.IsLastLevel();

            if (isLastLevel)
            {
                int p1 = GameManager.Instance.Player1Score;
                int p2 = GameManager.Instance.Player2Score;
                CreateFinalScoreScreen(winPanel.transform, p1, p2);
                CreateFinalScreenButtons(winPanel.transform);
            }
            else
            {
                bool isTutorial = SceneManager.GetActiveScene().name == "Tutorial";
                string message;
                if (isTutorial)
                    message = "Tutorial is done";
                else if (winningPlayer > 0)
                    message = "Player " + winningPlayer + " Wins!";
                else
                    message = "It's a Draw!";
                if (!isTutorial && GameManager.Instance != null)
                {
                    int p1 = GameManager.Instance.Player1Score;
                    int p2 = GameManager.Instance.Player2Score;
                    message += "\n\nCumulative: Player 1: " + p1 + " pts | Player 2: " + p2 + " pts";
                }
                Text targetText = winMessageText;
                if (targetText == null)
                    targetText = winPanel != null ? winPanel.GetComponentInChildren<Text>() : null;
                if (targetText != null)
                {
                    targetText.text = message;
                    targetText.gameObject.SetActive(true);
                    targetText.fontSize = 24;
                    targetText.color = new Color(0.95f, 0.95f, 0.9f);
                    targetText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    targetText.verticalOverflow = VerticalWrapMode.Overflow;
                    if (targetText.GetComponent<Outline>() == null)
                    {
                        Outline outline = targetText.gameObject.AddComponent<Outline>();
                        outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
                        outline.effectDistance = new Vector2(1f, 1f);
                    }
                    RectTransform rt = targetText.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0.5f, 0.62f);
                        rt.anchorMax = new Vector2(0.5f, 0.62f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = Vector2.zero;
                        rt.sizeDelta = new Vector2(700, 140);
                    }
                }
                CreateEndGameButtons(winPanel.transform, showNextLevel: true);
            }
        }

        if (analytics != null)
        {
            Debug.Log("Analytics: GameWon - Player " + winningPlayer);
            analytics.Send("GameWon", winningPlayer.ToString(), "N/A", "N/A", "Win");
        }
    }

    public void ShowGameOver(string message = "GAME OVER!\nNo Balls Remaining")
    {
        if (isGameOver)
            return;

        CollectibleWallTriangle.DespawnAll();
        WallPlacementPowerUp.Instance?.CancelPowerUp();
        isGameOver = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            CreateEndGameButtons(gameOverPanel.transform, showNextLevel: false);
        }

        if (gameOverText != null)
        {
            gameOverText.text = message;
        }

        // analytics (from analytics branch)
        if (analytics != null)
        {
            Debug.Log("Analytics: GameOver -> " + message);
            analytics.Send("GameOver", "0", "N/A", "0", message);
        }
    }

    public void PlayAgain()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private string GetNextLevelButtonLabel()
    {
        switch (SceneManager.GetActiveScene().name)
        {
            case "Tutorial": return "Level1";
            case "Level1": return "Level2";
            case "Level2": return "Level3";
            default: return "NEXT LEVEL";
        }
    }

    private void CreateEndGameButtons(Transform parent, bool showNextLevel)
    {
        if (parent == null || endButtonsCreated)
            return;

        endButtonsCreated = true;
        bool isTutorial = SceneManager.GetActiveScene().name == "Tutorial";

        float playAgainY = showNextLevel ? 0.2f : 0.15f;

        GameObject playAgainObj = new GameObject("Play Again Button");
        playAgainObj.transform.SetParent(parent, false);

        RectTransform playAgainRect = playAgainObj.AddComponent<RectTransform>();
        playAgainRect.anchorMin = new Vector2(0.5f, playAgainY);
        playAgainRect.anchorMax = new Vector2(0.5f, playAgainY);
        playAgainRect.pivot = new Vector2(0.5f, 0.5f);
        playAgainRect.anchoredPosition = Vector2.zero;
        playAgainRect.sizeDelta = new Vector2(220, 50);

        Image playAgainImage = playAgainObj.AddComponent<Image>();
        Button playAgainButton = playAgainObj.AddComponent<Button>();
        GameplayButtonStyle.Apply(playAgainImage, playAgainButton, new Color(0.2f, 0.6f, 0.3f));
        playAgainButton.onClick.AddListener(PlayAgain);

        GameObject playAgainTextObj = new GameObject("Text");
        playAgainTextObj.transform.SetParent(playAgainObj.transform, false);

        RectTransform playAgainTextRect = playAgainTextObj.AddComponent<RectTransform>();
        playAgainTextRect.anchorMin = Vector2.zero;
        playAgainTextRect.anchorMax = Vector2.one;
        playAgainTextRect.offsetMin = Vector2.zero;
        playAgainTextRect.offsetMax = Vector2.zero;

        Text playAgainText = playAgainTextObj.AddComponent<Text>();
        playAgainText.text = isTutorial ? "Play tutorial again" : "PLAY AGAIN";
        playAgainText.font = GameplayFontProvider.GetFont();
        playAgainText.fontSize = 26;
        GameplayButtonStyle.ApplyLabel(playAgainText, playAgainTextRect);
        playAgainText.alignment = TextAnchor.MiddleCenter;

        if (showNextLevel && GameManager.Instance != null && GameManager.Instance.GetNextLevelBuildIndex() >= 0)
        {
            GameObject nextLevelObj = new GameObject("Next Level Button");
            nextLevelObj.transform.SetParent(parent, false);

            RectTransform nextLevelRect = nextLevelObj.AddComponent<RectTransform>();
            nextLevelRect.anchorMin = new Vector2(0.5f, 0.1f);
            nextLevelRect.anchorMax = new Vector2(0.5f, 0.1f);
            nextLevelRect.pivot = new Vector2(0.5f, 0.5f);
            nextLevelRect.anchoredPosition = Vector2.zero;
            nextLevelRect.sizeDelta = isTutorial ? new Vector2(280, 58) : new Vector2(220, 50);

            Image nextLevelImage = nextLevelObj.AddComponent<Image>();
            Button nextLevelButton = nextLevelObj.AddComponent<Button>();
            GameplayButtonStyle.Apply(nextLevelImage, nextLevelButton, new Color(0.25f, 0.5f, 0.75f));
            nextLevelButton.interactable = true;
            nextLevelButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.LoadNextLevel();
            });

            GameObject nextLevelTextObj = new GameObject("Text");
            nextLevelTextObj.transform.SetParent(nextLevelObj.transform, false);

            RectTransform nextLevelTextRect = nextLevelTextObj.AddComponent<RectTransform>();
            nextLevelTextRect.anchorMin = Vector2.zero;
            nextLevelTextRect.anchorMax = Vector2.one;
            nextLevelTextRect.offsetMin = Vector2.zero;
            nextLevelTextRect.offsetMax = Vector2.zero;

            Text nextLevelText = nextLevelTextObj.AddComponent<Text>();
            nextLevelText.text = GetNextLevelButtonLabel();
            nextLevelText.font = GameplayFontProvider.GetFont();
            nextLevelText.fontSize = 26;
            GameplayButtonStyle.ApplyLabel(nextLevelText, nextLevelTextRect);
            nextLevelText.alignment = TextAnchor.MiddleCenter;
        }
    }

    private bool finalButtonsCreated = false;

    private void CreateFinalScoreScreen(Transform parent, int player1Score, int player2Score)
    {
        if (parent == null) return;

        // Hide default win message so our layout is clean
        if (winMessageText != null) winMessageText.gameObject.SetActive(false);
        Text existingText = parent.GetComponentInChildren<Text>();
        if (existingText != null) existingText.gameObject.SetActive(false);

        // Determine the overall winner and choose highlight colors
        string winnerText;
        Color winnerColor;
        Color p1Color = Color.white;
        Color p2Color = Color.white;
        Color gold = new Color(1f, 0.84f, 0f);

        if (player1Score > player2Score)
        {
            winnerText = "PLAYER 1 WINS!";
            winnerColor = new Color(0.2f, 1f, 0.4f);
            p1Color = gold;
        }
        else if (player2Score > player1Score)
        {
            winnerText = "PLAYER 2 WINS!";
            winnerColor = new Color(0.2f, 1f, 0.4f);
            p2Color = gold;
        }
        else
        {
            winnerText = "IT'S A TIE!";
            winnerColor = new Color(0.4f, 0.8f, 1f);
        }

        // Winner banner — top, large, and prominent
        CreateScoreText(parent, winnerText, 0.72f, 52, winnerColor);

        // Sub-header: FINAL SCORES
        CreateScoreText(parent, "FINAL SCORES", 0.57f, 32, new Color(1f, 0.9f, 0.4f));

        // Player 1 score — highlighted gold if winner
        CreateScoreText(parent, "Player 1: " + player1Score + " pts", 0.44f, 40, p1Color);

        // Player 2 score — highlighted gold if winner
        CreateScoreText(parent, "Player 2: " + player2Score + " pts", 0.33f, 40, p2Color);
    }

    private void CreateScoreText(Transform parent, string text, float anchorY, int fontSize, Color color)
    {
        GameObject obj = new GameObject("ScoreText");
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(600, 60);

        Text t = obj.AddComponent<Text>();
        t.text = text;
        t.font = GameplayFontProvider.GetFont();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
    }

    private void CreateFinalScreenButtons(Transform parent)
    {
        if (parent == null || finalButtonsCreated)
            return;

        finalButtonsCreated = true;

        CreateButton(parent, "Restart Game", 0.2f, () =>
        {
            hasSelectedLaunchOption = false;
            if (GameManager.Instance != null)
                GameManager.Instance.RestartGame();
        });
    }

    private void CreateButton(Transform parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(label + " Button");
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(220, 50);

        Image img = btnObj.AddComponent<Image>();
        Button btn = btnObj.AddComponent<Button>();
        GameplayButtonStyle.Apply(img, btn, new Color(0.2f, 0.6f, 0.3f));
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textObj.AddComponent<Text>();
        txt.text = label.ToUpper();
        txt.font = GameplayFontProvider.GetFont();
        txt.fontSize = 24;
        GameplayButtonStyle.ApplyLabel(txt, textRect);
        txt.alignment = TextAnchor.MiddleCenter;
    }
}
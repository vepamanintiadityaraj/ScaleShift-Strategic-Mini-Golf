using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages alternate turns for two players. Setup: 1) Duplicate the Ball in the scene.
/// 2) Set one BallController Player Index to 1, the other to 2. 3) Assign both to this component.
/// 4) Add a UI Text for "Turn: Player X" and assign GameUI's Turn Text.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Player Balls (assign both in Inspector)")]
    [SerializeField] private BallController player1Ball;
    [SerializeField] private BallController player2Ball;

    private int currentPlayer = 1;
    private bool currentBallWasMoving = false;
    private bool currentPlayerHasShotThisTurn = false;
    private bool holeSinkSwitchPending = false;
    private int player1ShotsToSink = -1;
    private int player2ShotsToSink = -1;
    private GameUI gameUI;

    // Fairness rule: only the player who shoots SECOND in the level is entitled
    // to a draw-chance shot when the first-shooter sinks. If the second-shooter
    // sinks first they win outright — the first-shooter has already had more turns.
    private int startingPlayer = 1; // set in Start()
    private int firstSinkPlayer = 0;
    private bool drawChanceActive = false;

    public int CurrentPlayer => currentPlayer;
    public bool IsTwoPlayerMode => player1Ball != null && player2Ball != null;

    public bool IsMyTurn(int playerIndex)
    {
        if (!IsTwoPlayerMode) return true;
        return currentPlayer == playerIndex;
    }

    public BallController GetCurrentBall()
    {
        return currentPlayer == 1 ? player1Ball : player2Ball;
    }

    public BallController GetPlayerBall(int playerIndex)
    {
        return playerIndex == 1 ? player1Ball : player2Ball;
    }

    private bool IsPlayerEliminated(int playerIndex)
    {
        BallController ball = GetPlayerBall(playerIndex);
        return ball == null || ball.IsEliminated;
    }

    /// <summary>
    /// Who opens the level: Tutorial → P1; Level1 → P1; then alternates (L2 P2, L3 P1, …).
    /// </summary>
    private static int GetStartingPlayerForCurrentScene()
    {
        if (GameManager.Instance == null)
            return 1;
        int idx = GameManager.Instance.GetCurrentLevelIndex();
        if (idx < 0)
            return 1;
        if (idx == 0)
            return 1;
        // idx 1 = Level1 → P1; idx 2 → P2; idx 3 → P1
        return ((idx - 1) % 2 == 0) ? 1 : 2;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        gameUI = FindFirstObjectByType<GameUI>();
        if (IsTwoPlayerMode && gameUI != null)
        {
            int starter = GetStartingPlayerForCurrentScene();
            startingPlayer = starter;
            currentPlayer = starter;
            currentBallWasMoving = false;
            currentPlayerHasShotThisTurn = false;
            int fallback = GameManager.GetMaxShotsPerPlayerForScene(SceneManager.GetActiveScene().name);
            gameUI.UpdatePlayerBalls(1, player1Ball != null ? player1Ball.BallsRemaining : fallback);
            gameUI.UpdatePlayerBalls(2, player2Ball != null ? player2Ball.BallsRemaining : fallback);
            gameUI.SetTurn(starter);
        }
    }

    private void Update()
    {
        if (!IsTwoPlayerMode || gameUI == null || gameUI.IsRoundComplete)
            return;

        BallController currentBall = GetCurrentBall();
        if (currentBall == null) return;

        // Lethal wall elimination is handled directly by EliminatePlayer — skip here
        if (currentBall.IsEliminated) return;

        // Ball sunk into hole: inactive but not eliminated — switch turn after a short delay
        if (!currentBall.gameObject.activeInHierarchy)
        {
            if (!holeSinkSwitchPending)
            {
                holeSinkSwitchPending = true;
                StartCoroutine(SwitchTurnAfterDelay(2f));
            }
            return;
        }

        if (currentBall.IsMoving || currentBall.IsSinking)
        {
            currentBallWasMoving = true;
        }
        else
        {
            if (currentBallWasMoving && currentPlayerHasShotThisTurn)
            {
                if (!WallPlacementPowerUp.BlocksTurnSwitchFor(currentPlayer))
                {
                    // New scoring rule: the trailing player just finished their
                    // single "draw chance" shot without sinking — first-to-sink wins.
                    if (drawChanceActive && currentPlayer != firstSinkPlayer)
                    {
                        drawChanceActive = false;
                        gameUI?.ShowWin(firstSinkPlayer);
                        return;
                    }

                    if (currentBall.BallsRemaining <= 0)
                        SwitchTurnBecauseCurrentOut();
                    else
                        SwitchTurn();
                }
            }
            if (!WallPlacementPowerUp.BlocksTurnSwitchFor(currentPlayer))
                currentBallWasMoving = false;
        }
    }

    private void SwitchTurn()
    {
        holeSinkSwitchPending = false;
        int otherPlayer = currentPlayer == 1 ? 2 : 1;

        // If the other player is eliminated, the current player keeps shooting alone
        if (IsPlayerEliminated(otherPlayer))
        {
            currentPlayerHasShotThisTurn = false;
            return;
        }

        BallController otherBall = GetPlayerBall(otherPlayer);
        if (otherBall != null && otherBall.BallsRemaining <= 0)
        {
            BallController currentBall = GetCurrentBall();
            if (currentBall == null || currentBall.BallsRemaining <= 0)
            {
                if (player1ShotsToSink >= 0 || player2ShotsToSink >= 0)
                {
                    ResolveLevelByShots();
                    return;
                }
                gameUI?.ShowGameOver("GAME OVER!\nBoth players ran out of balls!");
                return;
            }
        }

        currentPlayer = otherPlayer;
        currentPlayerHasShotThisTurn = false;
        gameUI?.SetTurn(currentPlayer);
    }

    public void NotifyCurrentPlayerShot()
    {
        currentPlayerHasShotThisTurn = true;
    }

    public void NotifyBallCountChanged(int playerIndex, int ballsRemaining)
    {
        if (gameUI != null && IsTwoPlayerMode)
            gameUI.UpdatePlayerBalls(playerIndex, ballsRemaining);
    }

    public void RecordPlayerSunk(int playerIndex, int shotsTaken)
    {
        if (playerIndex == 1) player1ShotsToSink = shotsTaken;
        else player2ShotsToSink = shotsTaken;

        int otherPlayer = playerIndex == 1 ? 2 : 1;
        int otherShots = otherPlayer == 1 ? player1ShotsToSink : player2ShotsToSink;

        if (otherShots >= 0)
        {
            // Both players have sunk — compare shot counts to decide.
            drawChanceActive = false;
            ResolveLevelByShots();
            return;
        }

        if (IsPlayerEliminated(otherPlayer))
        {
            gameUI?.ShowWin(playerIndex);
            return;
        }

        BallController otherBall = GetPlayerBall(otherPlayer);
        if (otherBall != null && otherBall.BallsRemaining <= 0)
        {
            gameUI?.ShowWin(playerIndex);
            return;
        }

        // Only the player who shoots SECOND is entitled to a draw-chance shot.
        // If the first-shooter (startingPlayer) sinks first, give the second
        // shooter their corresponding turn as a draw chance.
        // If the second-shooter sinks first, they already won in fewer turns —
        // the first-shooter gets no draw chance.
        if (playerIndex != startingPlayer)
        {
            // Second-shooter sank first → outright win, no draw chance for P1.
            gameUI?.ShowWin(playerIndex);
            return;
        }

        // First-shooter sank first. Arm the 1-shot draw chance for the second shooter.
        firstSinkPlayer = playerIndex;
        drawChanceActive = true;

        // Reflect the new stakes in the UI: the trailing player effectively has
        // only one shot left (to tie), regardless of how many balls they had.
        gameUI?.SetDrawChanceUI(otherPlayer);

        gameUI?.ShowPlayerSunk(playerIndex);
    }

    private void ResolveLevelByShots()
    {
        if (player1ShotsToSink >= 0 && player2ShotsToSink >= 0)
        {
            if (player1ShotsToSink < player2ShotsToSink)
                gameUI?.ShowWin(1);
            else if (player2ShotsToSink < player1ShotsToSink)
                gameUI?.ShowWin(2);
            else
                gameUI?.ShowWin(0);
        }
        else if (player1ShotsToSink >= 0)
            gameUI?.ShowWin(1);
        else if (player2ShotsToSink >= 0)
            gameUI?.ShowWin(2);
    }

    public void SwitchTurnBecauseCurrentOut()
    {
        int other = currentPlayer == 1 ? 2 : 1;

        int otherSinkShots = other == 1 ? player1ShotsToSink : player2ShotsToSink;
        if (otherSinkShots >= 0)
        {
            gameUI?.ShowWin(other);
            return;
        }

        if (IsPlayerEliminated(other))
        {
            int currentSinkShots = currentPlayer == 1 ? player1ShotsToSink : player2ShotsToSink;
            if (currentSinkShots >= 0)
            {
                gameUI?.ShowWin(currentPlayer);
                return;
            }
            gameUI?.ShowGameOver("GAME OVER!\nNo Balls Remaining");
            return;
        }

        BallController otherBall = GetPlayerBall(other);
        if (otherBall != null && otherBall.BallsRemaining <= 0)
        {
            int currentSinkShots = currentPlayer == 1 ? player1ShotsToSink : player2ShotsToSink;
            if (currentSinkShots >= 0)
            {
                gameUI?.ShowWin(currentPlayer);
                return;
            }
            gameUI?.ShowGameOver("GAME OVER!\nNo Balls Remaining");
            return;
        }

        currentPlayer = other;
        currentPlayerHasShotThisTurn = false;
        gameUI?.SetTurn(currentPlayer);
    }

    public void EliminatePlayer(int playerIndex)
    {
        BallController ball = GetPlayerBall(playerIndex);
        if (ball == null || ball.IsEliminated) return;

        ball.Eliminate();

        // Always clear map collectibles when someone is eliminated (not only when GameUI runs elimination flow).
        CollectibleWallTriangle.DespawnAll();
        WallPlacementPowerUp.Instance?.CancelPowerUp();

        gameUI?.UpdatePlayerBalls(playerIndex, 0);

        int otherPlayer = playerIndex == 1 ? 2 : 1;
        BallController otherBall = GetPlayerBall(otherPlayer);

        // If the other player already sunk (won), show that they won the game
        if (otherBall != null && !otherBall.gameObject.activeInHierarchy && !otherBall.IsEliminated)
        {
            gameUI?.ShowWin(otherPlayer);
            return;
        }

        if (!IsPlayerEliminated(otherPlayer))
        {
            // Do NOT set currentPlayer here — keep it on the eliminated player so the
            // surviving ball's Update() returns early (IsMyTurn = false) while the
            // elimination overlay is visible. currentPlayer is set in ShowTurnAfterDelay
            // only after the overlay is dismissed, so the key-press that dismisses it
            // cannot simultaneously start the power bar charging.
            gameUI?.ShowPlayerEliminated(playerIndex, otherPlayer);
            StartCoroutine(ShowTurnAfterDelay(otherPlayer, 2f));
        }
        else
        {
            gameUI?.ShowGameOver("GAME OVER!\nBoth players were eliminated!");
        }
    }

    private IEnumerator ShowTurnAfterDelay(int player, float _)
    {
        // Ignore any key that was held when the overlay appeared
        yield return new WaitForSeconds(0.2f);
        while (!Input.anyKeyDown)
            yield return null;
        gameUI?.HideEliminationAnnouncement();
        // Activate the surviving player AFTER the dismissal key-press has been consumed
        // by Unity's Update phase (coroutines resume at end-of-frame, after Update).
        // This prevents the Space/click that closes the overlay from also starting a charge.
        currentPlayer = player;
        currentPlayerHasShotThisTurn = false;
        gameUI?.SetTurn(player);
    }

    private IEnumerator SwitchTurnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        holeSinkSwitchPending = false;
        SwitchTurn();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

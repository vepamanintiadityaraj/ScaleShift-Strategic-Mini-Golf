using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent manager for level progression and cumulative scoring across scenes.
/// Level order: Tutorial (0) -> Level1 (1) -> Level2 (2) -> Level3 (3)
/// Points: Level1=10, Level2=20, Level3=30. Tutorial awards 0.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private int player1Score;
    private int player2Score;

    private static readonly string[] LevelSceneNames = { "Tutorial", "Level1", "Level2", "Level3" };
    private static readonly int[] LevelPoints = { 0, 10, 20, 30 }; // Tutorial=0, L1=10, L2=20, L3=30

    public int Player1Score => player1Score;
    public int Player2Score => player2Score;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Always start at Tutorial when pressing Play, regardless of which scene is open in editor
        if (SceneManager.GetActiveScene().name != "Tutorial")
        {
            SceneManager.LoadScene("Tutorial");
        }
    }

    /// <summary>
    /// Records the winner for the current level and adds points. Call when a level is completed.
    /// </summary>
    public void RecordLevelWinner(int winningPlayer)
    {
        int levelIndex = GetCurrentLevelIndex();
        if (levelIndex < 0 || levelIndex >= LevelPoints.Length)
            return;

        int points = LevelPoints[levelIndex];
        if (winningPlayer == 1)
            player1Score += points;
        else if (winningPlayer == 2)
            player2Score += points;
    }

    /// <summary>
    /// Returns the build index of the current scene if it's a level scene, else -1.
    /// </summary>
    public int GetCurrentLevelIndex()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < LevelSceneNames.Length; i++)
        {
            if (LevelSceneNames[i] == sceneName)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns true if the current scene is the last level (Level3).
    /// </summary>
    public bool IsLastLevel()
    {
        return GetCurrentLevelIndex() == LevelSceneNames.Length - 1;
    }

    /// <summary>
    /// Returns the build index of the next level scene, or -1 if no next level.
    /// </summary>
    public int GetNextLevelBuildIndex()
    {
        int current = GetCurrentLevelIndex();
        if (current < 0 || current >= LevelSceneNames.Length - 1)
            return -1;
        return current + 1;
    }

    /// <summary>
    /// Loads the next level. Call only when a next level exists.
    /// </summary>
    public void LoadNextLevel()
    {
        int nextIndex = GetNextLevelBuildIndex();
        if (nextIndex >= 0)
            SceneManager.LoadScene(nextIndex);
    }

    /// <summary>
    /// Resets scores and loads Tutorial.
    /// </summary>
    public void RestartGame()
    {
        player1Score = 0;
        player2Score = 0;
        SceneManager.LoadScene("Tutorial");
    }

    /// <summary>
    /// Loads Tutorial (optionally without resetting scores - for "Back to Tutorial").
    /// </summary>
    public void GoToTutorial(bool resetScores = true)
    {
        if (resetScores)
        {
            player1Score = 0;
            player2Score = 0;
        }
        SceneManager.LoadScene("Tutorial");
    }

    /// <summary>
    /// Returns the build index for Tutorial scene (used when scenes are in build order).
    /// </summary>
    public static int GetTutorialBuildIndex()
    {
        return 0;
    }

    /// <summary>
    /// Shots per player: Tutorial 15, Level1 5, Level2 10, Level3 12.
    /// </summary>
    public static int GetMaxShotsPerPlayerForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "Level1": return 5;
            case "Level2": return 10;
            case "Level3": return 12;
            case "Tutorial":
            default:
                return 15;
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tutorial scene: optional spawned practice walls stay off; in-level labels explain mechanics.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Tutorial walls (spawned if not in scene; kept inactive)")]
    [SerializeField] private Vector2 blueWallPosition = new Vector2(-2.5f, 0.5f);
    [SerializeField] private Vector2 redWallPosition = new Vector2(3f, -0.5f);
    [SerializeField] private float wallWidth = 1.5f;
    [SerializeField] private float wallHeight = 0.4f;

    private GameObject blueWallInstance;
    private GameObject redWallInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "Tutorial") return;
        EnsureTutorialWallsExist();
    }

    public void BeginTutorial()
    {
        if (SceneManager.GetActiveScene().name != "Tutorial") return;
        DeactivateTutorialWalls();
    }

    private void DeactivateTutorialWalls()
    {
        if (blueWallInstance != null) blueWallInstance.SetActive(false);
        if (redWallInstance != null) redWallInstance.SetActive(false);
    }

    private void EnsureTutorialWallsExist()
    {
        TutorialWall[] walls = FindObjectsByType<TutorialWall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TutorialWall w in walls)
        {
            if (w.Type == TutorialWall.WallType.Blue && blueWallInstance == null) blueWallInstance = w.gameObject;
            else if (w.Type == TutorialWall.WallType.Red && redWallInstance == null) redWallInstance = w.gameObject;
        }
        if (blueWallInstance == null) blueWallInstance = CreateTutorialWall(blueWallPosition, TutorialWall.WallType.Blue);
        if (redWallInstance == null) redWallInstance = CreateTutorialWall(redWallPosition, TutorialWall.WallType.Red);
        DeactivateTutorialWalls();
    }

    private GameObject CreateTutorialWall(Vector2 position, TutorialWall.WallType type)
    {
        string name = type == TutorialWall.WallType.Blue ? "Tutorial Blue Wall" : "Tutorial Red Wall";
        Color color = type == TutorialWall.WallType.Blue ? new Color(0.2f, 0.4f, 0.9f) : new Color(0.9f, 0.25f, 0.2f);

        GameObject wall = new GameObject(name);
        wall.transform.position = position;

        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRectSprite(color);
        sr.sortingOrder = 2;
        wall.transform.localScale = new Vector3(wallWidth, wallHeight, 1f);

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        Rigidbody2D rb = wall.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        TutorialWall tw = wall.AddComponent<TutorialWall>();
        tw.SetWallType(type);
        return wall;
    }

    private static Sprite CreateRectSprite(Color color)
    {
        int w = 32, h = 8;
        Texture2D tex = new Texture2D(w, h);
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

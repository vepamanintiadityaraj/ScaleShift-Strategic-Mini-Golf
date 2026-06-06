using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fills the orthographic camera with a solid green plane behind Tutorial / Level scenes (no grass texture).
/// </summary>
public static class GrassBackgroundBootstrap
{
    private const string RootName = "GrassBackground_Runtime";
    private static readonly Color BackgroundGreen = new Color(0.22f, 0.58f, 0.26f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        TryCreateBackground();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateBackground();
    }

    private static void TryCreateBackground()
    {
        if (GameObject.Find(RootName) != null)
            return;

        string scene = SceneManager.GetActiveScene().name;
        if (scene != "Tutorial" && scene != "Level1" && scene != "Level2" && scene != "Level3")
            return;

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f,
            0,
            SpriteMeshType.FullRect);

        GameObject root = new GameObject(RootName);
        var sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = BackgroundGreen;
        sr.sortingOrder = -500;
        sr.drawMode = SpriteDrawMode.Simple;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;

        root.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        root.transform.localScale = new Vector3(viewW, viewH, 1f);
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu command to bake stone tiling onto all structural walls in Tutorial + Levels.
/// </summary>
public static class StoneWallEditorMenu
{
    private const string TileAssetPath = "Assets/Resources/StoneWall/WallBrickSeamless.png";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Tutorial.unity",
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level2.unity",
        "Assets/Scenes/Level3.unity"
    };

    [MenuItem("Golf/Apply stone texture to all walls (save scenes)")]
    public static void ApplyStoneToAllWalls()
    {
        Sprite tile = AssetDatabase.LoadAssetAtPath<Sprite>(TileAssetPath);
        if (tile == null)
        {
            EditorUtility.DisplayDialog(
                "Stone walls",
                "Could not load sprite at:\n" + TileAssetPath + "\n\nImport WallBrickSeamless.png first.",
                "OK");
            return;
        }

        int total = 0;
        foreach (string scenePath in ScenePaths)
        {
            if (!File.Exists(scenePath))
                continue;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int n = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                n += ApplyRecursive(root.transform, tile);

            total += n;
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[StoneWall] Saved stone texture on {total} wall object(s) across scenes.");
        EditorUtility.DisplayDialog(
            "Stone walls",
            $"Updated {total} wall object(s) and saved Tutorial + Level1–3.",
            "OK");
    }

    private static int ApplyRecursive(Transform t, Sprite tile)
    {
        int count = 0;
        if (StoneWallApplier.IsStructuralWallName(t.name))
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Vector2 worldSize = new Vector2(sr.bounds.size.x, sr.bounds.size.y);

                Undo.RecordObject(sr.transform, "Stone walls");
                Undo.RecordObject(sr, "Stone walls");

                sr.sprite = tile;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.color = Color.white;
                sr.tileMode = SpriteTileMode.Continuous;
                t.localScale = Vector3.one;
                sr.size = worldSize;

                var box = t.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    Undo.RecordObject(box, "Stone walls");
                    box.size = worldSize;
                    box.offset = Vector2.zero;
                }

                count++;
            }
        }

        for (int i = 0; i < t.childCount; i++)
            count += ApplyRecursive(t.GetChild(i), tile);

        return count;
    }
}
#endif

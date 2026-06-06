using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Red shrink zones and diagonal red walls: same <b>tiled</b> rendering as stone walls, but
/// <see cref="StoneWallApplier.ApplyTiledWallSpritePreserveOrientation"/> keeps rotation/scale so direction matches the scene.
/// Art: <c>Resources/RedZoneCenter_Crystal</c> (volcanic).
/// </summary>
public static class RedZoneCenterSpriteApplier
{
    private const string RedZoneSpriteResourcePath = "RedZoneCenter_Crystal";
    private static Sprite _redZoneSprite;
    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!_hooked)
        {
            SceneManager.sceneLoaded += (_, __) => ApplyToActiveScene();
            _hooked = true;
        }

        ApplyToActiveScene();
    }

    private static void ApplyToActiveScene()
    {
        if (_redZoneSprite == null)
            _redZoneSprite = Resources.Load<Sprite>(RedZoneSpriteResourcePath);
        if (_redZoneSprite == null)
            return;

        SizeZone[] zones = Object.FindObjectsByType<SizeZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            SizeZone zone = zones[i];
            if (zone == null)
                continue;

            bool isShrinkZone = zone.ZoneType == SizeZoneType.Shrink;
            bool isDiagonalRedWall = zone.name.StartsWith("Diagonal Red Wall");
            if (!isShrinkZone && !isDiagonalRedWall)
                continue;

            StoneWallApplier.ApplyTiledWallSpritePreserveOrientation(zone.transform, _redZoneSprite, Color.white);
        }
    }
}

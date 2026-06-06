using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Blue grow zones and angled blue wall junctions: tiled marble art (dark-blue tinted)
/// using <see cref="StoneWallApplier.ApplyTiledWallSpritePreserveOrientation"/> —
/// same pipeline as red zones but with <c>Resources/BlueZoneCenter_Crystal</c>.
/// </summary>
public static class BlueZoneCenterSpriteApplier
{
    private const string BlueZoneSpriteResourcePath = "BlueZoneCenter_Crystal";
    private static Sprite _blueZoneSprite;
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
        if (_blueZoneSprite == null)
            _blueZoneSprite = Resources.Load<Sprite>(BlueZoneSpriteResourcePath);
        if (_blueZoneSprite == null)
            return;

        SizeZone[] zones = Object.FindObjectsByType<SizeZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            SizeZone zone = zones[i];
            if (zone == null)
                continue;

            bool isGrowZone = zone.ZoneType == SizeZoneType.Grow;
            bool isAngledBlueJunction = zone.name.StartsWith("Angled Blue Wall Junction");
            if (!isGrowZone && !isAngledBlueJunction)
                continue;

            StoneWallApplier.ApplyTiledWallSpritePreserveOrientation(zone.transform, _blueZoneSprite, Color.white);
        }
    }
}

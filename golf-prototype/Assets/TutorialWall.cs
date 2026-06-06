using UnityEngine;

/// <summary>
/// Tutorial walls: blue = ball grows, red = ball shrinks. On hit, apply size change; wall disappears.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialWall : MonoBehaviour
{
    public enum WallType { Blue, Red }

    [SerializeField] private WallType wallType = WallType.Blue;

    public WallType Type => wallType;

    public void SetWallType(WallType type) { wallType = type; }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHitByBall(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHitByBall(other.gameObject);
    }

    private void TryHitByBall(GameObject other)
    {
        if (other.GetComponent<BallController>() == null) return;

        BallSizeController sizeController = other.GetComponent<BallSizeController>();
        if (sizeController != null)
        {
            if (wallType == WallType.Blue)
                sizeController.Grow();
            else
                sizeController.Shrink();
        }

        gameObject.SetActive(false);
    }
}

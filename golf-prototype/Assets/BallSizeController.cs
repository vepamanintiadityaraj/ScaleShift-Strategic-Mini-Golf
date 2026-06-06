using UnityEngine;

public enum BallSize
{
    Small,
    Normal,
    Large
}

public class BallSizeController : MonoBehaviour
{
    [Header("Size Settings")]
    [SerializeField] private float smallScale = 0.5f;
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float largeScale = 1.5f;
    [SerializeField] private float sizeChangeCooldown = 0.2f;

    private BallSize currentSize = BallSize.Normal;
    private CircleCollider2D circleCollider;
    private float lastSizeChangeTime = -999f;

    public BallSize CurrentSize => currentSize;

    /// <summary>
    /// The canonical scale for a given BallSize. HoleController (and anything
    /// else that needs to match ball sizing) should use this instead of keeping
    /// its own copy of the values.
    /// </summary>
    public float GetScaleFor(BallSize size)
    {
        switch (size)
        {
            case BallSize.Small:  return smallScale;
            case BallSize.Large:  return largeScale;
            default:              return normalScale;
        }
    }

    private void Awake()
    {
        circleCollider = GetComponent<CircleCollider2D>();
        SetSize(BallSize.Normal);
    }

    public void SetSize(BallSize newSize)
    {
        if (Time.time - lastSizeChangeTime < sizeChangeCooldown)
            return;

        currentSize = newSize;
        lastSizeChangeTime = Time.time;

        float targetScale = normalScale;
        switch (currentSize)
        {
            case BallSize.Small:
                targetScale = smallScale;
                break;
            case BallSize.Normal:
                targetScale = normalScale;
                break;
            case BallSize.Large:
                targetScale = largeScale;
                break;
        }

        transform.localScale = Vector3.one * targetScale;

        if (circleCollider != null)
        {
            circleCollider.radius = 0.5f;
        }

        GameUI gameUI = FindFirstObjectByType<GameUI>();
        if (gameUI != null)
        {
            gameUI.UpdateBallSize(currentSize);
        }
    }

    public void Shrink()
    {
        if (currentSize == BallSize.Normal)
            SetSize(BallSize.Small);
        else if (currentSize == BallSize.Large)
            SetSize(BallSize.Normal);
    }

    public void Grow()
    {
        if (currentSize == BallSize.Normal)
            SetSize(BallSize.Large);
        else if (currentSize == BallSize.Small)
            SetSize(BallSize.Normal);
    }
}

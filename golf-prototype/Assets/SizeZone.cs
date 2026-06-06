using UnityEngine;

public enum SizeZoneType
{
    Shrink,
    Grow
}

public class SizeZone : MonoBehaviour
{
    [SerializeField] private SizeZoneType zoneType = SizeZoneType.Shrink;
    public SizeZoneType ZoneType => zoneType;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplySizeChange(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplySizeChange(other.gameObject);
    }

    private void TryApplySizeChange(GameObject go)
    {
        BallSizeController ballSize = go.GetComponent<BallSizeController>();
        if (ballSize != null)
            ApplySizeChange(ballSize);
    }

    private void ApplySizeChange(BallSizeController ballSize)
    {
        switch (zoneType)
        {
            case SizeZoneType.Shrink:
                ballSize.Shrink();
                break;
            case SizeZoneType.Grow:
                ballSize.Grow();
                break;
        }
    }

    public void SetZoneType(SizeZoneType type)
    {
        zoneType = type;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class PowerBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform pointer;
    [SerializeField] private GameObject powerBarPanel;
    [SerializeField] private RectTransform barRect;

    private void Awake()
    {
        if (powerBarPanel != null)
        {
            powerBarPanel.SetActive(false);
        }
    }

    public void Show()
    {
        if (powerBarPanel != null)
        {
            powerBarPanel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (powerBarPanel != null)
        {
            powerBarPanel.SetActive(false);
        }
    }

    public void SetPower(float normalizedPower)
    {
        if (pointer != null && barRect != null)
        {
            float barHeight = barRect.sizeDelta.y;
            float yPos = normalizedPower * barHeight - (barHeight / 2f);
            pointer.anchoredPosition = new Vector2(0, yPos);
        }
    }
}

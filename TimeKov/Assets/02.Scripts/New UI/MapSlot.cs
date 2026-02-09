using UnityEngine;
using UnityEngine.EventSystems; 

public class MapSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("Map Info")]
    public string sceneName;

    [Header("Visuals")]
    public GameObject highlightBorder; 
    public GameObject lockIcon;      
    public bool isLocked = false; 

    private MapSelectManager manager;

    void Start()
    {
        manager = GetComponentInParent<MapSelectManager>();

        if (highlightBorder != null) highlightBorder.SetActive(false);

        if (lockIcon != null) lockIcon.SetActive(isLocked);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null) manager = GetComponentInParent<MapSelectManager>();

        if (manager != null)
        {
            manager.SelectMap(this);

            if (eventData.clickCount == 2)
            {
                if (!isLocked)
                {
                    manager.OnClickSelectButton();
                }
            }
        }
    }
    public void SetSelected(bool isSelected)
    {
        if (highlightBorder != null) highlightBorder.SetActive(isSelected);
    }
}
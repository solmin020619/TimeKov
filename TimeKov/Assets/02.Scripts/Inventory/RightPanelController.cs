using UnityEngine;

public class RightPanelController : MonoBehaviour
{
    public GameObject warehousePanel;
    public GameObject lootPanel;
    public GameObject shopPanel;

    public enum PanelType
    {
        None,
        Warehouse,
        Loot,
        Shop
    }

    public void ShowPanel(PanelType type)
    {
        warehousePanel.SetActive(false);
        lootPanel.SetActive(false);
        shopPanel.SetActive(false);

        switch (type)
        {
            case PanelType.Warehouse:
                warehousePanel.SetActive(true);
                break;

            case PanelType.Loot:
                lootPanel.SetActive(true);
                break;

            case PanelType.Shop:
                shopPanel.SetActive(true);
                break;
        }
    }
}
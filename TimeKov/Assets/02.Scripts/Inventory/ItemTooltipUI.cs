using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    public static ItemTooltipUI Instance;

    [Header("Root")]
    public GameObject root;

    [Header("UI")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descText;

    [Header("Offset")]
    public Vector2 screenOffset = new Vector2(18f, -18f);

    private RectTransform rootRect;

    private void Awake()
    {
        Instance = this;

        if (root != null)
        {
            rootRect = root.GetComponent<RectTransform>();
            root.SetActive(false);
        }
    }

    private void Update()
    {
        if (root != null && root.activeSelf)
        {
            Vector2 pos = (Vector2)Input.mousePosition + screenOffset;
            root.transform.position = pos;
        }
    }

    public void Show(SlotInfo slot)
    {
        if (slot == null || slot.slotIndex == 0 || slot.itemCount <= 0)
        {
            Hide();
            return;
        }

        if (!DataStore.IsLoaded)
            DataStore.LoadAll();

        ItemRow item = DataStore.GetItem(slot.slotIndex);
        if (item == null)
        {
            Hide();
            return;
        }

        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (nameText != null)
            nameText.text = item.itemName;

        if (descText != null)
            descText.text = item.description;

        if (iconImage != null)
        {
            Sprite sprite = Resources.Load<Sprite>("Icon/" + slot.slotIndex);
            iconImage.sprite = sprite;
            iconImage.enabled = (sprite != null);
        }

        Vector2 pos = (Vector2)Input.mousePosition + screenOffset;
        if (root != null)
            root.transform.position = pos;
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }
}
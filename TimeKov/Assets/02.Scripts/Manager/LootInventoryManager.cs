using System.Collections.Generic;
using UnityEngine;

public class LootInventoryManager : MonoBehaviour
{
    public Transform contentTransform;
    public GameObject slotPrefab;

    private List<GameObject> spawnedSlots = new List<GameObject>();

    public void ClearSlots()
    {
        foreach (var slot in spawnedSlots)
        {
            Destroy(slot);
        }
        spawnedSlots.Clear();
    }

    public void SetLoot(List<(int itemId, int count)> items, GameObject playerInventoryGO)
    {
        ClearSlots();

        foreach (var item in items)
        {
            GameObject slotObj = Instantiate(slotPrefab, contentTransform);
            spawnedSlots.Add(slotObj);

            GetItem slot = slotObj.GetComponent<GetItem>();
            if (slot != null)
            {
                slot.SetData(playerInventoryGO, item.itemId, item.count);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

public class FacilityPrefabDatabase : MonoBehaviour
{
    [Serializable]
    public class FacilityPrefabEntry
    {
        public int facilityId;
        public GameObject prefab;
    }

    [SerializeField] private FacilityPrefabEntry[] entries;

    private Dictionary<int, GameObject> prefabByFacilityId;

    private void Awake()
    {
        prefabByFacilityId = new Dictionary<int, GameObject>();

        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            FacilityPrefabEntry entry = entries[i];

            if (entry == null)
                continue;

            if (entry.prefab == null)
            {
                Debug.LogWarning($"[FacilityPrefabDatabase] facilityId={entry.facilityId} prefab is null.");
                continue;
            }

            if (prefabByFacilityId.ContainsKey(entry.facilityId))
            {
                Debug.LogWarning($"[FacilityPrefabDatabase] Duplicate facilityId={entry.facilityId}");
                continue;
            }

            prefabByFacilityId.Add(entry.facilityId, entry.prefab);
        }
    }

    public GameObject GetPrefab(int facilityId)
    {
        if (prefabByFacilityId == null)
            return null;

        prefabByFacilityId.TryGetValue(facilityId, out GameObject prefab);
        return prefab;
    }
}
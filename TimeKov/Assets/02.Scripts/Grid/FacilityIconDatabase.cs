using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FacilityIconDatabase : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static FacilityIconDatabase Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildDictionary();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 폰트 ──────────────────────────────────────────────────────────────
    [Header("레이블 폰트")]
    [Tooltip("BuildingLabelUI에 사용할 한글 TMP 폰트를 여기에 드래그하세요.")]
    public TMP_FontAsset labelFont;

    // ── 아이콘 등록 ────────────────────────────────────────────────────────
    [Serializable]
    public class IconEntry
    {
        [Tooltip("DataStore FacilityRow.facilityId와 동일한 값")]
        public int facilityId;
        [Tooltip("해당 건물 아이콘 (Import Settings에서 Sprite로 설정)")]
        public Sprite icon;
    }

    [SerializeField] private IconEntry[] entries;

    // ── 런타임 딕셔너리 ────────────────────────────────────────────────────
    private Dictionary<int, Sprite> _iconById;

    public Sprite GetIcon(int facilityId)
    {
        if (_iconById == null) BuildDictionary();
        _iconById.TryGetValue(facilityId, out var sprite);
        return sprite;
    }

    private void BuildDictionary()
    {
        _iconById = new Dictionary<int, Sprite>();
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null || e.icon == null) continue;
            if (_iconById.ContainsKey(e.facilityId))
            {
                Debug.LogWarning($"[FacilityIconDatabase] 중복 facilityId={e.facilityId}");
                continue;
            }
            _iconById[e.facilityId] = e.icon;
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => BuildDictionary();
#endif
}
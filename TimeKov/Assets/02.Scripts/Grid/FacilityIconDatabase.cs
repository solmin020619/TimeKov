using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 설비 아이콘 조회.
//  1순위: FacilityData 시트의 iconKey -> Resources/Facilities/{iconKey} 에서 스프라이트 로드.
//         (시트 한 곳만 고치면 아이콘이 바뀐다. 아이템 아이콘(ItemDatabase.GetIcon)과 같은 방식.)
//  2순위(폴백): 아래 entries 인스펙터 수동 매핑. iconKey 미설정/스프라이트 미배치/데이터 미로드 시 사용.
//  => 시트/Resources 세팅 전에도 기존 동작 그대로라 무회귀.
public class FacilityIconDatabase : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
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

    // ── 폰트 ──────────────────────────────────────────────────────────
    [Header("라벨 폰트")]
    [Tooltip("BuildingLabelUI 에 쓰는 한글 TMP 폰트를 여기에 드래그하세요.")]
    public TMP_FontAsset labelFont;

    // ── 아이콘 수동 매핑(폴백) ────────────────────────────────────────
    [Serializable]
    public class IconEntry
    {
        [Tooltip("FacilityData 의 facilityId 와 동일한 값")]
        public int facilityId;
        [Tooltip("해당 설비 아이콘 (Import Settings 에서 Sprite 로 설정)")]
        public Sprite icon;
    }

    [Tooltip("시트 iconKey 가 비어 있을 때 쓰는 폴백 매핑. 시트로 완전 이전하면 비워도 됨.")]
    [SerializeField] private IconEntry[] entries;

    // ── 런타임 ────────────────────────────────────────────────────────
    private Dictionary<int, Sprite> _iconById;                                   // 폴백(수동) 매핑 캐시
    private static readonly Dictionary<string, Sprite> _iconKeyCache = new();     // iconKey -> Resources 스프라이트(성공분만)

    public Sprite GetIcon(int facilityId)
    {
        // 1순위: 시트 iconKey -> Resources/Facilities/{key}
        var fd = GameDataUtility.GetFacility(facilityId);
        if (fd != null && !string.IsNullOrEmpty(fd.iconKey))
        {
            if (!_iconKeyCache.TryGetValue(fd.iconKey, out var byKey))
            {
                byKey = Resources.Load<Sprite>("Facilities/" + fd.iconKey);
                if (byKey != null) _iconKeyCache[fd.iconKey] = byKey;   // 실패는 캐시 안 함(추후 배치 시 재시도)
            }
            if (byKey != null) return byKey;
        }

        // 2순위(폴백): 인스펙터 수동 매핑
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

using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 설비 아이콘 조회 — 시트 FacilityData.iconKey 로만 결정한다.
//   iconKey -> Resources/Facilities/{iconKey} 에서 스프라이트 로드 (아이템 아이콘 ItemDatabase.GetIcon 과 같은 방식).
//   시트가 유일 원본이라 옛 인스펙터 수동매핑(entries)은 제거됨. iconKey 가 비면 아이콘 없음(null).
// labelFont 는 BuildingLabelUI / FacilityWorldDisplay 가 쓰는 한글 폰트라 유지한다.
[SingleInstance]
public class FacilityIconDatabase : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static FacilityIconDatabase Instance { get; private set; }

    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 폰트 ──────────────────────────────────────────────────────────
    [Header("라벨 폰트")]
    [Tooltip("BuildingLabelUI / 설비 이름표에 쓰는 한글 TMP 폰트를 여기에 드래그하세요.")]
    public TMP_FontAsset labelFont;

    // ── 런타임 ────────────────────────────────────────────────────────
    private static readonly Dictionary<string, Sprite> _iconKeyCache = new();   // iconKey -> Resources 스프라이트(성공분만)

    public Sprite GetIcon(int facilityId)
    {
        var fd = GameDataUtility.GetFacility(facilityId);
        if (fd == null || string.IsNullOrEmpty(fd.iconKey)) return null;

        if (!_iconKeyCache.TryGetValue(fd.iconKey, out var sprite))
        {
            sprite = Resources.Load<Sprite>("Facilities/" + fd.iconKey);
            if (sprite != null) _iconKeyCache[fd.iconKey] = sprite;   // 실패는 캐시 안 함(추후 배치 시 재시도)
        }
        return sprite;
    }
}

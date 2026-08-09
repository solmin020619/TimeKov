// ItemDatabase.cs
// GameDataHolder.ItemData 를 래핑해서 UI 에 필요한 데이터 제공
// ItemDataSO 없이 기존 데이터 테이블 시스템과 연동
// 아이콘은 Resources/Items/ 폴더에서 iconKey 로 로드

using System.Collections.Generic;
using UnityEngine;

public static class ItemDatabase
{
    // 아이콘 스프라이트 캐시 (Resources.Load 중복 방지)
    private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

    // itemId (int) 로 ItemDataSheetData 조회
    // 없으면 null 반환
    // GameDataUtility.GetItem 이 이미 int 키를 처리함
    public static ItemDataSheetData GetItem(int itemId)
    {
        return GameDataUtility.GetItem(itemId);
    }

    // iconKey 로 스프라이트 로드 (캐시 적용)
    // 스프라이트는 Assets/Resources/Items/ 폴더에 있어야 함
    public static Sprite GetIcon(string iconKey)
    {
        if (string.IsNullOrEmpty(iconKey)) return null;

        if (_iconCache.TryGetValue(iconKey, out var cached))
            return cached;

        var sprite = Resources.Load<Sprite>("Items/" + iconKey);

        if (sprite == null)
            Debug.LogWarning("[ItemDatabase] 아이콘 없음: Resources/Items/" + iconKey);

        _iconCache[iconKey] = sprite;
        return sprite;
    }

    // 씬 전환 시 아이콘 캐시 초기화
    public static void ClearCache()
    {
        _iconCache.Clear();
    }
}
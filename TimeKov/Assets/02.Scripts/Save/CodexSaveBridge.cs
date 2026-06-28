// =====================================================================
// CodexSaveBridge.cs
// ItemDiscovery / CodexDiscovery / RecipeProgress는 static 클래스라 ISaveable을
// 직접 구현할 수 없다. 이 컴포넌트가 세 클래스의 상태를 모아 GameSaveData에
// 캡처/복원하는 다리 역할을 한다.
//
// SaveSlotManager.Awake()가 자기 Instance를 세팅한 직후 이 컴포넌트를 직접
// 스폰한다(자체 RuntimeInitializeOnLoadMethod를 쓰지 않음) — 그래야 이 컴포넌트의
// Awake가 실행되는 시점에 SaveSlotManager.Instance가 항상 이미 준비돼 있다.
//
// 주의: 이 컴포넌트는 SaveSlotManager와 같은 DontDestroyOnLoad 싱글톤이라 게임
// 세션 중 Awake가 단 한 번만 실행된다(MainMenu 최초 진입 시) — 그 시점엔 아직 슬롯이
// 선택되지 않았으므로 Awake에서 복원해봐야 항상 빈 데이터를 복원하게 된다. 그래서 복원은
// Awake가 아니라 SaveSlotManager.LoadSlot()/CreateSlot()이 슬롯을 확정하는 시점에
// ResetAndRestore()를 직접 호출해서 트리거한다(슬롯이 바뀔 때마다 재실행됨).
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class CodexSaveBridge : MonoBehaviour, ISaveable
{
    private void Awake()
    {
        SaveSlotManager.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
    }

    public void Capture(GameSaveData data)
    {
        data.discoveredItemIds = new List<int>(ItemDiscovery.AllObtained);

        data.monsterKills = new List<MonsterKillData>();
        foreach (var kv in CodexDiscovery.AllMonsterKills)
            data.monsterKills.Add(new MonsterKillData { sourceId = kv.Key, kills = kv.Value });
        data.watchedTutorials = new List<string>(CodexDiscovery.AllWatchedTutorials);
        data.activatedStats = new List<string>(CodexDiscovery.AllActivatedStats);
        data.activatedRates = new List<string>(CodexDiscovery.AllActivatedRates);

        data.recipeCrafts = new List<RecipeCraftData>();
        foreach (var kv in RecipeProgress.AllCrafts)
            data.recipeCrafts.Add(new RecipeCraftData { recipeId = kv.Key, crafts = kv.Value });
        data.claimedFacilityRewards = new List<int>(RecipeProgress.AllClaimedFacilities);
        data.activatedJackpots = new List<string>(RecipeProgress.AllActivatedJackpots);
    }

    /// <summary>SaveSlotManager가 슬롯을 확정(생성/로드)할 때마다 호출한다.
    /// 이전에 활성화돼 있던 다른 슬롯의 메모리 상태를 먼저 비우고, 새 슬롯의 저장된 값으로 채운다.</summary>
    public void ResetAndRestore()
    {
        ItemDiscovery.Reset();
        CodexDiscovery.Reset();
        RecipeProgress.Reset();

        if (SaveSlotManager.Instance == null || !SaveSlotManager.Instance.HasActiveSlot) return;
        GameSaveData data = SaveSlotManager.Instance.Data;

        ItemDiscovery.RestoreObtained(data.discoveredItemIds);

        var kills = new List<KeyValuePair<string, int>>();
        foreach (var entry in data.monsterKills)
            kills.Add(new KeyValuePair<string, int>(entry.sourceId, entry.kills));
        CodexDiscovery.RestoreState(kills, data.watchedTutorials, data.activatedStats, data.activatedRates);

        var crafts = new List<KeyValuePair<string, int>>();
        foreach (var entry in data.recipeCrafts)
            crafts.Add(new KeyValuePair<string, int>(entry.recipeId, entry.crafts));
        RecipeProgress.RestoreState(crafts, data.claimedFacilityRewards, data.activatedJackpots);
    }
}

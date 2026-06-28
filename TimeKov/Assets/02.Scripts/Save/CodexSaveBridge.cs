// =====================================================================
// CodexSaveBridge.cs
// ItemDiscovery / CodexDiscovery / RecipeProgress는 static 클래스라 ISaveable을
// 직접 구현할 수 없다. 이 컴포넌트가 세 클래스의 상태를 모아 GameSaveData에
// 캡처/복원하는 다리 역할을 한다.
//
// SaveSlotManager.Awake()가 자기 Instance를 세팅한 직후 이 컴포넌트를 직접
// 스폰한다(자체 RuntimeInitializeOnLoadMethod를 쓰지 않음) — 그래야 이 컴포넌트의
// Awake가 실행되는 시점에 SaveSlotManager.Instance가 항상 이미 준비돼 있다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class CodexSaveBridge : MonoBehaviour, ISaveable
{
    private void Awake()
    {
        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
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

    private void RestoreFromSave()
    {
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

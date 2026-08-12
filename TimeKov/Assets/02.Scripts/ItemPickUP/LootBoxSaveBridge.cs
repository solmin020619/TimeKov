// =====================================================================
// LootBoxSaveBridge.cs
// 땅에 떨어진 드롭 상자(LootBox)를 세이브에 싣는 다리.
//
// 왜 필요한가:
//   드롭 상자는 몹이 죽을 때 씬에 그냥 Instantiate 되는 오브젝트라, 저장 항목 어디에도
//   기록되지 않았다. 그래서 몹을 잡고 상자를 안 주운 채 메인메뉴로 나갔다 오면 그 아이템이
//   통째로 사라졌다(회복 불가).
//
// 왜 컴포넌트인가:
//   LootBox 는 static 리스트(All)로만 존재를 알 수 있어서 ISaveable 을 직접 구현할 수 없다.
//   CodexSaveBridge 와 같은 이유 - SaveSlotManager 가 Awake 에서 직접 스폰한다(씬 세팅 불필요).
//
// 씬 구분:
//   상자 위치는 월드 좌표라 다른 씬에서 되살리면 엉뚱한 데 뜬다. 항목마다 씬 이름을 함께
//   저장하고, 저장/복원 모두 "지금 씬 것"만 손댄다.
//   ★이 덕분에 메인메뉴에서 자동저장이 돌아도(이 다리는 DontDestroyOnLoad 라 계속 등록돼 있다)
//     월드에 있던 상자 기록이 지워지지 않는다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LootBoxSaveBridge : MonoBehaviour, ISaveable, ISaveLoadListener
{
    private void Awake()
    {
        SaveSlotManager.Instance?.Register(this);
        SaveSlotManager.Instance?.RegisterListener(this);
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
        SaveSlotManager.Instance?.UnregisterListener(this);
    }

    // ── 저장 ─────────────────────────────────────────────────────────

    public void Capture(GameSaveData data)
    {
        if (data == null) return;
        string scene = SceneManager.GetActiveScene().name;

        // 지금 씬 기록만 새로 쓴다. 다른 씬(예: 월드) 항목은 그대로 둔다.
        data.droppedBoxes.RemoveAll(b => b != null && b.sceneName == scene);

        foreach (var box in LootBox.All)
        {
            if (box == null) continue;

            var contents = box.Contents;
            if (contents == null || contents.Count == 0) continue;   // 빈 상자는 되살릴 것이 없다

            // LootBox 는 프리팹 루트가 아니라 자식에 붙어 있을 수 있다(EnemyDropOnDeath 가
            // GetComponentInChildren 으로 찾는다). 스폰 위치는 루트 기준이라야 원래 자리에 선다.
            Vector3 pos = box.transform.root.position;

            var entry = new DroppedBoxData { sceneName = scene, posX = pos.x, posY = pos.y, posZ = pos.z };
            foreach (var (itemId, count) in contents)
                entry.contents.Add(new ItemStackData { itemId = itemId, amount = count });

            data.droppedBoxes.Add(entry);
        }
    }

    // ── 복원 ─────────────────────────────────────────────────────────
    // 씬 로드 후 모든 Start 가 끝난 다음 프레임에 호출된다(SaveSlotManager.FireAfterLoad).
    // 몹/스포너가 자리를 잡은 뒤라 상자 프리팹을 빌려 오기에도 안전한 시점이다.

    public void OnAfterLoad()
    {
        var mgr = SaveSlotManager.Instance;
        if (mgr == null || !mgr.HasActiveSlot) return;

        var saved = mgr.Data.droppedBoxes;
        if (saved == null || saved.Count == 0) return;

        string scene = SceneManager.GetActiveScene().name;

        // 상자 프리팹은 몹이 들고 있다(몹 26종이 같은 프리팹 하나를 공용).
        // 몹이 다 죽은 뒤에도 찾을 수 있도록 비활성 오브젝트까지 뒤진다.
        GameObject prefab = null;
        var src = FindAnyObjectByType<EnemyDropOnDeath>(FindObjectsInactive.Include);
        if (src != null) prefab = src.BoxPrefab;

        int restored = 0, toStorage = 0;

        foreach (var entry in saved)
        {
            if (entry == null || entry.sceneName != scene) continue;
            if (entry.contents == null || entry.contents.Count == 0) continue;

            if (prefab == null)
            {
                // 프리팹을 못 구하는 씬 - 상자를 세울 수는 없어도 아이템을 잃게 두지는 않는다.
                foreach (var s in entry.contents)
                    InventoryManager.StorageInstance?.AddItem(s.itemId, s.amount);
                toStorage++;
                continue;
            }

            var go = Instantiate(prefab, new Vector3(entry.posX, entry.posY, entry.posZ), Quaternion.identity);
            var box = go.GetComponentInChildren<LootBox>();
            if (box == null)
            {
                Debug.LogWarning("[LootBoxSave] 스폰한 상자에서 LootBox 를 못 찾음 - 내용물을 창고로 보낸다");
                foreach (var s in entry.contents)
                    InventoryManager.StorageInstance?.AddItem(s.itemId, s.amount);
                Destroy(go);
                toStorage++;
                continue;
            }

            var list = new List<(int itemId, int count)>();
            foreach (var s in entry.contents) list.Add((s.itemId, s.amount));
            box.Initialize(list);
            restored++;
        }

        // 되살린 것은 지금 씬의 LootBox 로 다시 존재하므로, 다음 저장 때 Capture 가 새로 기록한다.
        // 여기서 목록을 비워두지 않으면 같은 상자가 이중으로 남을 수 있다.
        saved.RemoveAll(b => b != null && b.sceneName == scene);

        if (restored > 0 || toStorage > 0)
            Debug.Log($"[LootBoxSave] 드롭 상자 복원: {restored}개 (창고로 회수 {toStorage}개)");
    }
}

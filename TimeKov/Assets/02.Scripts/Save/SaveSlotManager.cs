// =====================================================================
// SaveSlotManager.cs
// 통합 세이브 시스템의 중앙 관리자 (DontDestroyOnLoad 싱글톤).
// DataBoot/DevHotkeys와 동일한 패턴: 씬에 수동 배치할 필요 없이 게임 시작 시 자동 생성되고
// 씬 전환에도 유지된다.
//
// 폴더 구조:
//   {persistentDataPath}/Saves/{slotId}/meta.json   - 목록 화면용 가벼운 메타데이터
//   {persistentDataPath}/Saves/{slotId}/save.json   - 실제 진행 데이터 (GameSaveData)
//
// 흐름:
//   1) WorldSelectUI가 ListSlots()로 목록 표시
//   2) CreateSlot() 또는 LoadSlot()으로 활성 슬롯 확정 -> Data에 적재
//   3) 게임플레이 씬으로 전환. 각 시스템은 자기 Awake/Start에서 SaveSlotManager.Instance.Data를
//      직접 읽어 스스로 복원한다 (DataBoot/GameDataHolder와 동일한 "끌어오기" 패턴 — 이 매니저는
//      복원 순서/타이밍을 모름).
//   4) 저장 시점(자동/수동)에 SaveActive() 호출 -> 등록된 모든 ISaveable.Capture()를 모아
//      save.json 1개에 한 번에 기록 (요청에 따라 "하나씩"이 아니라 "한 번에 다" 처리).
// =====================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[SingleInstance]
public class SaveSlotManager : MonoBehaviour
{
    public static SaveSlotManager Instance { get; private set; }

    /// <summary>현재 활성 슬롯의 진행 데이터. 슬롯을 로드/생성하기 전엔 null.</summary>
    public GameSaveData Data { get; private set; }
    public SaveSlotMeta ActiveMeta { get; private set; }
    public string ActiveSlotId { get; private set; }
    public bool HasActiveSlot => Data != null && !string.IsNullOrEmpty(ActiveSlotId);

    readonly List<ISaveable> _saveables = new();
    readonly List<ISaveLoadListener> _loadListeners = new();
    CodexSaveBridge _codexBridge;

    static string SavesRoot => Path.Combine(Application.persistentDataPath, "Saves");
    const string MetaFileName = "meta.json";
    const string SaveFileName = "save.json";

    // 퀘스트 완료/코어 강화 등 특정 이벤트에서만 저장하면, 그 사이에 인벤토리만 채우거나
    // 건물만 짓고 나가는 경우 진행이 그냥 사라진다 — 그런 누락을 막는 최후의 안전망.
    const float AutoSaveIntervalSeconds = 30f;

    // BeforeSceneLoad(첫 씬의 Awake들보다 먼저)여야 한다. AfterSceneLoad로 두면, 타이틀을 거치지
    // 않고 World 씬을 곧바로 Play했을 때(에디터 테스트) World의 다른 컴포넌트들 Awake가 먼저 돌면서
    // Instance가 아직 null인 시점에 SaveSlotManager.Instance?.Register(...) 호출이 통째로 스킵돼버린다.
    // 이 타이밍만 고쳐도 "활성 슬롯 없음" 자체는 정상 — MainMenu(월드 선택)를 거치지 않고 들어간
    // 경우엔 의도적으로 활성 슬롯이 없는 상태로 두고, 모든 ISaveable이 그걸 보고 조용히 저장/복원을
    // 건너뛴다(World 씬 직접 Play = 저장 시스템 미개입 샌드박스 테스트).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Spawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[SaveSlotManager]");
        go.AddComponent<SaveSlotManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;
        Directory.CreateDirectory(SavesRoot);
        InvokeRepeating(nameof(AutoSaveTick), AutoSaveIntervalSeconds, AutoSaveIntervalSeconds);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // CodexSaveBridge는 static 클래스(ItemDiscovery 등)를 ISaveable로 잇는 다리.
        // 여기서 직접 스폰해야 그 Awake가 도는 시점에 Instance(바로 위)가 이미 보장된다 —
        // 자체 RuntimeInitializeOnLoadMethod를 따로 두면 이 메서드와 실행 순서가 보장되지 않는다.
        var bridgeGo = new GameObject("[CodexSaveBridge]");
        _codexBridge = bridgeGo.AddComponent<CodexSaveBridge>();
        DontDestroyOnLoad(bridgeGo);

        // 땅에 떨어진 드롭 상자도 static 리스트로만 존재해 ISaveable을 직접 못 단다 - 같은 이유로 여기서 스폰.
        var lootGo = new GameObject("[LootBoxSaveBridge]");
        lootGo.AddComponent<LootBoxSaveBridge>();
        DontDestroyOnLoad(lootGo);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void AutoSaveTick()
    {
        if (HasActiveSlot) SaveActive();
    }

    // 앱 종료도 나가기와 같은 위생 - 벨트 위 아이템을 창고로 회수한 뒤 저장한다
    // (저장 항목에 벨트 점유가 없어서, 회수하지 않으면 실려 있던 아이템이 증발한다).
    // 슬롯이 없으면 저장할 데가 없으니 아무것도 안 한다. 에디터에서 메인메뉴를 안 거치고
    // 월드 씬을 바로 재생하면 늘 이 경우인데, 그때마다 경고가 떠서 진짜 경고와 섞였다.
    // (자동저장 AutoSaveTick 도 같은 조건으로 거른다)
    void OnApplicationQuit()
    {
        if (!HasActiveSlot) return;
        TIMEKOV.Factory.BeltSegment.RescueAllToStorage();
        SaveActive();
    }

    // ── ISaveable / ISaveLoadListener 등록 ───────────────────────────
    // ISaveable: 각 시스템이 자기 Awake에서 Register(this). SaveActive()가 순회해 Capture.
    // ISaveLoadListener: 씬 로드 후 OnAfterLoad()가 필요한 시스템이 Awake에서 RegisterListener.

    public void Register(ISaveable s)
    {
        if (s != null && !_saveables.Contains(s)) _saveables.Add(s);
    }

    public void Unregister(ISaveable s) => _saveables.Remove(s);

    public void RegisterListener(ISaveLoadListener l)
    {
        if (l != null && !_loadListeners.Contains(l)) _loadListeners.Add(l);
    }

    public void UnregisterListener(ISaveLoadListener l) => _loadListeners.Remove(l);

    // 씬 로드 후 모든 Start()가 끝난 다음 프레임에 리스너들에게 OnAfterLoad 발화.
    // sceneLoaded는 Awake 이후 Start 이전에 발화하므로, yield return null 1회로
    // "이 씬의 Start 전체 완료"를 보장할 수 있다.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (HasActiveSlot) StartCoroutine(FireAfterLoad());
    }

    IEnumerator FireAfterLoad()
    {
        yield return null;
        foreach (var l in _loadListeners)
        {
            if (l == null) continue;
            try { l.OnAfterLoad(); }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSlotManager] {l.GetType().Name}.OnAfterLoad() 실패: {e}");
            }
        }
    }

    // ── 슬롯 목록 ──────────────────────────────────────────────────────

    /// <summary>모든 슬롯의 메타데이터를 마지막 플레이 시각 내림차순으로 반환. meta.json만 읽으므로 가볍다.</summary>
    public List<SaveSlotMeta> ListSlots()
    {
        var result = new List<SaveSlotMeta>();
        if (!Directory.Exists(SavesRoot)) return result;

        foreach (var dir in Directory.GetDirectories(SavesRoot))
        {
            string metaPath = Path.Combine(dir, MetaFileName);
            if (!File.Exists(metaPath)) continue;
            try
            {
                var meta = JsonUtility.FromJson<SaveSlotMeta>(File.ReadAllText(metaPath));
                if (meta != null) result.Add(meta);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSlotManager] meta 읽기 실패({dir}): {e.Message}");
            }
        }

        result.Sort((a, b) => string.CompareOrdinal(b.lastPlayedIso, a.lastPlayedIso));
        return result;
    }

    // ── 슬롯 생성 / 로드 / 삭제 ───────────────────────────────────────

    /// <summary>새 월드 슬롯을 만들고 즉시 활성 슬롯으로 만든다. 빈 GameSaveData로 시작.</summary>
    public SaveSlotMeta CreateSlot(string worldName)
    {
        string slotId = "World_" + DateTime.UtcNow.Ticks; // 충돌 없는 고유 폴더명
        string dir = Path.Combine(SavesRoot, slotId);
        Directory.CreateDirectory(dir);

        string nowIso = DateTime.UtcNow.ToString("o");
        var meta = new SaveSlotMeta
        {
            slotId = slotId,
            worldName = string.IsNullOrWhiteSpace(worldName) ? "이름없는 월드" : worldName,
            createdAtIso = nowIso,
            lastPlayedIso = nowIso,
            coreLevelSnapshot = 0,
        };

        ActiveSlotId = slotId;
        ActiveMeta = meta;
        Data = new GameSaveData();

        WriteSlotFiles(dir, meta, Data);

        // CodexDiscovery 등 static 상태는 슬롯과 무관하게 메모리에 남으므로, 슬롯이 바뀔
        // 때마다(새 슬롯 생성 포함) 직접 리셋+복원을 트리거해줘야 한다 (CodexSaveBridge 참고).
        _codexBridge?.ResetAndRestore();
        return meta;
    }

    /// <summary>기존 슬롯을 읽어 활성 슬롯으로 만든다. 실패 시 false (Data/ActiveSlotId는 변경하지 않음).</summary>
    public bool LoadSlot(string slotId)
    {
        string dir = Path.Combine(SavesRoot, slotId);
        string metaPath = Path.Combine(dir, MetaFileName);
        string savePath = Path.Combine(dir, SaveFileName);

        if (!File.Exists(metaPath) || !File.Exists(savePath))
        {
            Debug.LogError($"[SaveSlotManager] 슬롯을 찾을 수 없음: {slotId}");
            return false;
        }

        try
        {
            ActiveMeta = JsonUtility.FromJson<SaveSlotMeta>(File.ReadAllText(metaPath));
            Data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(savePath)) ?? new GameSaveData();
            ActiveSlotId = slotId;

            // CodexDiscovery 등 static 상태는 슬롯과 무관하게 메모리에 남으므로, 슬롯이 바뀔
            // 때마다 직접 리셋+복원을 트리거해줘야 한다 (CodexSaveBridge 참고).
            _codexBridge?.ResetAndRestore();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSlotManager] 슬롯 로드 실패({slotId}): {e.Message}");
            return false;
        }
    }

    public void DeleteSlot(string slotId)
    {
        string dir = Path.Combine(SavesRoot, slotId);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        if (ActiveSlotId == slotId)
        {
            ActiveSlotId = null;
            ActiveMeta = null;
            Data = null;
        }
    }

    // ── 저장 ───────────────────────────────────────────────────────────

    /// <summary>등록된 모든 시스템의 Capture()를 모아 활성 슬롯에 한 번에 기록.</summary>
    public void SaveActive()
    {
        if (!HasActiveSlot)
        {
            Debug.LogWarning("[SaveSlotManager] 활성 슬롯이 없어 저장을 건너뜀.");
            return;
        }

        foreach (var s in _saveables)
        {
            if (s == null) continue;
            try { s.Capture(Data); }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSlotManager] {s.GetType().Name}.Capture() 실패, 나머지는 계속 저장: {e}");
            }
        }

        ActiveMeta.lastPlayedIso = DateTime.UtcNow.ToString("o");
        ActiveMeta.coreLevelSnapshot = Data.coreLevel;

        string dir = Path.Combine(SavesRoot, ActiveSlotId);
        Directory.CreateDirectory(dir);
        WriteSlotFiles(dir, ActiveMeta, Data);
    }

    // 임시 파일에 먼저 쓰고 교체하는 원자적 쓰기 — 저장 중 게임이 죽어도 기존 세이브가 깨지지 않게.
    static void WriteSlotFiles(string dir, SaveSlotMeta meta, GameSaveData data)
    {
        AtomicWrite(Path.Combine(dir, MetaFileName), JsonUtility.ToJson(meta, true));
        AtomicWrite(Path.Combine(dir, SaveFileName), JsonUtility.ToJson(data, true));
    }

    static void AtomicWrite(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);

        if (!File.Exists(path)) { File.Move(tmp, path); return; }   // 첫 저장 - 교체할 원본이 없다

        // ★File.Replace 를 쓴다. 예전엔 Delete 후 Move 였는데, 그 둘 사이에 게임이 죽거나
        //   전원이 나가면 원본도 임시본도 아닌 상태가 되어 세이브가 통째로 사라진다.
        //   Replace 는 교체가 한 번에 이뤄지고, 덤으로 직전 세이브를 .bak 으로 남긴다.
        string backup = path + ".bak";
        try
        {
            File.Replace(tmp, path, backup, ignoreMetadataErrors: true);
        }
        catch (Exception e)
        {
            // 파일시스템이 Replace 를 못 하는 환경(일부 네트워크/외장 드라이브)일 때의 폴백.
            // 이 경로는 예전과 같은 위험을 지므로 로그를 남겨 추적할 수 있게 한다.
            Debug.LogWarning($"[SaveSlotManager] File.Replace 실패, 폴백 사용: {e.Message}");
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}

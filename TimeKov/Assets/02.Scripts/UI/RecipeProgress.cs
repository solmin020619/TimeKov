using System.Collections.Generic;
using UnityEngine;

// 레시피 제작 진행도. CodexSaveBridge(ISaveable)가 세이브 슬롯에 영구 저장한다.
// 레시피 1회 제작 = +1. CraftsToMaster(10) 달성 = 마스터 -> 잭팟(JackpotChance 확률 2배 제작) 활성.
// 설비별 보상(전 레시피 마스터 시 코어키트) 수령 상태도 여기서 관리.
public static class RecipeProgress
{
    public const int   CraftsToMaster = 10;     // 레시피 마스터(잭팟 해금) 기준 제작 수
    public const float JackpotChance  = 0.10f;  // 마스터 레시피 잭팟 확률(2배 제작)

    private static readonly Dictionary<string, int> _craft = new Dictionary<string, int>();
    private static readonly HashSet<int> _claimedFacilities = new HashSet<int>();

    // 잭팟 발생 알림 (facilityId, itemId, 월드위치). 공장 이펙트가 구독.
    public static event System.Action<int, int, Vector3> OnJackpot;

    // ── 제작 진행도 ──────────────────────────────────────────────
    public static void RecordCraft(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId)) return;
        string id = recipeId.Trim();
        _craft.TryGetValue(id, out int c);
        int n = c + 1;
        _craft[id] = n;
        // 마스터(CraftsToMaster회) 도달 순간 도감 알림(!) - 설비 탭서 잭팟 활성화할 게 생김.
        if (n == CraftsToMaster)
            CodexNotice.MarkUnseen(CodexNotice.Facility);
    }

    public static int Crafts(string recipeId)
        => (!string.IsNullOrEmpty(recipeId) && _craft.TryGetValue(recipeId.Trim(), out int c)) ? c : 0;

    public static bool IsMastered(string recipeId) => Crafts(recipeId) >= CraftsToMaster;

    // 잭팟 활성화(마스터 후 도감서 직접 클릭). 활성화돼야 공장서 2배 발동.
    private static readonly HashSet<string> _jackpotOn = new HashSet<string>();
    public static bool IsJackpotActive(string recipeId) => !string.IsNullOrEmpty(recipeId) && _jackpotOn.Contains(recipeId.Trim());
    public static void ActivateJackpot(string recipeId) { if (!string.IsNullOrEmpty(recipeId)) _jackpotOn.Add(recipeId.Trim()); }

    public static void RaiseJackpot(int facilityId, int itemId, Vector3 worldPos)
        => OnJackpot?.Invoke(facilityId, itemId, worldPos);

    // ── 설비 보상 수령 ──────────────────────────────────────────
    public static bool IsFacilityRewardClaimed(int facilityId) => _claimedFacilities.Contains(facilityId);
    public static void MarkFacilityRewardClaimed(int facilityId) => _claimedFacilities.Add(facilityId);

    // ── 세이브 캡처/복원 (CodexSaveBridge 전용) ──────────────────────────
    public static IReadOnlyDictionary<string, int> AllCrafts => _craft;
    public static IReadOnlyCollection<int> AllClaimedFacilities => _claimedFacilities;
    public static IReadOnlyCollection<string> AllActivatedJackpots => _jackpotOn;

    /// <summary>세이브 복원 전용 — 알림(!) 발생 없이 조용히 채운다.</summary>
    public static void RestoreState(
        IEnumerable<KeyValuePair<string, int>> crafts,
        IEnumerable<int> claimedFacilities,
        IEnumerable<string> jackpotsOn)
    {
        if (crafts != null) foreach (var kv in crafts) _craft[kv.Key] = kv.Value;
        if (claimedFacilities != null) foreach (var f in claimedFacilities) _claimedFacilities.Add(f);
        if (jackpotsOn != null) foreach (var j in jackpotsOn) _jackpotOn.Add(j);
    }

    // 세션 시작 시 1회 초기화 + CodexSaveBridge가 슬롯 전환마다(다른 월드로 이동 등) 호출해
    // 이전 슬롯의 메모리 상태가 새 슬롯으로 새는 것을 막는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { _craft.Clear(); _claimedFacilities.Clear(); _jackpotOn.Clear(); }
}

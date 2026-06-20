using System.Collections.Generic;
using UnityEngine;

// 레시피 제작 진행도(세션 단위, 저장 없음 - CodexDiscovery/FacilityUnlockManager와 동일 정책).
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _craft.Clear(); _claimedFacilities.Clear(); _jackpotOn.Clear(); }
}

using System.Collections.Generic;
using UnityEngine;

// 박스 등급별 VFX를 관리한다 — 떨어져 있을 때(드롭)와 F로 먹을 때(수집).
// 박스 내용물 중 최고 등급(itemGrade)에 맞는 항목이 적용된다.
public class LootBoxVFX : MonoBehaviour
{
    [System.Serializable]
    public struct GradeVfx
    {
        [Tooltip("이 항목이 적용될 아이템 등급")]
        public ItemGrade grade;

        [Tooltip("박스가 바닥에 있을 때 (오라)")]
        public GameObject dropVfx;

        [Tooltip("F로 먹을 때 (Trail)")]
        public GameObject collectVfx;
    }

    [Tooltip("등급별 VFX 목록. 박스 내용물 중 최고 등급에 맞는 항목이 쓰인다.")]
    [SerializeField] private GradeVfx[] gradeVfx;

    [Tooltip("수집 VFX가 플레이어에게 빨려가는 시간 (초)")]
    [SerializeField] private float collectFlyTime = 0.35f;

    [Tooltip("수집 VFX가 플레이어 원점에서 얼마나 위로 빨려들지 (m). 몸 중앙에 오게 조절")]
    [SerializeField] private float collectTargetHeight = 0.5f;

    [Tooltip("켜면 박스 등급 / 선택된 VFX를 Console에 출력 (디버그)")]
    [SerializeField] private bool logGradeInfo = true;

    [Tooltip("디버그 — 0 이상이면 박스 등급을 이 값으로 강제한다 (VFX 테스트용). 평소엔 -1")]
    [SerializeField] private int debugForceGrade = -1;

    private GameObject _collectPrefab;

    void Start()
    {
        LootBox box = GetComponentInChildren<LootBox>();
        if (box == null) return;

        int realGrade = GetTopGrade(box);
        int top = debugForceGrade >= 0 ? debugForceGrade : realGrade;
        int idx = FindEntry(top);

        if (logGradeInfo)
            Debug.Log($"[LootBoxVFX] {(debugForceGrade >= 0 ? "[디버그 강제] " : "")}등급 = {top} → " +
                      (idx >= 0
                          ? $"gradeVfx 항목 #{idx} (Grade = {gradeVfx[idx].grade} / {(int)gradeVfx[idx].grade}) 사용"
                          : "맞는 Grade 항목이 없음 → VFX 안 뜸"));

        if (idx < 0) return;

        _collectPrefab = gradeVfx[idx].collectVfx;

        GameObject dropPrefab = gradeVfx[idx].dropVfx;
        if (dropPrefab != null) Instantiate(dropPrefab, box.transform);
    }

    // LootBox.Collect 에서 호출 — 등급 Trail VFX를 박스에서 플레이어 쪽으로 날린다
    public void PlayCollectEffect(Vector3 from, Transform target)
    {
        if (target == null || _collectPrefab == null) return;

        GameObject fx = Instantiate(_collectPrefab, from, Quaternion.identity);
        fx.AddComponent<LootBoxCollectFlyer>().Begin(target, collectFlyTime, collectTargetHeight);
    }

    // grade 이하 중 가장 높은 등급의 gradeVfx 항목 인덱스.
    // 정확히 일치하는 항목이 없으면 그 아래 가장 가까운 항목을 쓴다 (등급 빈틈 방지).
    private int FindEntry(int grade)
    {
        if (gradeVfx == null || gradeVfx.Length == 0 || grade < 0) return -1;

        int best = -1;
        int lowest = 0;
        for (int i = 0; i < gradeVfx.Length; i++)
        {
            int g = (int)gradeVfx[i].grade;
            if (g <= grade && (best < 0 || g > (int)gradeVfx[best].grade))
                best = i;
            if (g < (int)gradeVfx[lowest].grade)
                lowest = i;
        }
        return best >= 0 ? best : lowest;
    }

    // 박스 내용물 중 가장 높은 등급 번호 (없으면 -1). logGradeInfo 면 내용물을 Console에 출력.
    private int GetTopGrade(LootBox box)
    {
        int top = -1;
        string report = "[LootBoxVFX] 박스 내용물:";

        IReadOnlyList<(int itemId, int count)> contents = box.Contents;
        for (int i = 0; i < contents.Count; i++)
        {
            ItemDataSheetData item = GameDataUtility.GetItem(contents[i].itemId);
            int g = item != null ? (int)item.itemGrade : -1;
            string name = item != null ? item.itemGrade.ToString() : "데이터없음";
            if (g > top) top = g;
            report += $" [itemId {contents[i].itemId} = 등급 {g}({name})]";
        }

        if (logGradeInfo) Debug.Log(report);
        return top;
    }
}

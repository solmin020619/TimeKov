// =====================================================================
// GimmickSave.cs
// 기믹/퍼즐의 진행 상태를 세이브에 넣고 빼는 공용 창구.
//
// [왜 값이 float 하나인가]
//   기믹마다 저장할 것이 다르다 — 스위치는 켜짐/꺼짐, 에너지 노드는 넣은 연료 개수,
//   파괴물은 맞은 횟수, 순서 퍼즐은 몇 번째까지 밟았는지. 전부 숫자 하나로 표현되므로
//   같은 그릇에 담는다. 그래야 새 기믹을 만들 때 GameSaveData 를 안 건드려도 된다.
//
// [id 를 계층 경로로 짓는 이유]
//   기믹 하나에 컴포넌트가 여럿 붙고(스위치 + 트리거), 씬에 같은 이름의 오브젝트가
//   흔하다. 오브젝트 이름만 쓰면 조용히 겹쳐서 "하나만 켰는데 둘 다 켜짐"이 된다.
//   계층 경로는 씬 안에서 유일하므로 아무것도 안 해도 안 겹친다.
//   ★대신 오브젝트를 옮기거나 이름을 바꾸면 id 가 바뀌어 진행이 초기화된다.
//     그게 곤란하면 각 컴포넌트의 '세이브 id' 칸에 직접 적어 고정한다(그게 우선한다).
//   ★같은 오브젝트에 기믹 컴포넌트가 둘 이상 붙을 수 있으므로(KeyLock 은 트리거이면서
//     상호작용물) 종류 접두어(kind)를 붙여 구분한다.
//
// [저장 시점]
//   값이 바뀔 때마다 바로 파일까지 쓴다(Set). 퍼즐 진행은 드문 조작이라 비용이 문제되지
//   않고, 다음 저장 지점까지 미루면 그 사이에 게임을 끈 플레이어가 진행을 잃는다.
//   ★같은 값을 다시 넣으면 아무것도 하지 않는다 — 토글 스위치를 연타해도 파일을 다시
//     쓰지 않는다.
// =====================================================================

using UnityEngine;

public static class GimmickSave
{
    /// <summary>이 기믹의 저장 키. "종류:이름" 형태다 — 이름은 직접 지정한 id, 없으면 계층 경로.
    ///
    /// ★종류 접두어는 직접 지정한 id 에도 반드시 붙인다. 한 컴포넌트가 값을 두 개 저장하는
    ///   경우가 있어서다 — SequenceTrigger 는 '완성 여부'(부모가 저장)와 '몇 칸까지 밟았는지'를
    ///   따로 저장하는데, 지정 id 를 그대로 쓰면 둘이 같은 칸에 써서 서로를 덮어쓴다.</summary>
    public static string Key(string kind, Component owner, string overrideId)
    {
        if (!string.IsNullOrEmpty(overrideId)) return $"{kind}:{overrideId}";
        return owner == null ? kind : $"{kind}:{PathOf(owner.transform)}";
    }

    /// <summary>씬 이름 + 루트부터의 계층 경로("World|Gimmicks/Btn Gimmick/Lever").
    /// ★씬 이름을 붙이는 이유: 씬마다 "Gimmicks/Switch" 처럼 같은 구조를 쓰기 쉬운데,
    ///   경로만 쓰면 다른 씬의 스위치와 같은 칸을 공유해 한쪽을 켜면 다른 쪽도 켜진다.</summary>
    public static string PathOf(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return $"{t.gameObject.scene.name}|{path}";
    }

    /// <summary>저장된 값 읽기. 기록이 없으면 false(= 아직 아무것도 안 함).</summary>
    public static bool TryGet(string id, out float value)
    {
        value = 0f;
        var data = SaveSlotManager.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(id)) return false;

        for (int i = 0; i < data.gimmickStates.Count; i++)
            if (data.gimmickStates[i] != null && data.gimmickStates[i].id == id)
            {
                value = data.gimmickStates[i].value;
                return true;
            }
        return false;
    }

    public static bool GetBool(string id, bool fallback = false)
        => TryGet(id, out float v) ? v > 0.5f : fallback;

    public static int GetInt(string id, int fallback = 0)
        => TryGet(id, out float v) ? Mathf.RoundToInt(v) : fallback;

    /// <summary>값 기록 + 즉시 파일까지 저장. 값이 그대로면 아무것도 하지 않는다.
    /// 되돌릴 수 없는 진행(연료를 넣었다, 스위치를 켰다, 조건을 풀었다)에 쓴다.</summary>
    public static void Set(string id, float value)
    {
        if (Write(id, value)) SaveSlotManager.Instance?.SaveActive();
    }

    public static void Set(string id, bool on) => Set(id, on ? 1f : 0f);

    /// <summary>값 기록만. 파일은 다음 저장 시점(30초 자동저장·이벤트 저장)에 같이 나간다.
    ///
    /// ★자주 바뀌는 값에 쓴다. SaveActive() 는 등록된 모든 시스템의 Capture 를 돌고 세이브
    ///   파일 두 개를 통째로 다시 쓴다 — 파괴물을 한 대 칠 때마다 부르면 전투 중에 끊긴다.
    ///   잃어도 자원 손해가 없는 값(때린 횟수 등)만 이쪽을 쓸 것.</summary>
    public static void SetDeferred(string id, float value) => Write(id, value);

    /// <summary>메모리의 세이브 데이터에만 기록. 실제로 바뀌었으면 true.</summary>
    static bool Write(string id, float value)
    {
        var data = SaveSlotManager.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(id)) return false;

        for (int i = 0; i < data.gimmickStates.Count; i++)
        {
            var e = data.gimmickStates[i];
            if (e == null || e.id != id) continue;
            if (Mathf.Approximately(e.value, value)) return false;   // 안 바뀌었으면 파일도 안 쓴다
            e.value = value;
            return true;
        }

        data.gimmickStates.Add(new GimmickStateData { id = id, value = value });
        return true;
    }
}

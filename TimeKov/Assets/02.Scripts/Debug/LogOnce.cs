// =====================================================================
// LogOnce.cs
// 콘솔 도배를 막는 로그 창구. 같은 문제를 여러 번 알릴 이유가 없다.
//
// [왜 필요한가]
//   경고 자체는 옳은데 '뜨는 횟수'가 문제인 자리가 있다.
//     - 반복 실행: 스폰 실패 경고가 리스폰마다 다시 찍힌다(무한히 쌓인다).
//     - 다수 인스턴스: 같은 설정 실수를 스포너 25개가 각자 찍어 25줄이 된다.
//   콘솔이 이런 줄로 차면 정작 봐야 할 새 문제가 스크롤 밖으로 밀려난다.
//
// [쓰는 법]
//   LogOnce.Warn("navmesh:" + name, "...")  -> 같은 키는 처음 한 번만
//   LogOnce.Group("스포너 설정 비었음", name) -> 프레임 끝에 전부 묶어 한 줄로
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class LogOnce : MonoBehaviour
{
    private static readonly HashSet<string> _seen = new();
    private static readonly Dictionary<string, List<string>> _groups = new();
    private static LogOnce _pump;

    /// <summary>같은 키로는 한 번만 경고한다. 키에 대상 이름을 넣으면 대상별로 한 번씩 나온다.</summary>
    public static void Warn(string key, string message)
    {
        if (string.IsNullOrEmpty(key) || !_seen.Add(key)) return;
        Debug.LogWarning(message);
    }

    /// <summary>같은 제목끼리 모았다가 프레임 끝에 한 줄로 낸다. 여러 오브젝트가 같은 문제를 가질 때.</summary>
    public static void Group(string title, string item)
    {
        if (string.IsNullOrEmpty(title)) return;
        if (!_groups.TryGetValue(title, out var list)) { list = new List<string>(); _groups[title] = list; }
        list.Add(item);
        EnsurePump();
    }

    /// <summary>씬을 다시 시작해 같은 문제를 다시 보고 싶을 때(개발용).</summary>
    public static void Reset() { _seen.Clear(); _groups.Clear(); }

    // ★플레이 재진입 대비. 에디터에서 도메인 리로드를 끄면 static 이 그대로 살아남아
    //   "이미 본 경고"로 취급돼 두 번째 플레이부터 아무것도 안 뜬다(문제가 사라진 걸로 오해하게 된다).
    //   이 프로젝트는 재입장 stale static 으로 이미 몇 번 당했으니 명시적으로 지운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() { _seen.Clear(); _groups.Clear(); _pump = null; }

    private static void EnsurePump()
    {
        if (_pump != null) return;
        var go = new GameObject("[LogOnce]") { hideFlags = HideFlags.HideAndDontSave };
        _pump = go.AddComponent<LogOnce>();
        DontDestroyOnLoad(go);
    }

    private void LateUpdate()
    {
        if (_groups.Count == 0) return;
        foreach (var kv in _groups)
        {
            var list = kv.Value;
            if (list.Count == 0) continue;
            // 한 줄 요약: 개수 + 대상 목록. 목록이 길면 앞쪽만 보여주고 나머지는 숫자로.
            string names = list.Count <= 8
                ? string.Join(", ", list)
                : string.Join(", ", list.GetRange(0, 8)) + $" 외 {list.Count - 8}개";
            Debug.LogWarning($"{kv.Key} ({list.Count}개): {names}");
        }
        _groups.Clear();
    }
}

// =====================================================================
// UIDuplicateGuard.cs
// "하나만 있어야 하는" 컴포넌트가 두 벌 존재할 때 조용히 넘어가지 않게 한다.
//
// [왜 필요한가]
//   싱글톤들이 전부 "이미 있으면 나를 파괴한다"로만 처리하고 있었다.
//   중복을 말없이 삼킨다. 게임은 굴러가고 콘솔도 조용해서 아무도 모른다.
//   그러다 인스펙터 참조가 파괴된 쪽을 가리키는 날 "UI 가 안 뜬다"로 터진다.
//   실제로 이 프로젝트에서 반년 동안 반복됐다.
//
// [중복이 생기는 대표 경로]
//   씬의 프리팹 인스턴스에 오브젝트를 추가한 상태에서 'Apply All Overrides' 를 누르면
//   그 오브젝트가 프리팹으로 복사된다. 씬 쪽 추가분이 남아 있으면 프리팹 1벌 + 씬 1벌이 된다.
//   (08-08 Canvas 프리팹에 TutorialOverlay/CastGauge/ChestPrompt 등이 이렇게 들어왔다)
//
// [여기서는 '알리기'만 한다. 어느 쪽을 지울지는 에디터 툴 몫이다]
//   한때 플레이 중 '파괴된 쪽 경로'를 기록해 에디터 툴에 넘겼는데 폐기했다. 이유 두 가지:
//     1) 중복 두 오브젝트는 이름/부모가 같아 경로가 완전히 동일하다.
//        기록 하나가 양쪽 모두에 매칭돼 '둘 다 지워라'로 표시되는 사고가 났다.
//     2) 어느 쪽이 살아남는지는 Awake 순서 소관이라 실행마다 바뀐다.
//        실행 결과는 지울 쪽의 근거가 못 된다.
//   지울 쪽 판정은 프리팹 구조로 해야 확정이고, 그건 에디트 모드에서
//   UIDuplicateScanner(Tools/TIMEKOV/UI 중복 검사)가 한다. 플레이는 필요 없다.
//
// [쓰는 법] 기존 한 줄을 이걸로 바꾸면 된다.
//   if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
// =====================================================================

using System.Text;
using UnityEngine;

public static class UIDuplicateGuard
{
    /// <summary>
    /// kept(먼저 자리잡은 것)와 dup(방금 깨어난 것)이 서로 다르면 중복이다.
    /// 어느 오브젝트 둘이 충돌했는지 전체 경로로 알리고 true 를 돌려준다(호출측이 파괴).
    /// </summary>
    public static bool Report<T>(T kept, T dup) where T : Component
    {
        if (kept == null || ReferenceEquals(kept, dup)) return false;

        // 에러로 낸다. 경고는 다른 줄에 묻히는데, 이건 반드시 고쳐야 하는 구조 문제다.
        Debug.LogError(
            $"[중복 UI] {typeof(T).Name} 이(가) 두 곳에 있다. 한쪽을 지워야 한다.\n" +
            $"  살아남음 : {FullPath(kept.transform)}\n" +
            $"  파괴됨   : {FullPath(dup.transform)}\n" +
            $"  * 정지하고 메뉴 Tools/TIMEKOV/UI 중복 검사 를 누르면 지울 쪽에만 빨간 배지가 붙는다.",
            dup);
        return true;
    }

    /// <summary>루트부터의 전체 하이어라키 경로. 어느 쪽을 지울지 바로 찾을 수 있게.</summary>
    public static string FullPath(Transform t)
    {
        if (t == null) return "(없음)";
        var sb = new StringBuilder(t.name);
        var p = t.parent;
        int guard = 0;
        while (p != null && guard++ < 32)
        {
            sb.Insert(0, p.name + " / ");
            p = p.parent;
        }
        sb.Append("   [씬: ").Append(t.gameObject.scene.name).Append(']');
        return sb.ToString();
    }
}

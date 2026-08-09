// =====================================================================
// Editor/TableBuildMenu.cs
// 시트 메뉴 진입점.
//
// 시트/코드 다시 만들기 (컬럼을 추가하거나 지웠을 때) - 스키마 -> Generated/*.g.cs
//
// ★값(숫자/텍스트)만 고쳤을 때는 아무것도 누를 필요 없다. Play 를 누르면 자동으로 최신 시트를 받는다.
//   이 메뉴는 컬럼이나 테이블 자체를 바꿔서 스키마 코드를 고친 다음에만 누른다.
//
// [08-09] '내용 보기(콘솔에 값 출력)' 메뉴를 삭제했다.
//   시트 반영 여부를 확인하려고 만든 건데, Play 가 매번 최신을 받고 게시본 지연도
//   .pending.csv 로 건너뛰게 되면서 확인할 일 자체가 없어졌다.
//   값이 궁금하면 시트를 열어보면 되고, 오류 검증은 로드할 때 어차피 돈다.
// =====================================================================

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class TableBuildMenu
{
    // 스키마(Data/Schemas/*.cs)를 읽어 Generated/ 폴더에 .g.cs 를 생성한다.
    // ★시트를 읽는 게 아니라 '스키마 코드'를 읽는다. 시트에 컬럼을 추가하는 것만으로는
    //   아무 일도 안 일어난다. 스키마에 Add(...) 를 먼저 넣어야 한다.
    [MenuItem("시트/코드 다시 만들기 (컬럼을 추가하거나 지웠을 때)")]
    public static void Generate()
    {
        Debug.Log("[테이블] 코드 생성 시작");
        CodeGenerator.GenerateAll();
    }

    // 플레이 중에는 비활성화
    [MenuItem("시트/코드 다시 만들기 (컬럼을 추가하거나 지웠을 때)", true)]
    private static bool ValidateMenu() => !EditorApplication.isPlaying;
}

#endif

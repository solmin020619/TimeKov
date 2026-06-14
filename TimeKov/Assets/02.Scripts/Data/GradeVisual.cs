using UnityEngine;

// 아이템 등급(ItemGrade)별 표시 색/이름을 한 곳에서 관리한다.
// 인벤/설비/보물상자/획득로그/드랍VFX 가 모두 여기를 참조해 등급색이 어긋나지 않게 한다.
// (예전엔 6곳에 따로 정의돼 서로 색이 달랐음 = 어긋남 진범)
// 초록 계열은 생명력 흡수 VFX 전용이라 등급색에서 제외한다.
public static class GradeVisual
{
    // 인덱스 = ItemGrade (Common/Advanced/Rare/Hero/Legend)
    private static readonly Color[] Colors =
    {
        new Color(0.56f, 0.60f, 0.68f, 1f),   // Common   - 쿨 실버블루(은은한 색, 색없음 방지)
        new Color(0.31f, 0.61f, 0.88f, 1f),   // Advanced - 파랑     (4f9be0)
        new Color(0.91f, 0.33f, 0.31f, 1f),   // Rare     - 코랄레드 (e8544f, 초록 대체)
        new Color(0.65f, 0.31f, 0.88f, 1f),   // Hero     - 보라     (a64fe0)
        new Color(1.00f, 0.69f, 0.13f, 1f),   // Legend   - 골드     (ffb020)
    };

    private static readonly string[] Names = { "일반", "고급", "희귀", "영웅", "전설" };

    // 연료템 전용 색 (등급과 별개). 연료는 1종뿐인 독자 아이템이라 따로 구분.
    // 구릿빛/브론즈 = 나뭇가지(나무/땔감) 테마. 등급 루팅 스펙트럼(회청적보금) 밖 + 갈색은 등급색 아님 -> 루팅템처럼 안 보임.
    // FuelConfig.fuelItemId 인 아이템에 적용.
    public static readonly Color FuelColor = new Color(0.72f, 0.47f, 0.26f, 1f);

    public static int Count => Colors.Length;

    // 등급 색. 범위 밖이면 흰색.
    public static Color GetColor(int grade)
        => (grade >= 0 && grade < Colors.Length) ? Colors[grade] : Color.white;
    public static Color GetColor(ItemGrade grade) => GetColor((int)grade);

    // 등급 한글 이름. 범위 밖이면 빈 문자열.
    public static string GetName(int grade)
        => (grade >= 0 && grade < Names.Length) ? Names[grade] : "";
    public static string GetName(ItemGrade grade) => GetName((int)grade);

    // 리치텍스트용 RRGGBB 16진 (예: <color=#xxxxxx>...). # 없이 6자리.
    public static string GetColorHex(int grade) => ColorUtility.ToHtmlStringRGB(GetColor(grade));
    public static string GetColorHex(ItemGrade grade) => GetColorHex((int)grade);

    // 일반(0) 등급인지. UI에서 테두리/오로라를 숨길지 판단용.
    public static bool IsCommon(int grade) => grade <= 0;
}

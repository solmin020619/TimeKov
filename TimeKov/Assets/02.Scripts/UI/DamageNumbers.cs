// =====================================================================
// DamageNumbers.cs
// 몬스터가 맞을 때 머리 위로 떠오르는 피해 숫자.
//
// [왜 여기 따로 두나]
//   띄우는 것 자체는 FloatingTextManager 가 이미 한다(플레이어 피해/회복 숫자).
//   다만 그쪽은 '화면 좌표'를 받는다. 몬스터는 월드에 있으므로 좌표 변환과
//   화면 밖 컷이 필요하고, 색/크기 규칙도 한 곳에 모여 있어야 나중에 조절이 쉽다.
//
// [보스도 포함된다]
//   보스 컨트롤러도 전부 EnemyHealth 를 쓴다. EnemyHealth.TakeDamage 한 곳에서
//   부르므로 일반몹/헬몹/필드몹/보스가 자동으로 다 나온다.
//
// [화면 밖은 그리지 않는다]
//   카메라 뒤나 화면 밖 몬스터까지 띄우면 숫자가 화면 가장자리에 뭉친다.
//   광역기로 여러 마리를 동시에 때릴 때 특히 지저분해진다.
// =====================================================================

using UnityEngine;

public static class DamageNumbers
{
    /// <summary>일반 피해 색. 어두운 배경에서도 읽히게 따뜻한 흰색.</summary>
    public static Color Normal = new Color(1f, 0.96f, 0.85f);

    /// <summary>치명타 색. 크기도 같이 커진다.</summary>
    public static Color Critical = new Color(1f, 0.58f, 0.18f);

    // 화면 밖 여유(px). 살짝 벗어난 것까지는 띄운다 - 가장자리에서 뚝 끊기면 더 어색하다.
    private const float ScreenMargin = 80f;

    private static Camera _cam;

    /// <summary>월드 좌표에 피해 숫자를 띄운다. 화면 밖이면 아무것도 하지 않는다.</summary>
    public static void Show(float amount, Vector3 worldPos, bool critical = false)
    {
        if (amount <= 0f) return;

        // Camera.main 은 매번 태그 탐색을 할 수 있어 타격마다 부르기엔 부담이다. 캐시한다.
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 sp = _cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) return;                                   // 카메라 뒤
        if (sp.x < -ScreenMargin || sp.x > Screen.width  + ScreenMargin) return;
        if (sp.y < -ScreenMargin || sp.y > Screen.height + ScreenMargin) return;

        int n = Mathf.Max(1, Mathf.RoundToInt(amount));
        string text = critical ? $"<size=130%>{n}</size>" : n.ToString();

        FloatingTextManager.Show(text, critical ? Critical : Normal, new Vector3(sp.x, sp.y, 0f));
    }
}

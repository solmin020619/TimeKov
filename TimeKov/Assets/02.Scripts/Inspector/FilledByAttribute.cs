using System;
using UnityEngine;

/// <summary>
/// "이 값은 실행하면 다른 데서 덮어쓴다" 표시.
///
/// 왜 필요한가: 인스펙터에 칸이 뚫려 있으면 거기서 조절되는 줄 알고 만진다.
/// 그런데 값의 진짜 주인이 SO/시트/세이브라 Awake 에서 덮어써 버리면
/// "고쳤는데 안 먹네 = 버그" 로 오해하게 된다. 실제로는 고칠 데를 잘못 찾은 것뿐이다.
///
/// 이 어트리뷰트를 달면 인스펙터에서 회색으로 잠기고,
/// 칸 아래에 "값을 바꾸려면: xxx" 라고 진짜 주인을 적어 준다.
/// 값은 계속 보이므로 플레이 중에 실제 적용값을 확인하는 용도로는 그대로 쓸 수 있다.
///
/// 아예 안 보여도 되는 값이면 이것 대신 [HideInInspector] 를 쓴다.
///
/// 사용 예:
///   [FilledBy("MeleeEnemyData.maxHP (몬스터 SO)")]
///   public float maxHP = 100f;
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class FilledByAttribute : PropertyAttribute
{
    /// <summary>진짜로 값을 고쳐야 하는 곳. 사람이 읽을 문장 그대로 적는다.</summary>
    public readonly string Source;

    public FilledByAttribute(string source)
    {
        Source = source;
    }
}

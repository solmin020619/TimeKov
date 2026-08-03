/// <summary>
/// 새 세이브 슬롯이 시작할 때의 플레이어 기본 스탯. **여기가 유일한 원본이다.**
///
/// 왜 모아뒀나: 예전에는 같은 숫자가 세 군데(씬 인스펙터 / PlayerStatComponent 코드 기본값 /
/// GameSaveData 기본값)에 흩어져 있었고, 셋을 손으로 맞춰야 했다. 하나만 고치면
/// "새로 시작한 슬롯" 과 "불러온 슬롯" 이 서로 다른 베이스로 시작하는 버그가 났다.
///
/// 이 값들은 앰플(소모품)로 영구 누적되는 스탯이라, 세이브가 있으면 저장값이 이긴다.
/// 즉 여기 숫자는 "아무것도 없는 상태에서 시작할 때" 만 쓰인다.
///
/// 최대 체력(시간)은 여기 없다. 그건 코어 강화 전용이고 CoreLevelData 시트의 maxTime 이 원본이다.
/// </summary>
public static class PlayerBaseStats
{
    /// <summary>기본 공격력.</summary>
    public const float ATK = 10f;

    /// <summary>기본 방어력.</summary>
    public const float DEF = 10f;

    /// <summary>기본 최대 스태미나.</summary>
    public const float MaxStamina = 100f;
}

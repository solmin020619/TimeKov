// =====================================================================
// ActiveBuff.cs
// 활성 버프 데이터 : ActiveBuffManager 가 목록으로 관리
// 버프 중첩 규칙
//   동일 itemId + 동일 effectTarget -> 교체 (시간 초기화)
//   다른 itemId + 동일 effectTarget -> 중첩 허용
// =====================================================================

using System;

[Serializable]
public class ActiveBuff
{
    // 어떤 스탯에 적용된 버프인지
    public EffectTarget target;

    // 실제로 적용된 수치 델타 (만료 시 역으로 원복)
    public float appliedDelta;

    // 남은 지속 시간 (초)
    public float remainingTime;

    // 버프 출처 아이템 ID
    public string sourceItemId;

    // 만료 여부
    public bool IsExpired => remainingTime <= 0f;
}
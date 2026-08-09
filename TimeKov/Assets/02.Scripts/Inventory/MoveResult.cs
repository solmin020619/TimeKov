// =====================================================================
// MoveResult.cs
// 아이템 일괄 이동 결과 구조체
// MoveAllTo / MoveFilteredTo 반환값
// UI 에서 IsCompleteFailure 기준으로 공간 부족 메시지 출력 여부를 결정한다
// =====================================================================

public struct MoveResult
{
    // 이동을 시도한 총 수량
    public int RequestedAmount;

    // 실제로 이동된 수량
    public int MovedAmount;

    // 이동하지 못한 수량
    public int FailedAmount;

    // 전혀 이동하지 못한 경우 : 공간 부족 메시지 표시 기준
    public bool IsCompleteFailure => MovedAmount == 0 && RequestedAmount > 0;

    // 일부만 이동된 경우 : 메시지 없이 UI 수량 변화로만 확인
    public bool IsPartialSuccess => MovedAmount > 0 && FailedAmount > 0;

    // 전부 이동 성공
    public bool IsFullSuccess => FailedAmount == 0 && MovedAmount > 0;
}
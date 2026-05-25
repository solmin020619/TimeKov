// =====================================================================
// PlayerStatWindowAdapter.cs
// C키로 여닫는 플레이어 스탯 패널 어댑터.
//
// 정보 전용 패널이라 게임 일시정지/입력 차단은 안 함.
// 다른 Window(인벤·공장)와 자유롭게 공존 가능.
//
// Inspector:
//   windowId = "PlayerStat"
//   layer = Window
//   pausesGame = false
//   locksGameplayInput = false   ← 정보용이라 게임 진행 그대로
//   panel = StatPanel GameObject
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class PlayerStatWindowAdapter : PanelWindowAdapter
{
    void Reset()
    {
        windowId = "PlayerStat";
        layer = UILayer.Window;
        pausesGame = false;
        locksGameplayInput = false;
    }
}

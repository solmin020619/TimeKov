// =====================================================================
// TimelinePuzzleCellInput.cs
// 퍼즐 격자 한 칸의 입력만 받아 TimelinePuzzleUI 로 넘긴다.
//
// [왜 파일을 따로 두나]
//   유니티는 MonoBehaviour 의 클래스명과 파일명이 같아야 스크립트 에셋으로 인식한다.
//   한 파일에 몰아 넣으면 AddComponent 는 되더라도 씬/프리팹 저장 시 참조가 끊긴다.
//   (이 프로젝트의 GameSettingsUI 쪽에도 같은 메모가 있다)
//
// [왜 EventTrigger 를 안 쓰나]
//   칸이 25개라 델리게이트를 25×2개 매달게 되고, 드래그 중 매 프레임 검사에 얹히는
//   할당이 늘어난다. 인터페이스로 직접 받는 게 가볍고 추적하기도 쉽다.
// =====================================================================

using UnityEngine;
using UnityEngine.EventSystems;

public class TimelinePuzzleCellInput : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    TimelinePuzzleUI _owner;
    int _row, _col;

    public void Bind(TimelinePuzzleUI owner, int row, int col)
    {
        _owner = owner; _row = row; _col = col;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e != null && e.button != PointerEventData.InputButton.Left) return;
        if (_owner != null) _owner.CellDown(_row, _col);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_owner != null) _owner.CellEnter(_row, _col);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_owner != null) _owner.CellExit(_row, _col);
    }
}

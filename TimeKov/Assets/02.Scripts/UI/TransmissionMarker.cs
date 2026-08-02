using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// [08-02] 시간에너지 전송기 - 진행 바 위의 보상 마일스톤 마커.
//
// 마커 위치(5/15/25/75 ...)는 TransmissionManager.RewardMilestones 가 정하므로 개수가 데이터다.
//   -> 빌더가 템플릿 1개를 씬에 만들어 꺼두고, 실행 시 마일스톤 수만큼 복제한다.
// 아이콘 3종(완료/다음/잠금)은 미리 다 만들어두고 상태에 따라 켜고 끈다
//   (예전엔 갱신할 때마다 Destroy 후 다시 만들었다).
public class TransmissionMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum State { Done, Next, Locked }

    public UnityEngine.UI.Outline chipOutline;   // 풀네임 필수(전역 3D Outline 이 가림)

    [Header("아이콘 3종 - 상태에 따라 하나만 켠다")]
    public GameObject iconDone;     // 각인 메달
    public GameObject iconNext;     // 타깃(조준)
    public GameObject iconLocked;   // "?"

    [Header("상태색을 따라가는 조각들")]
    public Image doneCoin;
    public Image nextRing;
    public Image nextDot;
    public TMP_Text lockedMark;

    private TransmissionComputerUI _owner;
    private int _pct;

    public int Pct => _pct;

    public void Bind(TransmissionComputerUI owner, int pct)
    {
        _owner = owner;
        _pct = pct;
    }

    public void SetState(State st, Color col)
    {
        if (chipOutline != null) chipOutline.effectColor = col;
        if (iconDone != null) iconDone.SetActive(st == State.Done);
        if (iconNext != null) iconNext.SetActive(st == State.Next);
        if (iconLocked != null) iconLocked.SetActive(st == State.Locked);

        if (st == State.Done)
        {
            if (doneCoin != null) doneCoin.color = col;
        }
        else if (st == State.Next)
        {
            if (nextRing != null) nextRing.color = col;
            if (nextDot != null) nextDot.color = col;
        }
        else if (lockedMark != null) lockedMark.color = col;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_owner != null) _owner.ShowTooltip(_pct, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_owner != null) _owner.HideTooltip();
    }
}

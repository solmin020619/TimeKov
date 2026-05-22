// UIButtonSound.cs
// 버튼에 붙이는 사운드 컴포넌트
// ButtonColorEffect.cs와 함께 사용 가능 (각자 독립 동작)
//
// [사용법]
//   1. 사운드를 붙이고 싶은 Button 오브젝트에 이 컴포넌트 추가
//   2. 클립 필드가 비어있으면 UISoundManager의 기본 클립 사용
//   3. 특정 버튼만 다른 소리를 쓰고 싶으면 overrideClickClip / overrideHoverClip 에 클립 할당

using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSound : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [Tooltip("비워두면 UISoundManager.buttonClickClip 사용")]
    [SerializeField] private AudioClip overrideClickClip;

    [Tooltip("비워두면 UISoundManager.buttonHoverClip 사용")]
    [SerializeField] private AudioClip overrideHoverClip;

    // ─── 이벤트 ──────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        // 좌클릭만 처리
        if (eventData.button != PointerEventData.InputButton.Left) return;

        var mgr = UISoundManager.Instance;
        if (mgr == null) return;

        if (overrideClickClip != null)
            mgr.PlayClip(overrideClickClip);
        else
            mgr.PlayButtonClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var mgr = UISoundManager.Instance;
        if (mgr == null) return;

        if (overrideHoverClip != null)
            mgr.PlayClip(overrideHoverClip);
        else
            mgr.PlayButtonHover();
    }
}

// =====================================================================
// UIButtonPressEffect.cs
// 버튼을 누르는 순간 살짝 작아졌다가 떼면 돌아오는 "눌린다" 촉감.
//
// [왜 필요한가]
//   지금 프로젝트의 UGUI Button 은 대부분 색만 살짝 바뀌거나 그것도 없다.
//   눌렀는데 화면이 그대로면 "먹었나?" 싶어서 두 번 누르게 된다.
//   설정창 버튼(GameSettingsUI.Btn)은 이미 눌림 스케일이 있는데, 그 감각을
//   프로젝트 전체 버튼으로 넓히는 것이 이 컴포넌트다.
//
// [붙이는 법]
//   손으로 안 붙인다. UIButtonPressInstaller 가 씬/프리팹/런타임 생성 구분 없이
//   Button 을 찾아 자동으로 붙인다. 특정 버튼만 빼려면 UIButtonPressEffectIgnore.
//
// [설계 메모]
//   1) 기준 크기를 Awake 가 아니라 '누르는 순간' 잡는다. 패널 열림 연출이 부모나
//      자기 크기를 건드리는 중일 수 있어서, 미리 잡아두면 엉뚱한 값이 기준이 된다.
//   2) localScale 만 쓴다. sizeDelta 를 건드리면 LayoutGroup 이 다시 계산되면서
//      옆 버튼들이 밀린다. 스케일은 레이아웃에 영향이 없다.
//   3) unscaledDeltaTime. 인벤/설정 등 timeScale=0 으로 멈춘 화면 위에서도 눌러야 한다.
//   4) 우리가 크기를 건드린 상태(_dirty)를 기억한다. 누른 채로 패널이 닫히면
//      작아진 채 굳어버리는데, OnDisable 에서 그때만 원복하면 남의 스케일을 밟지 않는다.
// =====================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// IBeginDragHandler 는 "드래그가 시작되면 눌림을 풀기" 위해서만 구현한다.
//   IDragHandler 는 일부러 구현하지 않는다. UGUI 는 IDragHandler 를 가진 오브젝트를
//   드래그 주인으로 잡는데, 여기서 구현해 버리면 스크롤뷰 안의 버튼이 드래그를 가로채
//   스크롤이 안 되는 회귀가 생긴다. IBeginDragHandler 만 있으면 주인 선정에 영향이 없고,
//   드래그 주인이 같은 오브젝트일 때만 알림을 받는다.
[DisallowMultipleComponent]
public class UIButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler
{
    /// <summary>눌렀을 때 크기(원래 크기 대비). 1 에 가까울수록 은근하다.</summary>
    public static float PressScale = 0.96f;

    /// <summary>줄어드는 시간(초). 손가락 감각이라 짧아야 한다.</summary>
    public static float DownTime = 0.05f;

    /// <summary>돌아오는 시간(초). 줄어들 때보다 조금 길어야 튕기는 느낌이 난다.</summary>
    public static float UpTime = 0.11f;

    private Selectable _target;
    private Vector3 _base = Vector3.one;
    private bool _held;      // 지금 누르고 있는 중
    private bool _dirty;     // 우리가 크기를 원래 값에서 벗어나게 해둔 상태
    private Coroutine _co;

    private void Awake()
    {
        _target = GetComponent<Selectable>();
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e != null && e.button != PointerEventData.InputButton.Left) return;
        if (_target != null && !_target.IsInteractable()) return;

        if (!_held) _base = transform.localScale;   // 설계 메모 1
        _held = true;
        Tween(_base * PressScale, DownTime);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_held) return;
        _held = false;
        Tween(_base, UpTime);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        // 아이템을 집어 끄는 것은 '누름'이 아니다. 드래그 내내 작아진 채로 남지 않게 바로 되돌린다.
        if (!_held) return;
        _held = false;
        Tween(_base, UpTime);
    }

    private void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        _held = false;
        if (_dirty)
        {
            transform.localScale = _base;
            _dirty = false;
        }
    }

    private void Tween(Vector3 to, float dur)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(TweenCo(to, dur));
    }

    private IEnumerator TweenCo(Vector3 to, float dur)
    {
        Vector3 from = transform.localScale;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = 1f - (1f - k) * (1f - k);           // OutQuad - 끝에서 부드럽게 멈춘다
            transform.localScale = Vector3.LerpUnclamped(from, to, k);
            _dirty = true;
            yield return null;
        }
        transform.localScale = to;
        _dirty = to != _base;
        _co = null;
    }
}

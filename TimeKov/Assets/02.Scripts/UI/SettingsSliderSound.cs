// SettingsSliderSound.cs
// 설정창 슬라이더에 붙이는 사운드 컴포넌트
// 슬라이더를 움직이다 멈추면(playDelay 초 뒤) 소리를 한 번 재생
//
// [사용법]
//   1. BGM 볼륨 슬라이더, SFX 볼륨 슬라이더, 마우스 감도 슬라이더 각각에 이 컴포넌트 추가
//   2. overrideClip이 비어있으면 UISoundManager.volumePreviewClip 사용
//   3. BGM 슬라이더는 BGM 계열 클립을, SFX 슬라이더는 SFX 계열 클립을 override에 넣으면 더 직관적

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class SettingsSliderSound : MonoBehaviour
{
    [Tooltip("비워두면 UISoundManager.volumePreviewClip 사용")]
    [SerializeField] private AudioClip overrideClip;

    [Tooltip("슬라이더가 멈춘 뒤 몇 초 후에 소리를 재생할지 (기본 0.35초)")]
    [SerializeField] private float playDelay = 0.35f;

    private Slider _slider;
    private Coroutine _delayCoroutine;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (_slider != null)
            _slider.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveListener(OnValueChanged);

        if (_delayCoroutine != null)
        {
            StopCoroutine(_delayCoroutine);
            _delayCoroutine = null;
        }
    }

    // ─── 슬라이더 값 변경 시 ─────────────────────────────────────────────────

    private void OnValueChanged(float _)
    {
        // 이전 대기 코루틴 취소 후 다시 시작 (슬라이더 이동 중에는 소리 안 남)
        if (_delayCoroutine != null)
            StopCoroutine(_delayCoroutine);

        _delayCoroutine = StartCoroutine(DelayedPlay());
    }

    private IEnumerator DelayedPlay()
    {
        yield return new WaitForSecondsRealtime(playDelay);

        var mgr = UISoundManager.Instance;
        if (mgr == null) yield break;

        if (overrideClip != null)
            mgr.PlayClip(overrideClip);
        else
            mgr.PlayVolumePreview();

        _delayCoroutine = null;
    }
}

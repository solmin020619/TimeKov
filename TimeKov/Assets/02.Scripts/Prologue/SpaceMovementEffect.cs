using UnityEngine;

/// <summary>
/// 프롤로그 씬의 우주 이동감 연출.
/// 스카이박스를 일정하지 않은 속도로 회전시켜 "원을 그리는" 느낌을 방지하고,
/// 별 파티클이 빠르게 쏟아져 추락 중인 우주선 느낌을 준다.
/// </summary>
public class SpaceMovementEffect : MonoBehaviour
{
    [Header("Skybox Rotation")]
    [Tooltip("기본 회전 속도 (°/s)")]
    [SerializeField] float _skyboxRotateSpeed = 12f;
    [Tooltip("속도 변동폭 — 기본 속도 ±이 값으로 불규칙하게 가속/감속 (°/s)")]
    [SerializeField] float _speedVariance = 8f;
    [Tooltip("변동 주기 (Hz). 작을수록 느리게 출렁임")]
    [SerializeField] float _varianceFreq = 0.12f;

    [Header("Star Particles")]
    [SerializeField] ParticleSystem _starParticles;

    float _currentRotation;

    void Start()
    {
        _currentRotation = RenderSettings.skybox != null
            ? RenderSettings.skybox.GetFloat("_Rotation")
            : 0f;

        _starParticles?.Play();
    }

    void Update()
    {
        if (RenderSettings.skybox == null) return;

        // 불규칙한 속도 — Sin 변조로 일정한 원 궤적 느낌 제거
        float speed = _skyboxRotateSpeed
                    + _speedVariance * Mathf.Sin(Time.time * _varianceFreq * Mathf.PI * 2f);

        _currentRotation += speed * Time.deltaTime;
        if (_currentRotation >= 360f) _currentRotation -= 360f;
        RenderSettings.skybox.SetFloat("_Rotation", _currentRotation);
    }

    void OnDestroy()
    {
        RenderSettings.skybox?.SetFloat("_Rotation", 0f);
    }
}

using UnityEngine;

public class SpaceMovementEffect : MonoBehaviour
{
    [Header("Skybox Rotation")]
    [SerializeField] float _skyboxRotateSpeed = 1.5f;

    [Header("Star Particles")]
    [SerializeField] ParticleSystem _starParticles;

    float _currentRotation;

    void Start()
    {
        _currentRotation = RenderSettings.skybox != null
            ? RenderSettings.skybox.GetFloat("_Rotation")
            : 0f;

        if (_starParticles != null)
            _starParticles.Play();
    }

    void Update()
    {
        if (RenderSettings.skybox == null) return;
        _currentRotation += _skyboxRotateSpeed * Time.deltaTime;
        if (_currentRotation >= 360f) _currentRotation -= 360f;
        RenderSettings.skybox.SetFloat("_Rotation", _currentRotation);
    }

    void OnDestroy()
    {
        // 씬 종료 시 회전값 초기화
        if (RenderSettings.skybox != null)
            RenderSettings.skybox.SetFloat("_Rotation", 0f);
    }
}

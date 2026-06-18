// MinimapController.cs
// 미니맵 카메라를 플레이어 위에 고정시키고 플레이어 방향 아이콘을 회전시킴

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera    _minimapCamera;
    [SerializeField] private Transform _playerIcon;   // 미니맵 UI 위의 플레이어 화살표 아이콘

    [Header("Settings")]
    [SerializeField] private float _height      = 30f;  // 카메라가 플레이어 위 몇 m
    [SerializeField] private float _orthoSize   = 20f;  // 시야 범위 (m 단위)
    [SerializeField] private bool  _rotateMap   = false; // true = 지도가 플레이어 방향으로 회전

    [Header("Performance")]
    [SerializeField] private float _renderInterval = 0.1f; // 렌더링 주기 (초). 0.1 = 10fps

    /// <summary>MinimapUI에서 아이콘 위치 계산에 사용</summary>
    public Camera MinimapCamera => _minimapCamera;

    private Transform _playerTransform;
    private float _nextRenderTime;

    void Awake()
    {
        if (_minimapCamera != null)
        {
            _minimapCamera.orthographicSize = _orthoSize;
            _minimapCamera.enabled  = false;
            _minimapCamera.allowHDR  = false;
            _minimapCamera.allowMSAA = false;

            var urpData = _minimapCamera.GetUniversalAdditionalCameraData();
            if (urpData != null) urpData.renderShadows = false;
        }
    }

    void Start()
    {
        var player = FindAnyObjectByType<Player>();
        if (player != null) _playerTransform = player.transform;
    }

    void LateUpdate()
    {
        if (_playerTransform == null) return;

        // 카메라를 플레이어 바로 위에 고정 (transform은 매 프레임 — MinimapUI 아이콘 계산에 필요)
        Vector3 pos = _playerTransform.position;
        pos.y += _height;
        _minimapCamera.transform.position = pos;

        if (_rotateMap)
        {
            float yAngle = _playerTransform.eulerAngles.y;
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, yAngle, 0f);

            if (_playerIcon != null)
                _playerIcon.localRotation = Quaternion.identity;
        }
        else
        {
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (_playerIcon != null)
            {
                float yAngle = _playerTransform.eulerAngles.y;
                _playerIcon.localRotation = Quaternion.Euler(0f, 0f, -yAngle);
            }
        }

        // 렌더링은 _renderInterval 주기로만 수행
        if (Time.time >= _nextRenderTime)
        {
            _nextRenderTime = Time.time + _renderInterval;
            _minimapCamera.Render();
        }
    }

    // 외부에서 시야 범위 동적 변경
    public void SetZoom(float size)
    {
        _orthoSize = Mathf.Max(5f, size);
        if (_minimapCamera != null)
            _minimapCamera.orthographicSize = _orthoSize;
    }
}

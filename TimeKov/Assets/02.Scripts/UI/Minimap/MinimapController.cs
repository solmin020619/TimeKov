// MinimapController.cs
// 미니맵 카메라를 플레이어 위에 고정시키고 플레이어 방향 아이콘을 회전시킴

using UnityEngine;

public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera    _minimapCamera;
    [SerializeField] private Transform _playerIcon;   // 미니맵 UI 위의 플레이어 화살표 아이콘

    [Header("Settings")]
    [SerializeField] private float _height      = 30f;  // 카메라가 플레이어 위 몇 m
    [SerializeField] private float _orthoSize   = 20f;  // 시야 범위 (m 단위)
    [SerializeField] private bool  _rotateMap   = false; // true = 지도가 플레이어 방향으로 회전

    /// <summary>MinimapUI에서 아이콘 위치 계산에 사용</summary>
    public Camera MinimapCamera => _minimapCamera;

    private Transform _playerTransform;

    void Awake()
    {
        if (_minimapCamera != null)
            _minimapCamera.orthographicSize = _orthoSize;
    }

    void Start()
    {
        var player = FindAnyObjectByType<Player>();
        if (player != null) _playerTransform = player.transform;
    }

    void LateUpdate()
    {
        if (_playerTransform == null) return;

        // 카메라를 플레이어 바로 위에 고정
        Vector3 pos = _playerTransform.position;
        pos.y += _height;
        _minimapCamera.transform.position = pos;

        if (_rotateMap)
        {
            // 지도가 플레이어 방향에 따라 회전 (나침반형)
            float yAngle = _playerTransform.eulerAngles.y;
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, yAngle, 0f);

            // 플레이어 아이콘은 항상 위쪽을 가리킴
            if (_playerIcon != null)
                _playerIcon.localRotation = Quaternion.identity;
        }
        else
        {
            // 지도는 항상 북쪽 고정, 플레이어 아이콘이 회전
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (_playerIcon != null)
            {
                float yAngle = _playerTransform.eulerAngles.y;
                _playerIcon.localRotation = Quaternion.Euler(0f, 0f, -yAngle);
            }
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

// MinimapUI.cs
// 미니맵 아이콘(마커) 위치 업데이트 관리
// - MinimapMarker를 가진 적/NPC/건물의 아이콘을 미니맵에 동적으로 생성·배치
// - 카메라 회전 여부(_rotateMap)를 자동으로 반영
// ─────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapUI : MonoBehaviour
{
    // ── 싱글턴 ────────────────────────────────────────────────────
    public static MinimapUI Instance { get; private set; }

    // ── 인스펙터 ──────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("MinimapCamera 오브젝트의 Camera 컴포넌트")]
    [SerializeField] private Camera _minimapCamera;

    [Tooltip("아이콘이 배치될 부모 RectTransform (미니맵 패널과 동일 크기, 중앙 앵커)")]
    [SerializeField] private RectTransform _iconContainer;

    [Header("Default Icons (선택)")]
    [SerializeField] private Sprite _enemyIcon;
    [SerializeField] private Sprite _npcIcon;
    [SerializeField] private Sprite _buildingIcon;
    [SerializeField] private Sprite _itemIcon;

    [Header("Icon Settings")]
    [Tooltip("아이콘 Image 컴포넌트를 가진 프리팹 (없으면 빈 GameObject + Image 자동 생성)")]
    [SerializeField] private GameObject _iconPrefab;

    [Tooltip("아이콘 크기 (pixels)")]
    [SerializeField] private float _iconSize = 12f;

    [Tooltip("true = 범위 밖 마커를 원 테두리에 고정 / false = 범위 밖이면 숨김")]
    [SerializeField] private bool _clampToEdge = true;

    // ── 런타임 ───────────────────────────────────────────────────
    // static pending 리스트: MinimapUI.Awake 이전에 등록된 마커 임시 보관
    private static readonly List<MinimapMarker> _pendingMarkers = new();
    private readonly Dictionary<MinimapMarker, RectTransform> _iconMap = new();

    private float _halfW;
    private float _halfH;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // 씬 재로드 시 이전 씬의 잔류 항목 방지
        _pendingMarkers.Clear();
    }

    void Start()
    {
        // 아이콘 컨테이너가 없으면 자신의 RectTransform 사용
        var refRect = _iconContainer != null
                    ? _iconContainer
                    : GetComponent<RectTransform>();

        _halfW = refRect.rect.width  * 0.5f;
        _halfH = refRect.rect.height * 0.5f;

        // Awake 이전에 들어온 마커들 처리
        foreach (var m in _pendingMarkers)
            CreateIcon(m);
        _pendingMarkers.Clear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (_minimapCamera == null) return;
        UpdateMarkerIcons();
    }

    // ── 마커 등록/해제 (MinimapMarker에서 호출) ──────────────────

    public static void RegisterMarker(MinimapMarker marker)
    {
        if (Instance != null)
            Instance.CreateIcon(marker);
        else
            _pendingMarkers.Add(marker);
    }

    public static void UnregisterMarker(MinimapMarker marker)
    {
        _pendingMarkers.Remove(marker);
        Instance?.RemoveIcon(marker);
    }

    // ── 내부: 아이콘 생성/삭제 ───────────────────────────────────

    void CreateIcon(MinimapMarker marker)
    {
        if (_iconContainer == null) return;
        if (_iconMap.ContainsKey(marker)) return;

        // 프리팹이 없으면 Image 컴포넌트만 가진 빈 오브젝트 생성
        GameObject go;
        if (_iconPrefab != null)
        {
            go = Instantiate(_iconPrefab, _iconContainer);
        }
        else
        {
            go = new GameObject("MinimapIcon");
            go.transform.SetParent(_iconContainer, false);
            go.AddComponent<Image>();
        }
        go.name = $"MinimapIcon_{marker.Type}_{marker.name}";

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = Vector2.one * _iconSize;

        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = marker.Icon != null ? marker.Icon : GetDefaultSprite(marker.Type);
            img.color  = marker.IconColor;
            img.raycastTarget = false;
        }

        _iconMap[marker] = rt;
    }

    void RemoveIcon(MinimapMarker marker)
    {
        if (_iconMap.TryGetValue(marker, out var rt))
        {
            if (rt != null) Destroy(rt.gameObject);
            _iconMap.Remove(marker);
        }
    }

    // ── 내부: 위치 업데이트 ──────────────────────────────────────

    void UpdateMarkerIcons()
    {
        // 카메라의 right/up 월드 벡터로 아이콘 위치 계산
        // → _rotateMap 여부와 무관하게 자동으로 올바른 방향 반영
        Vector3 camRight = _minimapCamera.transform.right;
        Vector3 camUp    = _minimapCamera.transform.up;
        Vector3 camPos   = _minimapCamera.transform.position;
        float   ortho    = _minimapCamera.orthographicSize;

        List<MinimapMarker> toRemove = null;

        foreach (var kvp in _iconMap)
        {
            var marker = kvp.Key;
            var rt     = kvp.Value;

            // 오브젝트가 파괴된 경우 정리 예약
            if (marker == null || rt == null)
            {
                (toRemove ??= new List<MinimapMarker>()).Add(marker);
                continue;
            }

            Vector3 delta = marker.transform.position - camPos;

            // 카메라 right/up 기준 정규화 좌표 (−1 ~ 1)
            float nx = Vector3.Dot(delta, camRight) / ortho;
            float ny = Vector3.Dot(delta, camUp)    / ortho;

            if (_clampToEdge)
            {
                // 원형 테두리 클램핑: 범위 밖이면 경계선 위로
                var n = new Vector2(nx, ny);
                if (n.sqrMagnitude > 1f) n = n.normalized;
                nx = n.x;
                ny = n.y;
                rt.gameObject.SetActive(true);
            }
            else
            {
                // 범위 밖 숨김
                bool outside = nx < -1f || nx > 1f || ny < -1f || ny > 1f;
                rt.gameObject.SetActive(!outside);
                nx = Mathf.Clamp(nx, -1f, 1f);
                ny = Mathf.Clamp(ny, -1f, 1f);
            }

            rt.anchoredPosition = new Vector2(nx * _halfW, ny * _halfH);
        }

        // null 마커 정리
        if (toRemove != null)
            foreach (var m in toRemove)
                _iconMap.Remove(m);
    }

    Sprite GetDefaultSprite(MinimapMarker.MarkerType type) => type switch
    {
        MinimapMarker.MarkerType.Enemy    => _enemyIcon,
        MinimapMarker.MarkerType.NPC      => _npcIcon,
        MinimapMarker.MarkerType.Building => _buildingIcon,
        MinimapMarker.MarkerType.Item     => _itemIcon,
        _                                 => null,
    };
}

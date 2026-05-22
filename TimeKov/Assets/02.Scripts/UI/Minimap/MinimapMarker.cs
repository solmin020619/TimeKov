// MinimapMarker.cs
// 미니맵에 표시될 월드 오브젝트에 붙이는 컴포넌트
// OnEnable/OnDisable에서 MinimapUI에 자동 등록/해제

using UnityEngine;

public class MinimapMarker : MonoBehaviour
{
    public enum MarkerType { Enemy, NPC, Building, Item }

    [Header("Marker Settings")]
    [Tooltip("미니맵 아이콘 종류 (기본 아이콘 색상/스프라이트 결정)")]
    public MarkerType Type = MarkerType.Enemy;

    [Tooltip("null이면 MinimapUI의 기본 아이콘 사용")]
    public Sprite Icon;

    [Tooltip("아이콘 색상 (기본: 빨강)")]
    public Color IconColor = Color.red;

    void OnEnable()  => MinimapUI.RegisterMarker(this);
    void OnDisable() => MinimapUI.UnregisterMarker(this);
}

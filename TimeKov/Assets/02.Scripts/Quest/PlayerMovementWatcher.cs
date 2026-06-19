using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 입력/이동을 외부에서 관찰해 InputBus/GameEvents 에 발화하는 옵저버.
/// 튜토리얼 objective(PressKey/MoveDistance)가 이걸 통해 입력을 받는다.
/// </summary>
public class PlayerMovementWatcher : MonoBehaviour
{
    [SerializeField] Transform playerTransform;

    [Tooltip("추가로 관찰할 키. 아래 필수 키(이동/점프/달리기/대시/Tab/B 등)는 코드에서 항상 자동 포함된다.")]
    [SerializeField] KeyCode[] watchedKeys = new KeyCode[0];

    // 튜토리얼이 의존하는 필수 키 - 씬 배열이 비거나 오래돼도 항상 감지되게 코드에서 보강.
    // (이게 없으면 인스펙터에서 키 하나 빠뜨리면 해당 단계가 완료 안 됨. 예: LeftShift 누락 -> 달리기 안 깨짐)
    static readonly KeyCode[] RequiredKeys =
    {
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.Space, KeyCode.LeftShift,
        KeyCode.B, KeyCode.E, KeyCode.F, KeyCode.Tab,
        KeyCode.Mouse0, KeyCode.Mouse1,
    };

    KeyCode[] _polled;
    Vector3 _lastPos;

    void Start()
    {
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else { Debug.LogError("[PlayerMovementWatcher] Player Transform 못 찾음"); enabled = false; return; }
        }

        // 필수 키 + 인스펙터 추가 키 합집합 (중복 발화 방지 위해 HashSet)
        var set = new HashSet<KeyCode>(RequiredKeys);
        if (watchedKeys != null)
            foreach (var k in watchedKeys) set.Add(k);
        _polled = new KeyCode[set.Count];
        set.CopyTo(_polled);

        _lastPos = playerTransform.position;
    }

    void Update()
    {
        if (playerTransform == null || _polled == null) return;

        // 입력 폴링, InputBus
        foreach (var k in _polled)
            if (Input.GetKeyDown(k)) InputBus.RaiseKeyDown(k);

        // XZ 평면 이동 거리, GameEvents.RaiseMovedDelta
        var pos = playerTransform.position;
        var flat = new Vector3(pos.x - _lastPos.x, 0, pos.z - _lastPos.z);
        float dist = flat.magnitude;
        if (dist > 0.001f) GameEvents.RaiseMovedDelta(dist);

        _lastPos = pos;
    }
}

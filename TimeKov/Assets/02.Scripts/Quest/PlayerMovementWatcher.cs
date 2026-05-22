using UnityEngine;

/// <summary>
/// Player/ 폴더 수정 금지 제약 우회용 옵저버.
/// 플레이어 Transform 외부에서 관찰, InputBus/GameEvents에 발화.
/// 나중에 Player/ 수정 가능해지면 PlayerController가 직접 Raise* 호출하도록 이전.
/// </summary>
public class PlayerMovementWatcher : MonoBehaviour
{
    [SerializeField] Transform playerTransform;
    [Tooltip("관찰할 키 목록. InputBus에 발화")]
    [SerializeField] KeyCode[] watchedKeys = new[]
    {
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.Space, KeyCode.B, KeyCode.E, KeyCode.F,
        KeyCode.Tab,
        KeyCode.Mouse0, KeyCode.Mouse1
    };

    Vector3 _lastPos;

    void Start()
    {
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else { Debug.LogError("[PlayerMovementWatcher] Player Transform 못 찾음"); enabled = false; return; }
        }
        _lastPos = playerTransform.position;
    }

    void Update()
    {
        if (playerTransform == null) return;

        // 입력 폴링, InputBus
        foreach (var k in watchedKeys)
            if (Input.GetKeyDown(k)) InputBus.RaiseKeyDown(k);

        // XZ 평면 이동 거리, GameEvents.RaiseMovedDelta
        var pos = playerTransform.position;
        var flat = new Vector3(pos.x - _lastPos.x, 0, pos.z - _lastPos.z);
        float dist = flat.magnitude;
        if (dist > 0.001f) GameEvents.RaiseMovedDelta(dist);

        _lastPos = pos;
    }
}

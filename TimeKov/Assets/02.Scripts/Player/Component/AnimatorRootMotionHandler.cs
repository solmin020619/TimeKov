using UnityEngine;

// 캐릭터 메시 오브젝트에 붙이는 스크립트
// Animator의 루트 모션을 제어 — 공격 중에만 XZ 이동 적용
//
// [구조 개선]
//   - OnAnimatorMove(Update 주기)에서 deltaPosition을 누적만 함
//   - 실제 MovePosition은 FixedUpdate(물리 주기)에서 처리 → 물리 일관성 확보
//   - 공격 종료 직후 잔여 누적량 + 속도 강제 초기화 → 슬라이딩 방지
//
// [슬라이딩 원인이었던 것]
//   Base Layer(Walk/Run 루트모션) + Action Layer(Attack 루트모션)가
//   _anim.deltaPosition에 합산되어, 공격 중 이동 방향이 비정상적으로 섞임
//   → GS_Idle/Walk/Run/Sprint .fbx.meta 의 keepOriginalPositionXZ=1 로 수정하여
//     Base Layer 애니메이션의 XZ 루트모션을 제거함 (별도 수정 완료)
public class AnimatorRootMotionHandler : MonoBehaviour
{
    [Header("Settings")]
    public float AttackRootMotionScale = 0.3f;  // 공격 중 루트 모션 비율 (0=비활성, 1=원본 100%)

    private Animator _anim;
    private Rigidbody _rb;
    private Player _player;

    // OnAnimatorMove(Update 주기) → FixedUpdate 전달용 버퍼
    private Vector3 _pendingDeltaPos;
    private bool _wasExecuting;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponentInParent<Rigidbody>();
        _player = GetComponentInParent<Player>();
    }

    // Unity가 애니메이션 평가 후 호출 (Update 주기)
    // MovePosition을 여기서 직접 호출하지 않고 누적만 함 (물리 타이밍 불일치 방지)
    void OnAnimatorMove()
    {
        if (_player == null || _rb == null) return;

        if (_player.Skill.IsExecuting)
        {
            Vector3 deltaPos = _anim.deltaPosition * AttackRootMotionScale;
            deltaPos.y = 0f;    // 수직 이동은 Rigidbody 물리에 맡김
            _pendingDeltaPos += deltaPos;
        }
    }

    // 물리 연산 주기(FixedUpdate)에서 실제 위치 적용
    void FixedUpdate()
    {
        if (_player == null || _rb == null) return;

        bool isExecuting = _player.Skill.IsExecuting;

        // 공격 종료 직후 → 잔여 루트모션 + X/Z 속도 완전 초기화 (슬라이딩 방지)
        if (_wasExecuting && !isExecuting)
        {
            _pendingDeltaPos = Vector3.zero;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
        _wasExecuting = isExecuting;

        // 공격 중 누적된 루트모션을 물리 주기에서 안전하게 적용
        if (isExecuting && _pendingDeltaPos.sqrMagnitude > 0.00001f)
        {
            _rb.MovePosition(_rb.position + _pendingDeltaPos);
            _pendingDeltaPos = Vector3.zero;
        }
    }
}

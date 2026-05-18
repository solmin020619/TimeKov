using UnityEngine;

// 캐릭터 메시 오브젝트에 붙이는 스크립트
// Animator와 같은 오브젝트에 있어야 함
public class AnimatorRootMotionHandler : MonoBehaviour
{
    [Header("Settings")]
    public float AttackRootMotionScale = 0.3f;  // 공격 중 루트 모션 배율 (1이면 원본, 낮출수록 덜 이동)

    private Animator _anim;
    private Rigidbody _rb;
    private Player _player;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponentInParent<Rigidbody>();
        _player = GetComponentInParent<Player>();
    }

    void OnAnimatorMove()
    {
        if (_player == null || _rb == null) return;

        if (_player.Skill.IsExecuting)
        {
            // 공격 중에만 루트 모션 적용, 배율로 속도 조절
            Vector3 deltaPos = _anim.deltaPosition * AttackRootMotionScale;
            deltaPos.y = 0f;    // 수직 이동은 Rigidbody에 맡김
            _rb.MovePosition(_rb.position + deltaPos);
        }

        // 일반 이동 중엔 루트 모션 무시
        // PlayerMovementComponent가 직접 처리
    }
}
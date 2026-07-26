using System.Collections;
using UnityEngine;

/// <summary>
/// 용암 콜라이더에 부착. 플레이어가 닿으면 화상 DoT 발생.
/// - 용암 위에 있는 동안 지속 데미지
/// - 이탈 후에도 BurnDuration 초 동안 화상 유지
/// - DEF 무시 (SpendHp 사용)
/// </summary>
public class LavaDamageZone : MonoBehaviour
{
    [Header("DoT 설정")]
    [SerializeField] float _damagePerTick = 10f;
    [SerializeField] float _tickInterval  = 1f;
    [SerializeField] float _burnDuration  = 3f;

    [Header("화염 VFX")]
    [SerializeField] GameObject _fireVfxPrefab;
    [SerializeField] Vector3    _fireVfxOffset = new Vector3(0f, 0.5f, 0f);

    bool                _inLava;
    PlayerStatComponent _stat;
    GameObject          _playerGO;
    Coroutine           _burnCoroutine;
    GameObject          _fireVfxInstance;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (_stat == null)
        {
            _stat = other.GetComponentInParent<PlayerStatComponent>()
                 ?? other.GetComponent<PlayerStatComponent>();
            _playerGO = _stat != null ? _stat.gameObject : null;
        }
        if (_stat == null) return;

        _inLava = true;

        if (_burnCoroutine != null) StopCoroutine(_burnCoroutine);
        _burnCoroutine = StartCoroutine(BurnRoutine());
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _inLava = false;
    }

    IEnumerator BurnRoutine()
    {
        if (_fireVfxInstance != null) Destroy(_fireVfxInstance);
        if (_fireVfxPrefab != null && _playerGO != null)
        {
            _fireVfxInstance = VfxUtils.SpawnAtCaster(
                _fireVfxPrefab, _playerGO, _fireVfxOffset, 9999f, true);
            if (_fireVfxInstance != null)
            {
                foreach (var ps in _fireVfxInstance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = true;

                    var em = ps.emission;
                    em.enabled = true;
                    if (em.rateOverTime.constantMax < 1f)
                        em.rateOverTime = 12f;
                }
            }
        }

        float tickAccum  = 0f;
        float leaveTimer = _burnDuration;

        while (true)
        {
            if (_stat == null || _stat.IsDead) break;

            if (_inLava)
            {
                leaveTimer = _burnDuration;
            }
            else
            {
                leaveTimer -= Time.deltaTime;
                if (leaveTimer <= 0f) break;
            }

            tickAccum += Time.deltaTime;
            if (tickAccum >= _tickInterval)
            {
                _stat.SpendHp(_damagePerTick);
                tickAccum -= _tickInterval;
            }

            yield return null;
        }

        if (_fireVfxInstance != null)
        {
            Destroy(_fireVfxInstance);
            _fireVfxInstance = null;
        }

        _burnCoroutine = null;
    }
}

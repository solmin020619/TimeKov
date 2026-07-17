using UnityEngine;

// 보스 리쉬(이탈) 타이머.
// 교전 후 플레이어가 distance 밖에 resetTime 만큼 머물면 리셋 신호를 준다.
// (보스 피 깎고 도망 -> 재접근 무한반복 익스플로잇 차단. 팰월드/마크식 풀피 복귀.)
//
// ★일부러 "타이머 + 스폰 자리 보관"만 한다. 리셋 동작 자체는 각 보스 컨트롤러에 남긴다.
// 이유: 실제 리셋의 대부분(코루틴 정지 / 연출 중단 복구 / 페이즈·광폭화·쿨 초기화)이
// 보스마다 완전히 다르다. 그걸 억지로 콜백으로 뽑으면 결합만 늘고 읽기 어려워진다.
// 공통분(HP 복구 / Warp 복귀 / 정지)은 BossMotor.ResetToSpawn 이 담당.
//
// 사용법:
//   Start:  _leash.Capture(transform);
//   Update: if (_engaged && _leash.Tick(_motor.PlanarDistance(player.position))) ResetBoss();
public class BossLeash
{
    private readonly float _distance;
    private readonly float _resetTime;
    private float _timer;

    public Vector3 SpawnPos { get; private set; }
    public Quaternion SpawnRot { get; private set; } = Quaternion.identity;

    public BossLeash(float distance, float resetTime)
    {
        _distance = distance;
        _resetTime = resetTime;
    }

    // 스폰 자리 기억(리셋 시 돌아올 곳). Start 에서 1회.
    public void Capture(Transform tr)
    {
        SpawnPos = tr.position;
        SpawnRot = tr.rotation;
    }

    public void Clear() => _timer = 0f;

    // 리셋해야 하면 true. 호출자가 실제 리셋을 수행한다.
    public bool Tick(float planarDistanceToPlayer)
    {
        if (planarDistanceToPlayer > _distance)
        {
            _timer += Time.deltaTime;
            if (_timer >= _resetTime) { _timer = 0f; return true; }
        }
        else _timer = 0f;
        return false;
    }
}

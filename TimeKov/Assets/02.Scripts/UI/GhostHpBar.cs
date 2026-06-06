using UnityEngine;
using UnityEngine.UI;

// HP 바 피격 잔상(데미지 칩) 효과.
// 피격으로 HP가 갑자기 깎이면 깎이기 직전 위치에 잔상을 남기고, 잠시 멈췄다가 현재 HP까지 천천히 줄어든다.
// 평소엔 잔상 Image 를 꺼두고, 피격(OnHurt) 순간에만 켠다. 시간 자연 감소(decay)에는 반응하지 않음.
//
// 정렬: 잔상 Image 를 메인 Fill 과 "똑같은 방식(Filled / fillAmount)" 으로 두고 Fill 바로 뒤에 깐다.
//       같은 스프라이트 기하를 쓰므로 fillAmount 가 메인 Fill 과 정확히 일치한다(틈/넘침 없음).
//       단, Fill 이 불투명해야 [0~현재] 구간의 잔상이 비치지 않고 칩([현재~잔상])만 보인다.
//       -> Fill 복제본을 잔상으로 쓰고, 메인 Fill 은 불투명, 숫자 텍스트는 바보다 위에 둘 것.
public class GhostHpBar : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("PlayerStatComponent. 비워두면 씬에서 자동 탐색")]
    [SerializeField] private PlayerStatComponent playerStat;
    [Tooltip("잔상 Image (메인 Fill 복제본, Fill 바로 뒤에 배치)")]
    [SerializeField] private Image ghostImage;

    [Header("잔상 설정")]
    [SerializeField] private Color ghostColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [Tooltip("피격 후 잔상이 그대로 멈춰있는 시간 (초)")]
    [SerializeField] private float holdDuration = 0.4f;
    [Tooltip("멈춤이 끝난 뒤 잔상이 현재 HP까지 줄어드는 속도 (전체 비율 / 초)")]
    [SerializeField] private float drainSpeed = 0.7f;

    private float _ghostFill;
    private float _fillLastFrame;
    private float _holdTimer;
    private bool _ghosting;

    private void Start()
    {
        if (playerStat == null)
        {
            var p = FindAnyObjectByType<Player>();
            if (p != null) playerStat = p.GetComponent<PlayerStatComponent>();
        }

        if (playerStat != null)
            playerStat.OnHurt += OnHurt;

        if (ghostImage != null)
        {
            ghostImage.color = ghostColor;
            // 메인 Fill 과 동일한 채움 방식 -> fillAmount 가 정확히 정렬됨
            ghostImage.type = Image.Type.Filled;
            ghostImage.fillMethod = Image.FillMethod.Horizontal;
            ghostImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            ghostImage.enabled = false;   // 평소엔 숨김 (맞을 때만)
        }

        _ghostFill = _fillLastFrame = CurrentFill();
    }

    private void OnDestroy()
    {
        if (playerStat != null)
            playerStat.OnHurt -= OnHurt;
    }

    private void OnHurt()
    {
        if (ghostImage == null) return;

        _ghostFill = Mathf.Max(_ghostFill, _fillLastFrame);
        _holdTimer = holdDuration;
        _ghosting = true;
        ghostImage.enabled = true;
        ghostImage.fillAmount = _ghostFill;
    }

    private void Update()
    {
        if (playerStat == null) return;

        float cur = CurrentFill();

        if (!_ghosting)
        {
            _ghostFill = cur;
            _fillLastFrame = cur;
            return;
        }

        _fillLastFrame = cur;

        if (cur >= _ghostFill)
        {
            EndGhost();
            return;
        }

        if (_holdTimer > 0f)
        {
            _holdTimer -= Time.deltaTime;
        }
        else
        {
            _ghostFill = Mathf.MoveTowards(_ghostFill, cur, drainSpeed * Time.deltaTime);
            if (_ghostFill <= cur + 0.0005f) { EndGhost(); return; }
        }

        ghostImage.fillAmount = _ghostFill;
    }

    private void EndGhost()
    {
        _ghosting = false;
        _ghostFill = CurrentFill();
        if (ghostImage != null) ghostImage.enabled = false;
    }

    private float CurrentFill()
    {
        if (playerStat == null || playerStat.MaxHp <= 0f) return 1f;
        return Mathf.Clamp01(playerStat.CurrentHp / playerStat.MaxHp);
    }
}

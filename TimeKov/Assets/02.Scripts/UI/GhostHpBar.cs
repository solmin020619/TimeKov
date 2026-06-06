using UnityEngine;
using UnityEngine.UI;

// HP 바 변화 연출 (피격 데미지 잔상 + 회복 채움).
// - 피격: 깎이기 직전 위치에 "빨간" 잔상을 남기고, 멈췄다가 현재 HP까지 감소. (파란 바는 즉시 깎임)
// - 회복: 회복분만큼 "초록"이 먼저 깔리고, 파란 바가 천천히 차오르며 초록을 채워나감.
//
// 잔상 Image 하나를 빨강/초록 겸용으로 쓴다 (메인 Fill 복제본, Fill 바로 뒤에 배치).
// 회복 채움을 위해 메인 Fill 의 fillAmount 를 직접 제어한다 (감소는 즉시, 증가는 천천히).
// 메인 Fill 미연결 시 회복 연출만 생략되고 나머지는 정상 동작.
public class GhostHpBar : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("PlayerStatComponent. 비워두면 씬에서 자동 탐색")]
    [SerializeField] private PlayerStatComponent playerStat;
    [Tooltip("잔상 Image (Fill 복제본, Fill 뒤). 피격=빨강 / 회복=초록 으로 자동 전환")]
    [SerializeField] private Image ghostImage;
    [Tooltip("메인 HP Fill (파란 바). 회복 시 천천히 차오르게 하려고 fillAmount 를 제어함. 비우면 회복 연출 생략")]
    [SerializeField] private Image mainFillImage;

    [Header("색")]
    [SerializeField] private Color damageColor = new Color(0.9f, 0.15f, 0.15f, 1f);  // 피격 잔상 빨강
    [SerializeField] private Color healColor   = new Color(0.45f, 1f, 0.5f, 1f);     // 회복 연한 초록

    [Header("피격 잔상")]
    [Tooltip("피격 후 잔상이 그대로 멈춰있는 시간 (초)")]
    [SerializeField] private float holdDuration = 0.4f;
    [Tooltip("멈춤이 끝난 뒤 잔상이 현재 HP까지 줄어드는 속도 (전체 비율 / 초)")]
    [SerializeField] private float drainSpeed = 0.7f;

    [Header("회복 채움")]
    [Tooltip("회복 시 파란 바가 차오르는 속도 (전체 비율 / 초). 낮을수록 천천히 채워짐")]
    [SerializeField] private float healFillSpeed = 0.8f;

    private enum Mode { None, Damage, Heal }
    private Mode _mode;
    private float _mainDisplay;   // 파란 바 표시값 (감소 즉시 / 증가 천천히)
    private float _ghostLevel;    // 잔상이 가리키는 값 (피격=직전HP, 회복=회복목표)
    private float _holdTimer;
    private float _fillLastFrame;

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
            ghostImage.type = Image.Type.Filled;
            ghostImage.fillMethod = Image.FillMethod.Horizontal;
            ghostImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            ghostImage.enabled = false;
        }

        _mainDisplay = _ghostLevel = _fillLastFrame = CurrentFill();
    }

    private void OnDestroy()
    {
        if (playerStat != null)
            playerStat.OnHurt -= OnHurt;
    }

    private void OnHurt()
    {
        if (ghostImage == null) return;

        // 피격: 직전 프레임 HP 에 빨간 잔상을 세우고 멈춤 시작
        _mode = Mode.Damage;
        _ghostLevel = Mathf.Max(_ghostLevel, _fillLastFrame);
        _holdTimer = holdDuration;
        ghostImage.color = damageColor;
        ghostImage.enabled = true;
    }

    private void Update()
    {
        if (playerStat == null) return;

        float cur = CurrentFill();

        // 회복 감지: HP 가 표시값보다 위로 올라감 (피격 잔상 진행 중이 아닐 때)
        if (cur > _mainDisplay + 0.0008f && _mode != Mode.Damage && ghostImage != null)
        {
            _mode = Mode.Heal;
            ghostImage.color = healColor;
            ghostImage.enabled = true;
        }

        // 파란 바 표시값: 감소는 즉시, 증가(회복)는 천천히 차오름
        if (cur <= _mainDisplay)
            _mainDisplay = cur;
        else
            _mainDisplay = Mathf.MoveTowards(_mainDisplay, cur, healFillSpeed * Time.deltaTime);

        if (_mode == Mode.Damage)
        {
            // 빨간 잔상: 멈췄다가 현재(파란 바)까지 감소
            if (_mainDisplay >= _ghostLevel) EndGhost();
            else if (_holdTimer > 0f) _holdTimer -= Time.deltaTime;
            else
            {
                _ghostLevel = Mathf.MoveTowards(_ghostLevel, _mainDisplay, drainSpeed * Time.deltaTime);
                if (_ghostLevel <= _mainDisplay + 0.0005f) EndGhost();
            }
        }
        else if (_mode == Mode.Heal)
        {
            // 초록 잔상은 회복 목표(현재 HP), 파란 바가 다 차오르면 종료
            _ghostLevel = cur;
            if (_mainDisplay >= _ghostLevel - 0.0005f) EndGhost();
        }

        if (ghostImage != null && ghostImage.enabled)
            ghostImage.fillAmount = _ghostLevel;

        _fillLastFrame = cur;
    }

    private void LateUpdate()
    {
        // 슬라이더가 Update 에서 즉시 채워둔 fillAmount 를 우리 표시값으로 덮어씀
        // (감소는 즉시라 차이 없고, 회복 때만 천천히 차오르게 됨)
        if (mainFillImage != null)
            mainFillImage.fillAmount = _mainDisplay;
    }

    private void EndGhost()
    {
        _mode = Mode.None;
        if (ghostImage != null) ghostImage.enabled = false;
    }

    private float CurrentFill()
    {
        if (playerStat == null || playerStat.MaxHp <= 0f) return 1f;
        return Mathf.Clamp01(playerStat.CurrentHp / playerStat.MaxHp);
    }
}

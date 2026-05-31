// =====================================================================
// TimeCatchUI.cs  ─  TIME CATCH 미니게임
//
// 동작 방식
//   1. StartCatch() 호출 → 팝업창 표시, 바늘 회전 시작
//   2. 바늘이 12시 방향 성공 구간을 지날 때 SPACE 누르면 성공 (+5%)
//   3. 실패(구간 밖 클릭) → 즉시 창 닫힘, 콜백 false
//   4. 한 바퀴(360°) 완료 without 입력 → 즉시 창 닫힘, 콜백 false
//   5. 성공 → 잠깐 피드백 표시 후 창 닫힘, 콜백 true
// =====================================================================

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeCatchUI : MonoBehaviour
{
    // ── 패널 ──────────────────────────────────────────────────────────
    [Header("팝업 패널 (숨김 시작)")]
    [SerializeField] private GameObject timeCatchPanel;

    // ── 시계 비주얼 ───────────────────────────────────────────────────
    [Header("시계 요소")]
    [SerializeField] private RectTransform needle;          // 회전 바늘
    [SerializeField] private Image         successZoneImage; // Radial360 성공 구간
    [SerializeField] private Image         trackRingImage;   // 외곽 트랙 링

    // ── 텍스트 ────────────────────────────────────────────────────────
    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI currentRateText;  // 현재 성공 확률
    [SerializeField] private TextMeshProUGUI bonusRateText;    // 캐치 성공 시 확률
    [SerializeField] private TextMeshProUGUI guideText;        // "SPACE" 안내
    [SerializeField] private TextMeshProUGUI resultText;       // 성공 피드백

    // ── 난이도 ────────────────────────────────────────────────────────
    [Header("난이도 설정")]
    [SerializeField] private float baseSpeed    = 110f;  // Lv.1 속도 (도/초)
    [SerializeField] private float speedInc     =  12f;  // 단계당 속도 증가
    [SerializeField] private float baseZone     =  60f;  // Lv.1 성공 구간 (도)
    [SerializeField] private float zoneDecr     =   4f;  // 단계당 구간 감소
    [SerializeField] private float minZone      =  20f;  // 최소 구간

    // ── 피드백 ────────────────────────────────────────────────────────
    [Header("피드백")]
    [SerializeField] private float successHoldTime = 0.8f; // 성공 메시지 표시 시간

    // ── 색상 ──────────────────────────────────────────────────────────
    [Header("색상")]
    [SerializeField] private Color zoneColor   = new Color(0.18f, 0.85f, 0.45f, 0.85f);
    [SerializeField] private Color successColor = new Color(0.18f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color missColor    = new Color(1f, 0.25f, 0.25f, 1f);

    // ── 내부 ──────────────────────────────────────────────────────────
    private bool   _isActive;
    private float  _angle;
    private float  _speed;
    private float  _zoneAngle;
    private Action<bool> _onResult;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        timeCatchPanel?.SetActive(false);
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    /// <summary>타임 캐치 팝업 시작</summary>
    /// <param name="coreLevel">현재 코어 레벨 (0~10) — 난이도</param>
    /// <param name="currentSuccessRate">현재 성공률 표시용 (0~1)</param>
    /// <param name="onResult">완료 콜백 (true=성공 +5%, false=실패/스킵)</param>
    public void StartCatch(int coreLevel, float currentSuccessRate, Action<bool> onResult)
    {
        _onResult = onResult;
        _angle    = 0f;
        _isActive = false;  // 아직 활성 아님 — Show 후에 켬

        int lv = Mathf.Max(1, coreLevel);
        _speed     = baseSpeed + speedInc * (lv - 1);
        _zoneAngle = Mathf.Max(minZone, baseZone - zoneDecr * (lv - 1));

        // 성공 구간 아크 설정
        if (successZoneImage != null)
        {
            successZoneImage.type       = Image.Type.Filled;
            successZoneImage.fillMethod = Image.FillMethod.Radial360;
            successZoneImage.fillOrigin = 2; // Top (12시)
            successZoneImage.fillAmount = _zoneAngle / 360f;
            successZoneImage.color      = zoneColor;
            // 구간 중앙이 12시에 오도록 오프셋 (+halfZone 반시계)
            successZoneImage.transform.localRotation =
                Quaternion.Euler(0f, 0f, _zoneAngle * 0.5f);
        }

        // 바늘 초기 위치 (12시)
        if (needle != null)
            needle.localRotation = Quaternion.identity;

        // 확률 텍스트
        int curPct   = Mathf.RoundToInt(currentSuccessRate * 100f);
        int bonusPct = Mathf.RoundToInt(Mathf.Clamp01(currentSuccessRate + 0.05f) * 100f);
        if (currentRateText != null) currentRateText.text = $"성공 확률  {curPct}%";
        if (bonusRateText   != null) bonusRateText.text   = $"캐치 성공  {bonusPct}%  (+5%)";

        // 안내 / 결과 텍스트 초기화
        if (guideText  != null) { guideText.gameObject.SetActive(true);  guideText.text  = "SPACE"; }
        if (resultText != null) { resultText.gameObject.SetActive(false); }

        timeCatchPanel?.SetActive(true);
        _isActive = true;
    }

    // ── 매 프레임 ─────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isActive) return;

        // 바늘 회전 (시계 방향)
        _angle += _speed * Time.deltaTime;
        if (needle != null)
            needle.localRotation = Quaternion.Euler(0f, 0f, -_angle);

        // ── SPACE 입력 ──────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Catch();
            return;
        }

        // ── 한 바퀴 완료 → 자동 실패 ───────────────────────
        if (_angle >= 360f)
        {
            _isActive = false;
            timeCatchPanel?.SetActive(false);
            _onResult?.Invoke(false);
        }
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    private void Catch()
    {
        _isActive = false;

        // 성공 판정: 12시 ±halfZone 안에 있는지
        float half = _zoneAngle * 0.5f;
        float norm = _angle % 360f;
        bool  hit  = norm <= half || norm >= (360f - half);

        if (hit)
            StartCoroutine(SuccessRoutine());
        else
            FailImmediate();
    }

    private void FailImmediate()
    {
        // 실패: 창 즉시 닫힘
        timeCatchPanel?.SetActive(false);
        _onResult?.Invoke(false);
    }

    private IEnumerator SuccessRoutine()
    {
        if (guideText  != null) guideText.gameObject.SetActive(false);
        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text  = "TIME CATCH!  +5%";
            resultText.color = successColor;
        }

        yield return new WaitForSeconds(successHoldTime);

        timeCatchPanel?.SetActive(false);
        _onResult?.Invoke(true);
    }
}

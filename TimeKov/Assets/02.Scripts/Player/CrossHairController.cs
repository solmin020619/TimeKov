using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform root;      // CrosshairRoot
    public RectTransform top;
    public RectTransform bottom;
    public RectTransform left;
    public RectTransform right;
    public Image hitMarker;         // X 이미지 (기본 비활성 추천)

    [Header("Cursor Mode")]
    public bool hideSystemCursor = true;
    public bool followMouse = true;

    [Header("Spread Tuning")]
    public float baseSpread = 10f;          // 기본 벌어짐
    public float maxSpread = 60f;           // 최대 벌어짐
    public float recoverSpeed = 18f;        // 회복 속도(클수록 빨리 좁아짐)

    [Header("Spread Kicks")]
    public float fireKick = 8f;             // 발사 시 퍼짐 증가
    public float runAdd = 14f;              // 달리기 중 추가 퍼짐
    public float hurtKick = 18f;            // 피격 시 퍼짐 증가
    public float hitShrink = 10f;           // 명중 시 순간(퍼짐 감소)

    [Header("Hit Marker")]
    public float hitMarkerTime = 0.06f;

    private float currentSpread;
    private float targetSpread;
    private float hitMarkerTimer;

    private bool isEnabled;
    private bool isRunning;

    void Awake()
    {
        currentSpread = baseSpread;
        targetSpread = baseSpread;

        if (hitMarker != null)
            hitMarker.enabled = false;
    }

    void OnEnable()
    {
        ApplyCursorState(true);
    }

    void OnDisable()
    {
        ApplyCursorState(false);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (!isEnabled) return;

        // 마우스 따라다니는 타입
        if (followMouse && root != null)
        {
            root.position = Input.mousePosition;
        }

        // 달리기 상태에 따른 목표 퍼짐
        float runSpread = isRunning ? runAdd : 0f;
        targetSpread = baseSpread + runSpread;

        // 현재 퍼짐이 목표로 회복
        currentSpread = Mathf.MoveTowards(currentSpread, targetSpread, recoverSpeed * Time.deltaTime);
        currentSpread = Mathf.Clamp(currentSpread, baseSpread, maxSpread);

        ApplySpread(currentSpread);

        // 히트마커 타이머
        if (hitMarkerTimer > 0f)
        {
            hitMarkerTimer -= Time.deltaTime;
            if (hitMarkerTimer <= 0f && hitMarker != null)
                hitMarker.enabled = false;
        }
    }

    void ApplySpread(float s)
    {
        if (top != null) top.anchoredPosition = new Vector2(0f, s);
        if (bottom != null) bottom.anchoredPosition = new Vector2(0f, -s);
        if (left != null) left.anchoredPosition = new Vector2(-s, 0f);
        if (right != null) right.anchoredPosition = new Vector2(s, 0f);
    }

    void ApplyCursorState(bool active)
    {
        if (!hideSystemCursor) return;
        Cursor.visible = !active;
        // Cursor.lockState는 쿼터뷰/마우스 조준이면 굳이 Lock 안 하는게 보통 안정적
    }

    // 외부에서 호출할 API

    // 무기 들었을 때/내렸을 때
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;

        if (root != null)
            root.gameObject.SetActive(enabled);

        ApplyCursorState(enabled);

        // 끌 때 히트마커도 정리
        if (!enabled && hitMarker != null)
            hitMarker.enabled = false;
    }

    // 달리기 상태 전달
    public void SetRunning(bool running)
    {
        isRunning = running;
    }

    // 발사 성공 시 호출
    public void OnFire()
    {
        currentSpread = Mathf.Min(maxSpread, currentSpread + fireKick);
    }

    // 플레이어 피격 시 호출
    public void OnHurt()
    {
        currentSpread = Mathf.Min(maxSpread, currentSpread + hurtKick);
    }

    // 명중 확인 시 호출 (히트마커 + 순간)
    public void OnHitConfirm()
    {
        // 순간縮(퍼짐 감소)
        currentSpread = Mathf.Max(baseSpread, currentSpread - hitShrink);

        // 히트마커
        if (hitMarker != null)
        {
            hitMarker.enabled = true;
            hitMarkerTimer = hitMarkerTime;
        }
    }
}

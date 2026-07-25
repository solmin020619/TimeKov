using TMPro;
using UnityEngine;

// SetActive(false) 대신 CanvasGroup.alpha=0으로 숨김 — GameObject 자체는 항상 활성 유지
// 안 그러면 markerRoot==TutorialMarker GO인 경우 Hide() 후 LateUpdate가 영원히 멈춤

/// <summary>
/// 심플 위치 마커.
/// 활성 ReachTriggerObjective의 targetTriggerId에 매칭되는 QuestTrigger를 찾아
/// 그 위치에 화면 좌표로 마커(Circle 스프라이트) + 거리(m) 표시.
/// 카메라 시야 밖이면 화면 가장자리에 클램프. 스프라이트/색은 인스펙터에서 직접 지정.
/// </summary>
public class TutorialMarker : MonoBehaviour
{
    [Header("UI 참조 (Builder가 자동 박음)")]
    [SerializeField] private RectTransform markerRoot;       // Image + Text 부모
    [SerializeField] private TMP_Text distanceText;

    [Header("동작")]
    [Tooltip("화면 가장자리에서 떨어진 픽셀 (시야 밖 클램프용)")]
    [SerializeField] private float screenEdgePadding = 80f;
    [Tooltip("이 거리 미만이면 거리 텍스트 숨김 (이미 도착)")]
    [SerializeField] private float hideTextDistance = 1.5f;
    [Tooltip("마커 위치 보간 속도. 높을수록 즉각, 낮을수록 부드러움. 흔들림(튐) 억제용.")]
    [SerializeField] private float positionSmooth = 14f;

    [Header("연출 (잔잔한 둥둥)")]
    [SerializeField] private float bobAmplitude = 4f;
    [SerializeField] private float bobSpeed = 2f;

    private Transform _player;
    private Camera _cam;
    private CanvasGroup _markerCg;
    private bool _wasVisible;
    private RectTransform _circle;
    private Vector3 _circleHome;

    void Awake()
    {
        // markerRoot에 CanvasGroup 부착 (alpha 토글용)
        if (markerRoot != null)
        {
            _markerCg = markerRoot.GetComponent<CanvasGroup>();
            if (_markerCg == null) _markerCg = markerRoot.gameObject.AddComponent<CanvasGroup>();
            _markerCg.alpha = 0f;

            // 메인 마커(Circle) 캐시 (둥둥 연출 기준 위치). 스프라이트/색은 인스펙터에서 직접 지정.
            _circle = markerRoot.Find("Circle") as RectTransform;
            if (_circle != null) _circleHome = _circle.localPosition;
        }
    }


    void LateUpdate()
    {
        if (markerRoot == null) return;

        var cam = GetCamera();
        var player = GetPlayer();
        if (cam == null || player == null) { Hide(); return; }
        if (QuestManager.Instance == null) { Hide(); return; }

        // 전체화면 차단 UI(설정/인벤/도감/설비/코어/전송/상자/건축/사망/튜토영상)가 떠 있으면 마커 숨김.
        // 마커 캔버스가 이 패널들보다 위로 정렬돼서, 안 숨기면 패널 위에 마커가 비쳐 보인다.
        if (IsBlockingUIOpen()) { Hide(); return; }

        // 활성 ReachTriggerObjective 첫 번째 찾기 (튜토리얼은 한 번에 1개)
        // IsCompleted 체크 — 완료된 obj는 무시
        Vector3 targetWorld = Vector3.zero;
        bool found = false;
        string lastTriggerId = null;
        foreach (var rt in QuestManager.Instance.Runtimes)
        {
            if (rt == null || rt.activeObjectives == null) continue;
            foreach (var obj in rt.activeObjectives)
            {
                if (obj is ReachTriggerObjective reach && !reach.IsCompleted)
                {
                    lastTriggerId = reach.targetTriggerId;
                    var trig = QuestTrigger.Get(reach.targetTriggerId);
                    if (trig != null)
                    {
                        targetWorld = trig.transform.position;
                        found = true;
                        break;
                    }
                }
            }
            if (found) break;
        }

        if (!found)
        {
            // 3초에 한 번 진단 로그 (활성 obj 종류 정리)
            if (Time.frameCount % 180 == 0)
            {
                int total = 0, reachCount = 0;
                string types = "";
                foreach (var rt in QuestManager.Instance.Runtimes)
                {
                    if (rt?.activeObjectives == null) continue;
                    foreach (var obj in rt.activeObjectives)
                    {
                        if (obj == null) continue;
                        total++;
                        types += obj.GetType().Name + ",";
                        if (obj is ReachTriggerObjective) reachCount++;
                    }
                }
                if (lastTriggerId != null)
                    Debug.LogWarning($"[TutorialMarker] ReachTrigger='{lastTriggerId}' 활성인데 QuestTrigger GameObject 못 찾음. 활성 obj {total}개=[{types}]");
            }
            Hide();
            return;
        }

        // ── 화면 좌표 계산 (카메라 뒤/특이점에서도 안정적으로) ─────────────
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector3 sp = cam.WorldToScreenPoint(targetWorld);
        bool behind = sp.z < 0f;

        bool onscreen = !behind
                      && sp.x >= screenEdgePadding && sp.x <= Screen.width - screenEdgePadding
                      && sp.y >= screenEdgePadding && sp.y <= Screen.height - screenEdgePadding;

        Vector2 targetPos;
        if (onscreen)
        {
            targetPos = new Vector2(sp.x, sp.y);
        }
        else
        {
            // 방향 결정: 카메라 뒤면 WorldToScreenPoint 가 발산(=튐 원인)하므로 쓰지 않고
            // 카메라 축(right/up) 투영으로 방향을 구한다. 특이점 jitter 가 사라진다.
            Vector2 dir;
            if (behind)
            {
                Vector3 to = targetWorld - cam.transform.position;
                dir = new Vector2(Vector3.Dot(to, cam.transform.right),
                                  Vector3.Dot(to, cam.transform.up));
            }
            else
            {
                dir = new Vector2(sp.x, sp.y) - center;
            }
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
            dir.Normalize();

            // 중심에서 dir 방향으로 패딩된 화면 사각형 가장자리와의 교점
            float halfW = center.x - screenEdgePadding;
            float halfH = center.y - screenEdgePadding;
            float scale = Mathf.Min(halfW / Mathf.Max(Mathf.Abs(dir.x), 1e-4f),
                                    halfH / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));
            targetPos = center + dir * scale;
        }

        // 부드럽게 이동(튐 방지). 막 다시 보이기 시작한 프레임은 스냅(슬라이딩 잔상 방지).
        Vector3 target3 = new Vector3(targetPos.x, targetPos.y, 0f);
        if (!_wasVisible || positionSmooth <= 0f)
            markerRoot.position = target3;
        else
            markerRoot.position = Vector3.Lerp(markerRoot.position, target3,
                                               1f - Mathf.Exp(-positionSmooth * Time.deltaTime));
        if (_markerCg != null) _markerCg.alpha = 1f;
        _wasVisible = true;

        // 잔잔한 둥둥 (살아있는 느낌만, 과한 연출 없음)
        if (_circle != null)
        {
            float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude;
            _circle.localPosition = _circleHome + new Vector3(0f, bob, 0f);
        }

        // 거리
        float dist = Vector3.Distance(player.position, targetWorld);
        if (distanceText != null)
        {
            if (dist < hideTextDistance) distanceText.text = "";
            else distanceText.text = $"{dist:F0} m";
        }

    }

    void Hide()
    {
        // SetActive 안 씀! markerRoot가 TutorialMarker 자체 GameObject면 LateUpdate 영원히 멈춤
        if (_markerCg != null) _markerCg.alpha = 0f;
        _wasVisible = false; // 다시 보일 때 보간 없이 스냅 (옛 위치에서 미끄러져 오는 것 방지)
        if (_circle != null) { _circle.localPosition = _circleHome; _circle.localScale = Vector3.one; }
    }

    // 전체화면 차단 UI가 하나라도 열려 있으면 true. 그동안 마커를 숨겨 패널 위 비침을 막는다.
    // IsUIBlocking()은 설정/인벤/도감/코어/전송/건축/상자/퀘스트 등 currentState 기반 UI를 전부 커버.
    // 설비/사망/튜토영상은 currentState와 별개 채널이라 따로 검사.
    static bool IsBlockingUIOpen()
    {
        if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking()) return true;
        if (MachineUI.IsAnyOpen) return true;
        if (DeathOverlayUI.IsOpen) return true;
        if (TutorialVideoUI.IsShowing) return true;
        return false;
    }

    Transform GetPlayer()
    {
        if (_player != null) return _player;
        var p = FindAnyObjectByType<Player>();
        if (p != null) _player = p.transform;
        return _player;
    }

    Camera GetCamera()
    {
        if (_cam != null) return _cam;
        _cam = Camera.main;
        return _cam;
    }
}

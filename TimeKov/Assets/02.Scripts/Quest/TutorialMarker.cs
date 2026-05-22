using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SetActive(false) 대신 CanvasGroup.alpha=0으로 숨김 — GameObject 자체는 항상 활성 유지
// 안 그러면 markerRoot==TutorialMarker GO인 경우 Hide() 후 LateUpdate가 영원히 멈춤

/// <summary>
/// Endfield 스타일 위치 마커.
/// 활성 ReachTriggerObjective의 targetTriggerId에 매칭되는 QuestTrigger를 찾아
/// 그 위치에 화면 좌표로 노란 마커 + 거리(m) 표시.
/// 카메라 시야 밖이면 화면 가장자리에 클램프.
/// </summary>
public class TutorialMarker : MonoBehaviour
{
    [Header("UI 참조 (Builder가 자동 박음)")]
    [SerializeField] private RectTransform markerRoot;       // Image + Text 부모
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private RectTransform offscreenArrow;   // 시야 밖일 때 회전하는 화살표 (선택)

    [Header("동작")]
    [Tooltip("화면 가장자리에서 떨어진 픽셀 (시야 밖 클램프용)")]
    [SerializeField] private float screenEdgePadding = 80f;
    [Tooltip("이 거리 미만이면 거리 텍스트 숨김 (이미 도착)")]
    [SerializeField] private float hideTextDistance = 1.5f;

    private Transform _player;
    private Camera _cam;
    private CanvasGroup _markerCg;

    void Awake()
    {
        // markerRoot에 CanvasGroup 부착 (alpha 토글용)
        if (markerRoot != null)
        {
            _markerCg = markerRoot.GetComponent<CanvasGroup>();
            if (_markerCg == null) _markerCg = markerRoot.gameObject.AddComponent<CanvasGroup>();
            _markerCg.alpha = 0f;
        }
    }


    void LateUpdate()
    {
        if (markerRoot == null) return;

        var cam = GetCamera();
        var player = GetPlayer();
        if (cam == null || player == null) { Hide(); return; }
        if (QuestManager.Instance == null) { Hide(); return; }

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
                else
                    Debug.Log($"[TutorialMarker] 활성 ReachTrigger 없음 (활성 obj {total}개, ReachTrigger {reachCount}개, 타입=[{types}])");
            }
            Hide();
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(targetWorld);
        bool behind = screen.z < 0f;

        // 카메라 뒤면 좌표 반전 (가장자리 방향 표시)
        if (behind)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        bool offscreen = behind
                       || screen.x < screenEdgePadding
                       || screen.x > Screen.width - screenEdgePadding
                       || screen.y < screenEdgePadding
                       || screen.y > Screen.height - screenEdgePadding;

        // 화면 가장자리에 클램프
        screen.x = Mathf.Clamp(screen.x, screenEdgePadding, Screen.width - screenEdgePadding);
        screen.y = Mathf.Clamp(screen.y, screenEdgePadding, Screen.height - screenEdgePadding);

        markerRoot.position = new Vector3(screen.x, screen.y, 0f);
        if (_markerCg != null) _markerCg.alpha = 1f;

        // 거리
        float dist = Vector3.Distance(player.position, targetWorld);
        if (distanceText != null)
        {
            if (dist < hideTextDistance) distanceText.text = "";
            else distanceText.text = $"{dist:F0} m";
        }

        // 시야 밖이면 화살표 회전 (마커 root → 목표 방향)
        if (offscreenArrow != null)
        {
            offscreenArrow.gameObject.SetActive(offscreen);
            if (offscreen)
            {
                Vector2 centerToMarker = new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
                float angle = Mathf.Atan2(centerToMarker.y, centerToMarker.x) * Mathf.Rad2Deg - 90f;
                offscreenArrow.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    void Hide()
    {
        // SetActive 안 씀! markerRoot가 TutorialMarker 자체 GameObject면 LateUpdate 영원히 멈춤
        if (_markerCg != null) _markerCg.alpha = 0f;
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

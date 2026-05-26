// =====================================================================
// HintArrowManager.cs
// "여기에 주목!" 화살표를 World/UI 타깃 위에 표시하는 단일 매니저.
//
// 사용 예:
//   // World 게임오브젝트 위에 5초간 표시
//   HintArrowManager.I.Show("dropslot_hint", recipeDropSlot.transform, 5f);
//
//   // 무한 표시 → Hide로 명시적 종료
//   HintArrowManager.I.Show("first_machine", machineRoot, 0f);
//   ...
//   HintArrowManager.I.Hide("first_machine");
//
//   // UI 요소(RectTransform) 위에 (Screen Space Overlay/Camera 모두 지원)
//   HintArrowManager.I.ShowOnUI("inv_slot", slotRect, canvas, 3f);
//
// 부착: 씬에 빈 GO 만들어 이 컴포넌트 부착 + arrowPrefab 슬롯에 화살표 프리팹 드래그.
// 같은 ID로 Show 재호출 시 기존 인스턴스 갱신(타깃·시간 업데이트).
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TimeKov.UI
{
    public class HintArrowManager : MonoBehaviour
    {
        public static HintArrowManager I { get; private set; }

        [Header("Prefab")]
        [Tooltip("표시할 화살표 프리팹 (FX_UI_GuidedArrow 등). ParticleSystem/UI Image 모두 가능.")]
        [SerializeField] GameObject arrowPrefab;

        [Header("World 모드")]
        [Tooltip("World 타깃 위 어디에 표시할지 오프셋 (y=2면 머리 위)")]
        [SerializeField] Vector3 worldOffset = new Vector3(0, 2f, 0);

        [Tooltip("World 모드 인스턴스 부모 (비우면 매니저 자식)")]
        [SerializeField] Transform worldParent;

        [Header("UI 모드")]
        [Tooltip("UI 화살표 인스턴스가 들어갈 Canvas (RectTransform). 비우면 ShowOnUI 호출 시 자동 탐색.")]
        [SerializeField] RectTransform uiParent;

        [Tooltip("UI 타깃 위 어디에 표시할지 오프셋 (픽셀, y=80이면 위쪽)")]
        [SerializeField] Vector2 uiOffset = new Vector2(0, 80f);

        readonly Dictionary<string, HintArrowInstance> _active = new();

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ── World 모드 ────────────────────────────────────────────────

        /// <summary>World 게임오브젝트 위에 화살표 표시. duration=0이면 무한.</summary>
        public void Show(string id, Transform worldTarget, float duration = 0f)
        {
            if (string.IsNullOrEmpty(id) || worldTarget == null) return;
            if (arrowPrefab == null)
            {
                Debug.LogWarning("[HintArrowManager] arrowPrefab 미설정");
                return;
            }

            if (_active.TryGetValue(id, out var existing))
            {
                existing.SetWorldTarget(worldTarget, worldOffset);
                existing.SetDuration(duration);
                return;
            }

            var parent = worldParent != null ? worldParent : transform;
            var go = Instantiate(arrowPrefab, parent);
            var inst = go.AddComponent<HintArrowInstance>();
            inst.SetWorldTarget(worldTarget, worldOffset);
            inst.SetDuration(duration);
            inst.OnExpired += () => _active.Remove(id);
            _active[id] = inst;
        }

        /// <summary>World 위치(Vector3) 고정 표시.</summary>
        public void Show(string id, Vector3 worldPosition, float duration = 0f)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (arrowPrefab == null) return;

            if (_active.TryGetValue(id, out var existing))
            {
                existing.SetWorldPosition(worldPosition + worldOffset);
                existing.SetDuration(duration);
                return;
            }

            var parent = worldParent != null ? worldParent : transform;
            var go = Instantiate(arrowPrefab, parent);
            var inst = go.AddComponent<HintArrowInstance>();
            inst.SetWorldPosition(worldPosition + worldOffset);
            inst.SetDuration(duration);
            inst.OnExpired += () => _active.Remove(id);
            _active[id] = inst;
        }

        // ── UI 모드 ───────────────────────────────────────────────────

        /// <summary>UI RectTransform 위에 화살표 표시. Canvas 자식으로 인스턴스화.</summary>
        public void ShowOnUI(string id, RectTransform uiTarget, Canvas canvas, float duration = 0f)
        {
            if (string.IsNullOrEmpty(id) || uiTarget == null) return;
            if (arrowPrefab == null) return;

            RectTransform parent = uiParent;
            if (parent == null && canvas != null)
                parent = canvas.GetComponent<RectTransform>();
            if (parent == null)
            {
                Debug.LogWarning("[HintArrowManager] ShowOnUI 실패 — uiParent 또는 canvas 필요");
                return;
            }

            if (_active.TryGetValue(id, out var existing))
            {
                existing.SetUITarget(uiTarget, uiOffset, canvas);
                existing.SetDuration(duration);
                return;
            }

            var go = Instantiate(arrowPrefab, parent);
            var inst = go.AddComponent<HintArrowInstance>();
            inst.SetUITarget(uiTarget, uiOffset, canvas);
            inst.SetDuration(duration);
            inst.OnExpired += () => _active.Remove(id);
            _active[id] = inst;
        }

        // ── 종료 ──────────────────────────────────────────────────────

        public void Hide(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_active.TryGetValue(id, out var inst))
            {
                inst.ForceHide();
                _active.Remove(id);
            }
        }

        public void HideAll()
        {
            foreach (var kv in _active) kv.Value.ForceHide();
            _active.Clear();
        }

        public bool IsActive(string id) => !string.IsNullOrEmpty(id) && _active.ContainsKey(id);
    }
}

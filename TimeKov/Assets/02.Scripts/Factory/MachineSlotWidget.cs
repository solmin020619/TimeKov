// =====================================================================
// MachineSlotWidget.cs
// 설비 출력 슬롯 위젯
// =====================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineSlotWidget : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityBorder;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;

        // InventorySlotUI 와 동일한 등급 색상 배열
        private static readonly Color[] GradeColors = new Color[]
        {
            new Color(0.60f, 0.60f, 0.60f, 1f),  // Common   - 회색
            new Color(0.30f, 0.55f, 0.90f, 1f),  // Advanced - 파랑
            new Color(0.20f, 0.75f, 0.40f, 1f),  // Rare     - 초록
            new Color(0.65f, 0.30f, 0.90f, 1f),  // Hero     - 보라
            new Color(0.95f, 0.55f, 0.10f, 1f),  // Legend   - 황금
        };

        private Action _onClick;
        private Action _onDoubleClick;
        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        // 드래그용 현재 아이템 정보
        private int _currentItemId;
        private int _currentAmount;
        private bool _hasItem;

        // 드래그 비주얼
        private static GameObject _dragVisual;
        private Canvas _canvas;

        // 외부에서 드래그 중인지 확인할 수 있도록 공개
        public static bool IsOutputDragging { get; private set; }
        public static int DragItemId { get; private set; }
        public static int DragAmount { get; private set; }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        // 오브젝트가 비활성화될 때 드래그 비주얼 강제 정리
        // (TakeOutput 후 SetActive(false) 되면 OnEndDrag가 호출 안 되므로 여기서 처리)
        private void OnDisable()
        {
            IsOutputDragging = false;
            if (_dragVisual != null)
            {
                Destroy(_dragVisual);
                _dragVisual = null;
            }
        }

        public void Setup(int itemId, int amount)
        {
            _currentItemId = itemId;
            _currentAmount = amount;
            _hasItem = itemId > 0 && amount > 0;

            var itemData = GameDataUtility.GetItem(itemId);
            string name = itemData != null ? itemData.itemName : itemId.ToString();

            // 아이콘 로드
            Sprite sprite = null;
            if (itemData != null && !string.IsNullOrEmpty(itemData.iconKey))
                sprite = ItemDatabase.GetIcon(itemData.iconKey);

            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                iconImage.enabled = true;
            }

            // 등급 테두리 색상
            if (rarityBorder != null)
            {
                int gradeIndex = itemData != null ? (int)itemData.itemGrade : 0;
                gradeIndex = Mathf.Clamp(gradeIndex, 0, GradeColors.Length - 1);
                rarityBorder.color = GradeColors[gradeIndex];
            }

            if (itemNameText != null)
                itemNameText.text = name;

            if (amountText != null)
                amountText.text = amount > 1 ? $"x{amount}" : "";
        }

        public void SetClickAction(Action a) => _onClick = a;
        public void SetDoubleClickAction(Action a) => _onDoubleClick = a;

        // ── 클릭 ────────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime < DoubleClickThreshold)
            {
                _onDoubleClick?.Invoke();
                _lastClickTime = 0f;
            }
            else
            {
                _onClick?.Invoke();
                _lastClickTime = now;
            }
        }

        // ── 드래그 ──────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_hasItem)
            {
                eventData.pointerDrag = null;
                return;
            }

            // Canvas 캐시 (Awake 이후 부모가 바뀌었을 경우 대비)
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();

            IsOutputDragging = true;
            DragItemId = _currentItemId;
            DragAmount = _currentAmount;

            // 드래그 고스트 이미지 생성
            _dragVisual = new GameObject("OutputDragVisual");
            _dragVisual.transform.SetParent(_canvas.transform, false);
            _dragVisual.transform.SetAsLastSibling();

            var rt = _dragVisual.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(64f, 64f);

            var img = _dragVisual.AddComponent<Image>();
            img.sprite = iconImage != null ? iconImage.sprite : null;
            img.color = Color.white;
            img.raycastTarget = false;

            var cg = _dragVisual.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 0.85f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 우클릭으로 드래그 취소 시 고스트 강제 정리
            if (Input.GetMouseButton(1)) { OnEndDrag(eventData); return; }

            if (_dragVisual == null || _canvas == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                eventData.position,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out Vector2 localPos))
            {
                _dragVisual.transform.localPosition = localPos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            IsOutputDragging = false;
            if (_dragVisual != null)
            {
                Destroy(_dragVisual);
                _dragVisual = null;
            }
        }
    }
}
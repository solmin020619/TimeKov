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
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityBorder;
        [SerializeField] private Image gradeAurora;   // 등급 오로라(하단 그라데이션, 인벤 슬롯과 동일)
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;

        // 등급 색상은 공용 GradeVisual 로 이동(중앙화).

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
                rarityBorder.color = GradeVisual.GetColor(gradeIndex);
            }
            // 등급 오로라(하단 글로우) - 인벤 슬롯과 동일.
            if (gradeAurora != null)
            {
                int gi = itemData != null ? (int)itemData.itemGrade : 0;
                Color gc = GradeVisual.GetColor(gi);
                gradeAurora.color = new Color(gc.r, gc.g, gc.b, gc.a * 0.6f);   // 바닥 선명 + 오로라 은은
            }

            if (itemNameText != null)
                itemNameText.text = name;

            if (amountText != null)
                amountText.text = amount > 1 ? $"x{amount}" : "";
        }

        /// <summary>
        /// 아직 생산되지 않은 결과물 아이콘을 흐리게(낮은 투명도) 미리 보여준다.
        /// 드래그/클릭/툴팁이 비활성인 미리보기 상태 (_hasItem = false).
        /// </summary>
        public void SetupPreview(int itemId)
        {
            _currentItemId = itemId;
            _currentAmount = 0;
            _hasItem = false; // 미리보기 — 가져가기/드래그/툴팁 모두 비활성

            var itemData = GameDataUtility.GetItem(itemId);

            Sprite sprite = null;
            if (itemData != null && !string.IsNullOrEmpty(itemData.iconKey))
                sprite = ItemDatabase.GetIcon(itemData.iconKey);

            // 미생산(빈 칸) = 유령 아이콘만, 등급 테두리/오로라는 끔 -> 완성(선명+발광)과 확실히 구분.
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.color  = sprite != null
                    ? new Color(1f, 1f, 1f, 0.22f)
                    : new Color(1f, 1f, 1f, 0.1f);
                iconImage.enabled = true;
            }

            if (rarityBorder != null)
                rarityBorder.color = new Color(0f, 0f, 0f, 0f);   // 빈 칸 = 테두리 발광 없음
            if (gradeAurora != null)
                gradeAurora.color = new Color(0f, 0f, 0f, 0f);    // 빈 칸 = 오로라 없음

            // 미리보기에는 이름/수량 표시 안 함
            if (itemNameText != null) itemNameText.text = "";
            if (amountText != null)   amountText.text   = "";
        }

        public void SetClickAction(Action a) => _onClick = a;
        public void SetDoubleClickAction(Action a) => _onDoubleClick = a;

        // ── 호버 툴팁 (인벤토리/창고와 동일) ──────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_hasItem || _currentItemId <= 0) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            ItemTooltipUI.Instance?.Show(_currentItemId, _canvas);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ItemTooltipUI.Instance?.Hide();
        }

        // ── 클릭 ────────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime < DoubleClickThreshold)
            {
                // 더블클릭으로 출력물을 가져가면 슬롯이 비므로 호버 툴팁 즉시 숨김
                // (마우스가 그대로면 Exit가 안 떠서 툴팁이 남는 문제)
                ItemTooltipUI.Instance?.Hide();
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
// =====================================================================
// ItemIconVisual.cs
// 컨베이어 벨트 위를 이동하는 아이템 오브젝트의 시각 표현.
//
// Setup(itemId, amount) 호출 시:
//   - 다른 팀원이 만든 ItemDatabase.Get(itemId)로 아이콘/이름을 가져온다.
//   - 해당 코드가 아직 없으면 itemId 문자열을 텍스트로 임시 표시한다.
//
// 아이콘/폰트 변경은 이 파일의 Apply() 만 수정하면 된다.
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class ItemIconVisual : MonoBehaviour
    {
        [SerializeField] private Image        iconImage;
        [SerializeField] private TextMeshProUGUI countText;

        public void Setup(string itemId, int amount)
        {
            // ── 다른 팀원 ItemDatabase 연동 시 아래 주석 해제 ─────────────
            // var data = ItemDatabase.Instance.Get(itemId);
            // if (data != null) Apply(data.icon, data.displayName, amount);
            // else              ApplyFallback(itemId, amount);
            // ────────────────────────────────────────────────────────────

            // 연동 전 임시: itemId 텍스트로 표시
            ApplyFallback(itemId, amount);
        }

        private void Apply(Sprite icon, string displayName, int amount)
        {
            if (iconImage  != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
            if (countText  != null) countText.text = amount > 1 ? $"x{amount}" : string.Empty;
            gameObject.name = $"[Item] {displayName} x{amount}";
        }

        private void ApplyFallback(string itemId, int amount)
        {
            if (iconImage  != null) iconImage.enabled = false;
            if (countText  != null) countText.text = $"{itemId}\nx{amount}";
            gameObject.name = $"[Item] {itemId} x{amount}";
        }
    }
}

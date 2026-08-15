using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropPickupRow : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image tierBar;
    [SerializeField] private TMP_Text nameText;

    // 수량 합산 때 숫자만 한 번 튀게 한다. 줄이 안 늘고 숫자만 조용히 바뀌면
    // 방금 뭘 더 주웠는지 눈에 안 들어온다.
    private const float CountPunchPeak = 1.3f;
    private const float CountPunchDuration = 0.2f;
    private Coroutine _countPunchCo;

    public void Set(int itemId, int count, Color tierColor)
    {
        ItemDataSheetData item = GameDataUtility.GetItem(itemId);

        nameText.text = item != null ? item.GetLocalizedName() : itemId.ToString();
        countText.text = count.ToString();
        tierBar.color = tierColor;

        // 아이콘 — 인벤토리와 동일한 ItemDatabase.GetIcon 사용 (Resources/Items/ + 캐시)
        Sprite icon = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    /// <summary>수량이 합산됐을 때 숫자를 한 번 튕긴다.</summary>
    public void PunchCount()
    {
        if (countText == null || !isActiveAndEnabled) return;
        if (_countPunchCo != null) StopCoroutine(_countPunchCo);
        _countPunchCo = StartCoroutine(PunchCountRoutine());
    }

    private IEnumerator PunchCountRoutine()
    {
        var t = countText.transform;
        float up = CountPunchDuration * 0.35f;
        float down = CountPunchDuration - up;

        float e = 0f;
        while (e < up)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / up);
            t.localScale = Vector3.one * Mathf.Lerp(1f, CountPunchPeak, k);
            yield return null;
        }
        e = 0f;
        while (e < down)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / down);
            k = 1f - (1f - k) * (1f - k);
            t.localScale = Vector3.one * Mathf.Lerp(CountPunchPeak, 1f, k);
            yield return null;
        }
        t.localScale = Vector3.one;
        _countPunchCo = null;
    }

    private void OnDisable()
    {
        // 트윈 중에 꺼지면 커진 채로 굳는다.
        if (_countPunchCo != null) { StopCoroutine(_countPunchCo); _countPunchCo = null; }
        if (countText != null) countText.transform.localScale = Vector3.one;
    }
}

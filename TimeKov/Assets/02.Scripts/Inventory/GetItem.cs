using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GetItem : MonoBehaviour
{
    public GameObject invenMana;
    public TextMeshProUGUI getItemText;
    public Image ItemIcon;

    // ✅ 추가: 드랍 슬롯 수량 표시 텍스트 (Hierarchy의 Amount TMP를 여기에 연결)
    public TextMeshProUGUI amountText;

    public int insertID;
    public int insertItemCount;

    // ✅ 슬롯 기본 배경(root) 저장
    private Sprite defaultSlotSprite;

    void Awake()
    {
        // 시작 전 프리팹/씬에서 세팅된 슬롯 배경을 저장해둠
        if (ItemIcon != null)
            defaultSlotSprite = ItemIcon.sprite;
    }

    void Start()
    {
        RefreshUI();
    }

    public void SetData(GameObject inventoryManagerGO, int id, int count)
    {
        invenMana = inventoryManagerGO;
        insertID = id;
        insertItemCount = count;
        RefreshUI();
    }

    // ✅ 추가: 수량 텍스트 갱신(표시/숨김)
    private void RefreshAmountUI()
    {
        if (amountText == null) return;

        if (insertID != 0 && insertItemCount > 1)
        {
            amountText.gameObject.SetActive(true);
            amountText.text = insertItemCount.ToString();
        }
        else
        {
            amountText.text = "";
            amountText.gameObject.SetActive(false);
        }
    }

    public void RefreshUI()
    {
        if (getItemText == null || ItemIcon == null)
        {
            Debug.LogWarning($"[GetItem] UI ref missing on {gameObject.name}");
            return;
        }

        if (insertID == 0)
        {
            // ✅ 빈칸에 글씨 뜨면 안 됨
            getItemText.text = "";

            // ✅ 빈칸이면 아이콘/이미지는 반드시 슬롯 배경으로 복구
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);

            // ✅ 추가: 빈칸이면 수량 숨김
            RefreshAmountUI();
            return;
        }

        // 아이템 있는 상태
        if (DataManager.Instance == null)
        {
            Debug.LogWarning($"[GetItem] DataManager.Instance is null (insertID={insertID})");
            getItemText.text = insertID.ToString();

            // 그래도 아이콘 못 찾으면 슬롯배경으로 fallback
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);

            // ✅ 추가: 데이터매니저가 없어도 수량은 표시 가능
            RefreshAmountUI();
            return;
        }

        var item = DataManager.Instance.GetItem(insertID);
        if (item == null)
        {
            Debug.LogWarning($"[GetItem] Item not found (id={insertID})");
            getItemText.text = insertID.ToString();

            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);

            // ✅ 추가
            RefreshAmountUI();
            return;
        }

        getItemText.text = item.itemName;

        Sprite spr = Resources.Load<Sprite>("Icon/" + insertID);
        if (spr != null)
        {
            ItemIcon.sprite = spr;
            ItemIcon.enabled = true;
        }
        else
        {
            // 아이콘 파일 없으면 슬롯배경으로
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);
            Debug.LogWarning($"[GetItem] Missing icon sprite: Resources/Icon/{insertID}");
        }

        // ✅ 추가: 정상 표시 케이스에서도 수량 갱신
        RefreshAmountUI();
    }

    public void ItemClick()
    {
        if (insertID == 0) return;

        if (invenMana == null)
        {
            Debug.LogWarning($"[GetItem] invenMana is null on {gameObject.name}");
            return;
        }

        var inv = invenMana.GetComponent<InventoryManager>();
        if (inv == null)
        {
            Debug.LogWarning($"[GetItem] InventoryManager not found on {invenMana.name}");
            return;
        }

        // ✅ 핵심: 인벤에 "넣은 만큼만" 드랍에서 줄이기 (인벤 꽉차면 증발 방지)
        int remaining = inv.TryAddItemFromLoot(insertID, insertItemCount);
        int added = insertItemCount - remaining;

        if (added <= 0)
        {
            // 하나도 못 넣었으면: 드랍 슬롯 변화 없음
            Debug.Log("[GetItem] 인벤이 가득 차서 루팅 실패");
            return;
        }

        if (remaining > 0)
        {
            // 부분만 들어갔으면: 드랍 슬롯 수량만 감소 (아이템 유지)
            insertItemCount = remaining;
            RefreshUI();

            // ✅ 루팅 이후 드랍 슬롯 스택 자동 합치기
            GetComponentInParent<LootContainer>()?.NotifyLootChanged();
            return;
        }

        // 전부 들어갔으면: 슬롯 비우기
        insertID = 0;
        insertItemCount = 0;
        RefreshUI();

        // ✅ 루팅 이후 드랍 슬롯 스택 자동 합치기
        GetComponentInParent<LootContainer>()?.NotifyLootChanged();
    }
}
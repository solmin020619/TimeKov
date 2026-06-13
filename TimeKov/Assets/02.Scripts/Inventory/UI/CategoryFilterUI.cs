// CategoryFilterUI.cs
// FilterBar ������Ʈ�� ���̴� ��ũ��Ʈ
// ī�װ��� ��ư Ŭ�� �� InventoryGridUI �� ���� ���� �˸�
// null �̸� ��ü ǥ��, ���� ������ �ش� ī�װ����� ǥ��

using System;
using UnityEngine;
using UnityEngine.UI;

public class CategoryFilterUI : MonoBehaviour
{
    // ��ư �迭 (Inspector ���� ������� �Ҵ�)
    // 0: AllFilterBtn
    // 1: RawMaterialBtn     (RawMaterial)
    // 2: ProcessedTier1Btn  (ProcessedTier1)
    // 3: ProcessedTier2Btn  (ProcessedTier2)
    // 4: TacticalBtn        (TacticalConsumable)
    // 5: CoreBtn            (CoreUpgrade)
    // 6: SpecialBtn         (Special)
    [Header("ī�װ��� ��ư (0��: ��ü, 1~6��: ī�װ��� �������)")]
    [SerializeField] private Button[] filterButtons;

    [Header("����")]
    [SerializeField] private Color selectedColor = new Color(0.30f, 0.60f, 0.90f, 1f);
    [SerializeField] private Color normalColor = new Color(0.20f, 0.25f, 0.35f, 1f);
    [SerializeField] private Color selectedBorderColor = new Color(0.37f, 0.77f, 1f, 1f);    // 선택 탭 테두리 (시안)
    [SerializeField] private Color normalBorderColor = new Color(0.59f, 0.70f, 0.80f, 0.26f); // 비선택 테두리 (크롬)
    [SerializeField] private Color selectedIconColor = new Color(0.68f, 0.89f, 1f, 1f);       // 선택 아이콘 (밝은 시안)
    [SerializeField] private Color normalIconColor = new Color(0.76f, 0.83f, 0.90f, 1f);      // 비선택 아이콘

    // ���� ���� �̺�Ʈ
    // null �̸� ��ü, ���� ������ �ش� ī�װ���
    public event Action<ItemCategory?> OnFilterChanged;

    // ���� ���õ� ī�װ��� (null = ��ü)
    private ItemCategory? _current = null;

    // ���� ���õ� ī�װ��� �ܺ� �б�� ������Ƽ
    public ItemCategory? CurrentFilter => _current;

    // ���� ���õ� ��ư �ε��� (0 = ��ü)
    private int _selectedIndex = 0;

    // ī�װ��� ��ư �ε����� ���� ItemCategory �� ���� ���̺�
    // filterButtons[0] = ��ü(null), filterButtons[1] = RawMaterial, ...
    private static readonly ItemCategory?[] IndexToCategory = new ItemCategory?[]
    {
        null,                              // 0: ��ü
        ItemCategory.RawMaterial,          // 1: ���� ���
        ItemCategory.ProcessedTier1,       // 2: 1�� ����ǰ
        ItemCategory.ProcessedTier2,       // 3: 2�� ��ȭ ����ǰ
        ItemCategory.TacticalConsumable,   // 4: ���� �Ҹ�ǰ
        ItemCategory.CoreUpgrade,          // 5: �ھ� ��ȭ ���
        ItemCategory.Special               // 6: Ư��
    };

    private void Start()
    {
        if (filterButtons == null || filterButtons.Length == 0) return;

        // ��ư���� �ε��� ��� Ŭ�� �̺�Ʈ ���
        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i] == null) continue;
            int capturedIndex = i;
            filterButtons[i].onClick.AddListener(() => SetFilterByIndex(capturedIndex));
        }

        // ù ��° ��ư(��ü) ���� ���·� �ʱ�ȭ
        UpdateButtonColors();
    }

    // ��ư �ε����� ���� ����
    public void SetFilterByIndex(int index)
    {
        if (index < 0 || index >= IndexToCategory.Length) return;

        _selectedIndex = index;
        _current = IndexToCategory[index];

        UpdateButtonColors();
        OnFilterChanged?.Invoke(_current);
    }

    // ��ư ���� ����
    private void UpdateButtonColors()
    {
        if (filterButtons == null) return;

        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i] == null) continue;
            bool sel = (i == _selectedIndex);

            var img = filterButtons[i].GetComponent<Image>();
            if (img != null) img.color = sel ? selectedColor : normalColor;

            // 테두리(Outline) 색 전환
            var ol = filterButtons[i].GetComponent<UnityEngine.UI.Outline>();
            if (ol != null) ol.effectColor = sel ? selectedBorderColor : normalBorderColor;

            // 아이콘 색 전환 (자식 CatIcon)
            var iconTr = filterButtons[i].transform.Find("CatIcon");
            if (iconTr != null)
            {
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null) iconImg.color = sel ? selectedIconColor : normalIconColor;
            }
        }
    }

    // �ܺο��� ��ü ���� �ʱ�ȭ (�κ��丮 ���� �� ȣ��)
    public void ResetToAll()
    {
        SetFilterByIndex(0);
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

public class WarehouseUIVisibility : MonoBehaviour
{
    [Header("Base Scene Names (기지 씬 이름들)")]
    public string[] baseSceneNames = { "Base" };

    [Header("Hide in non-base scenes")]
    public GameObject warehousePanelRoot;   // 오른쪽 창고 UI 전체 루트 (⚠ Drop이 여기 밑이면 같이 꺼짐)
    public GameObject moveToWarehouseButton; // 왼쪽 '창고 이동' 버튼(또는 그 부모)

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    private void Apply()
    {
        bool isBase = IsBaseScene(SceneManager.GetActiveScene().name);

        // ✅ 추가: 레이드 씬이라도 "Loot UI가 켜져있는 상태"면
        // warehousePanelRoot(=Drop이 같이 붙어있는 자리)를 꺼버리면 안됨.
        bool keepRightPanelForLoot = false;
        if (!isBase && UIStateManager.Instance != null)
        {
            keepRightPanelForLoot = (UIStateManager.Instance.GetCurrentState() == UIStateManager.UIState.Loot);
        }

        if (warehousePanelRoot != null)
            warehousePanelRoot.SetActive(isBase || keepRightPanelForLoot);

        if (moveToWarehouseButton != null)
            moveToWarehouseButton.SetActive(isBase);
    }

    private bool IsBaseScene(string sceneName)
    {
        if (baseSceneNames == null || baseSceneNames.Length == 0) return false;

        for (int i = 0; i < baseSceneNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(baseSceneNames[i]) && sceneName == baseSceneNames[i])
                return true;
        }
        return false;
    }
}
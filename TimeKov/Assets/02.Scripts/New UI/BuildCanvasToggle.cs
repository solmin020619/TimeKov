using UnityEngine;

public class BuildCanvasToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private GameObject buildCanvas;

    [Header("Player Visual")]
    [Tooltip("빌드모드일 때 숨길 오브젝트 (팔, 무기 등)")]
    [SerializeField] private GameObject[] playerVisuals;

    private bool lastBuildMode = false;
    private bool isAltHeld = false;

    // 각 오브젝트에서 수집한 Renderer 목록
    private Renderer[][] cachedRenderers;

    private void Awake()
    {
        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        if (buildCanvas != null)
            buildCanvas.SetActive(false);

        CacheRenderers();
    }

    private void CacheRenderers()
    {
        if (playerVisuals == null)
            return;

        cachedRenderers = new Renderer[playerVisuals.Length][];

        for (int i = 0; i < playerVisuals.Length; i++)
        {
            if (playerVisuals[i] != null)
                cachedRenderers[i] = playerVisuals[i].GetComponentsInChildren<Renderer>(true);
        }
    }

    private void Update()
    {
        HandleCanvasToggle();
        HandleAltCursor();
    }

    private void HandleCanvasToggle()
    {
        if (buildManager == null || buildCanvas == null)
            return;

        bool current = buildManager.IsBuildMode;

        if (current != lastBuildMode)
        {
            buildCanvas.SetActive(current);
            SetPlayerVisualsVisible(!current);
            lastBuildMode = current;
        }
    }

    private void SetPlayerVisualsVisible(bool visible)
    {
        if (playerVisuals == null)
            return;

        for (int i = 0; i < playerVisuals.Length; i++)
        {
            if (playerVisuals[i] == null)
                continue;

            Renderer[] renderers = playerVisuals[i].GetComponentsInChildren<Renderer>(true);

            for (int j = 0; j < renderers.Length; j++)
            {
                if (renderers[j] != null)
                    renderers[j].enabled = visible;
            }
        }
    }

    private void HandleAltCursor()
    {
        bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (altDown == isAltHeld)
            return;

        isAltHeld = altDown;

        if (isAltHeld)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
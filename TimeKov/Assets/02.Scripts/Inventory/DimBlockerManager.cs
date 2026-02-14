using UnityEngine;

public class DimBlockerManager : MonoBehaviour
{
    public static DimBlockerManager Instance { get; private set; }

    [SerializeField] private GameObject dimBlocker;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dimBlocker != null)
            dimBlocker.SetActive(false);
    }

    public void SetDim(bool on)
    {
        if (dimBlocker == null) return;
        dimBlocker.SetActive(on);
    }
}

using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class LootBoxHighlight : MonoBehaviour
{
    public MonoBehaviour outlineComponent;

    private void Start()
    {
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (outlineComponent != null)
            {
                // 통일된 노란색 강조 아웃라인 적용 (QuickOutline일 때만 색/두께 통일)
                if (outlineComponent is Outline outline)
                    InteractOutline.Enable(outline);
                else
                    outlineComponent.enabled = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (outlineComponent != null)
            {
                outlineComponent.enabled = false;
            }
        }
    }
}
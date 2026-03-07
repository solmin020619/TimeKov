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
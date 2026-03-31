using UnityEngine;

public class BuildZone : MonoBehaviour
{
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }
}
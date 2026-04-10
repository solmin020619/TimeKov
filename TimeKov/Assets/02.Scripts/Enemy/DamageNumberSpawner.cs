using TMPro;
using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DamageNumberUI damageNumberPrefab;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;     // NEW: ±âº»µô »ö
    [SerializeField] private Color criticalColor = Color.yellow;  // NEW: Ä¡¸íÅ¸µô »ö

    public void ShowDamage(float damage, bool isCritical, Vector3 worldPos)
    {
        if (damageNumberPrefab == null)
            return;

        DamageNumberUI instance = Instantiate(damageNumberPrefab, transform);
        instance.Show(
            Mathf.RoundToInt(damage),
            isCritical ? criticalColor : normalColor,
            worldPos
        );
    }
}
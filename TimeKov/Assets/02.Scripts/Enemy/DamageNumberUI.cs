using System.Collections;
using TMPro;
using UnityEngine;

public class DamageNumberUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text damageText;

    [Header("Motion")]
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float floatSpeed = 1.2f;
    [SerializeField] private Vector3 randomOffset = new Vector3(0.25f, 0f, 0.25f);

    private Camera cam;
    private Vector3 worldPosition;
    private Color baseColor;

    public void Show(int damage, Color color, Vector3 spawnWorldPos)
    {
        cam = Camera.main;
        worldPosition = spawnWorldPos + new Vector3(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(0f, 0.2f),
            Random.Range(-randomOffset.z, randomOffset.z)
        );

        baseColor = color;

        if (damageText != null)
        {
            damageText.text = damage.ToString();
            damageText.color = color;
        }

        StartCoroutine(FloatRoutine());
    }

    private IEnumerator FloatRoutine()
    {
        float t = 0f;

        while (t < lifeTime)
        {
            t += Time.deltaTime;

            worldPosition += Vector3.up * floatSpeed * Time.deltaTime;

            if (cam == null)
                cam = Camera.main;

            transform.position = worldPosition;

            if (cam != null)
                transform.forward = cam.transform.forward;

            if (damageText != null)
            {
                Color c = baseColor;
                c.a = Mathf.Lerp(1f, 0f, t / lifeTime);
                damageText.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
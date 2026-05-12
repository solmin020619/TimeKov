using UnityEngine;
using System.Collections;

public class ScrollTexture : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private Vector2 scrollDirection = new Vector2(0f, -1f);

    private Renderer rend;
    private Material material;
    private Vector2 currentOffset;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // Utilisation de la propriété sharedMaterial pour éviter de créer une instance unique de matériel
            material = rend.sharedMaterial;

            // Vérifiez que le matériau utilise un shader compatible URP, par exemple "Universal Render Pipeline/Lit"
            if (material.shader.name.Contains("Universal Render Pipeline"))
            {
                currentOffset = material.GetTextureOffset("_BaseMap");
            }
            else
            {
                Debug.LogError("Le shader n'est pas compatible avec le Universal Render Pipeline.");
            }
        }
        else
        {
            Debug.LogError("Le composant Renderer n'a pas été trouvé sur cet objet.");
        }
    }

    private void Update()
    {
        if (material != null)
        {
            currentOffset += scrollDirection * scrollSpeed * Time.deltaTime;
            material.SetTextureOffset("_BaseMap", currentOffset);
        }
    }
}

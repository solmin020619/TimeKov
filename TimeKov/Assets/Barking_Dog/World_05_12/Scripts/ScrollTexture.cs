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
            // [TimeKov 수정] sharedMaterial → material 로 변경.
            // 원본은 sharedMaterial 을 잡고 매 프레임 SetTextureOffset 호출하는데,
            // 이러면 디스크 .mat 파일이 dirty 상태로 갱신되어 git에 영구 unstaged 변경으로 뜸
            // (ForceField_02.mat 의 m_Offset.y 가 시간 누적되며 무한히 변경됨).
            // material 로 바꾸면 Unity 가 런타임 인스턴스를 자동 복제해서 그 인스턴스만 수정 →
            // 비주얼 스크롤 효과는 그대로 유지되고, 디스크 원본 .mat 은 안 건드림.
            material = rend.material;

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

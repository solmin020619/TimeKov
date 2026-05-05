using System.Collections.Generic;
using UnityEngine;

namespace CombatGirlsCharacterPack
{
    [System.Serializable]
    public class MaterialSet
    {
        public List<Material> materials; // 한 세트에 포함될 머티리얼 리스트 (메쉬의 슬롯 순서대로)
    }

    public class cloth_Multy_MaterialChanger : MonoBehaviour
    {
        [SerializeField] private List<SkinnedMeshRenderer> characterMeshRenderers; // 여러 SkinnedMeshRenderer들을 등록할 수 있는 리스트
        [SerializeField] private List<MaterialSet> materialSets; // 변경할 머티리얼 세트들의 리스트

        private int currentMaterialIndex = 0; // 현재 선택된 머티리얼 세트의 인덱스

        public void ChangeMaterial()
        {
            if (materialSets.Count == 0 || characterMeshRenderers.Count == 0)
                return; // 리스트가 비어 있는 경우, 아무 작업도 하지 않음

            // 현재 세트에 해당하는 머티리얼들을 모든 SkinnedMeshRenderer에 적용
            foreach (SkinnedMeshRenderer renderer in characterMeshRenderers)
            {
                Material[] rendererMats = renderer.materials;
                MaterialSet currentSet = materialSets[currentMaterialIndex];

                for (int i = 0; i < rendererMats.Length; i++)
                {
                    if (i < currentSet.materials.Count && currentSet.materials[i] != null)
                    {
                        rendererMats[i] = currentSet.materials[i];
                    }
                }
                renderer.materials = rendererMats;
            }

            // 다음 세트로 인덱스를 이동, 리스트 끝에 도달하면 처음으로 돌아감
            currentMaterialIndex = (currentMaterialIndex + 1) % materialSets.Count;
        }
    }
}
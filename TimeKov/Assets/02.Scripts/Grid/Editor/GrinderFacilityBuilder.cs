// =====================================================================
// Editor/GrinderFacilityBuilder.cs
// Tools/TIMEKOV/설비/연마기 프리팹 생성
//
// 기존 5x5 설비(용해로)를 템플릿으로 복제해서 연마기 설비 프리팹을 만든다.
// 5x5 공용 베이스(5x5_objet: 받침대 + BuildPort 10개 + 콜라이더)와 스크립트 구성
// (ProcessingMachine / MachineLoopSound / MachineAnimationEffect / MachineProgressUI)을
// 그대로 물려받고, 아트 모델만 연마기로 갈아끼운다.
//   -> 포트 위치/방향을 손으로 10개 배치하지 않아도 되고, 기존 설비와 규격이 어긋날 일이 없다.
//
// [아트 프리팹]
//   03.Model/.../5x5/연마기/연마기.prefab (Center/side/side_2/under + Animator).
//   애니메이션(Take 001, 0.67초 루프)은 MachineAnimationEffect 에 연결되어
//   "가공 중에만" 재생된다 - 다른 설비와 동일한 규칙.
//
// [재실행 안전] 이미 만든 프리팹이 있으면 지우고 다시 만든다.
// =====================================================================

using UnityEngine;
using UnityEditor;
using TIMEKOV.Factory;

public static class GrinderFacilityBuilder
{
    // 템플릿 = 같은 5x5 규격의 기존 설비. 포트 구성이 여기서 복제된다.
    private const string TemplatePath = "Assets/05.Prefabs/Grid/5X5/용해로.prefab";
    private const string ArtPath      = "Assets/03.Model/Re_Base/3x3,5x5,object/5x5/연마기/연마기.prefab";
    private const string OutputPath   = "Assets/05.Prefabs/Grid/5X5/연마기.prefab";

    private const string MachineName = "연마기";

    // 아트를 얹는 높이.
    // ★연마기 아트(조립본)는 자기 안에 이미 받침대(5x5_objet)를 끼고 조립돼 있다.
    //   그래서 아트 원점 = 받침대 바닥이고, 0 에 놓아야 템플릿 받침대와 정확히 겹친다.
    //   (용해로처럼 받침대가 없는 fbx 를 얹을 땐 0.928 같은 값이 필요하다 - 모델마다 다르니 여기서 조정)
    private const float ArtHeight = 0f;

    // 아트 조립본이 자체적으로 품고 있어 '중복'이 되는 파트들.
    // 받침대는 템플릿 쪽(포트가 붙어 있는 진짜 규격)을 쓰고 아트 사본은 걷어낸다
    // - 안 걷어내면 받침대가 두 겹으로 깔려 바닥이 두꺼워 보인다.
    private static readonly string[] DuplicateArtParts = { "5x5_objet" };

    [MenuItem("Tools/TIMEKOV/설비/연마기 프리팹 생성")]
    public static void Build()
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        if (template == null) { Debug.LogError($"[연마기] 템플릿을 못 찾았다: {TemplatePath}"); return; }

        var art = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath);
        if (art == null) { Debug.LogError($"[연마기] 아트 프리팹을 못 찾았다: {ArtPath}"); return; }

        var root = PrefabUtility.LoadPrefabContents(TemplatePath);
        try
        {
            // 1) 템플릿의 아트/이펙트 걷어내기.
            //    남길 것 = 받침대(5x5_objet), BuildPort 들, [Build] BodyCollider.
            //    걷어낼 것 = 용해로 모델, 증기 FX(연마기엔 없다).
            int removed = 0;
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i).gameObject;
                if (IsStructuralPart(child)) continue;
                Object.DestroyImmediate(child);
                removed++;
            }

            // 2) 연마기 아트 얹기
            var artInstance = (GameObject)PrefabUtility.InstantiatePrefab(art, root.transform);
            artInstance.name = MachineName + "_Model";
            artInstance.transform.localPosition = new Vector3(0f, ArtHeight, 0f);
            artInstance.transform.localRotation = Quaternion.identity;
            artInstance.transform.localScale = Vector3.one;

            // 아트가 자체적으로 끼고 있는 받침대 사본 제거(템플릿 받침대와 겹쳐 두 겹이 된다).
            // 프리팹 인스턴스라 이름이 그대로 남아 있어 이름으로 찾는다.
            int dup = 0;
            foreach (var t in artInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == artInstance.transform) continue;
                foreach (var name in DuplicateArtParts)
                {
                    if (!t.name.StartsWith(name)) continue;
                    Object.DestroyImmediate(t.gameObject);
                    dup++;
                    break;
                }
            }

            // 아트 안의 콜라이더는 끈다. 배치/상호작용 판정은 '[Build] BodyCollider' 가 전담한다
            // (아트 콜라이더가 남으면 설치 검사에 걸려 자기 자리에 못 짓는 사고가 난다).
            foreach (var col in artInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            // 3) 가공 중 애니메이션 연결 - 다른 설비와 같은 경로(MachineAnimationEffect)
            var animators = artInstance.GetComponentsInChildren<Animator>(true);
            var animEffect = root.GetComponent<MachineAnimationEffect>();
            if (animEffect == null) animEffect = root.AddComponent<MachineAnimationEffect>();

            var soAnim = new SerializedObject(animEffect);
            var animArray = soAnim.FindProperty("animators");
            animArray.arraySize = animators.Length;
            for (int i = 0; i < animators.Length; i++)
                animArray.GetArrayElementAtIndex(i).objectReferenceValue = animators[i];
            // 아트에 딸려온 파티클(증기 등)이 있으면 가공 중에만 나오도록 연결한다.
            // 템플릿에서 물려온 옛 참조는 버리고 이 아트 것으로 다시 채운다.
            var fx = artInstance.GetComponentsInChildren<ParticleSystem>(true);
            var fxArray = soAnim.FindProperty("effects");
            if (fxArray != null)
            {
                fxArray.arraySize = fx.Length;
                for (int i = 0; i < fx.Length; i++)
                    fxArray.GetArrayElementAtIndex(i).objectReferenceValue = fx[i];
            }
            soAnim.ApplyModifiedPropertiesWithoutUndo();

            // 4) 이름표 교체 (설비 표시명은 시트가 권위지만, 프리팹 값도 맞춰 둔다)
            var machine = root.GetComponent<ProcessingMachine>();
            if (machine != null)
            {
                var so = new SerializedObject(machine);
                var nameProp = so.FindProperty("machineName");
                if (nameProp != null) nameProp.stringValue = MachineName;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var progress = root.GetComponent<MachineProgressUI>();
            if (progress != null)
            {
                var so = new SerializedObject(progress);
                var nameProp = so.FindProperty("machineName");
                if (nameProp != null) nameProp.stringValue = MachineName;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            root.name = MachineName;

            PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            AssetDatabase.Refresh();

            Debug.Log($"[연마기] 생성 완료 -> {OutputPath}\n" +
                      $"  템플릿 자식 {removed}개 제거(용해로 모델/증기FX), 아트 1개 부착.\n" +
                      $"  Animator {animators.Length}개를 가공 애니메이션에 연결(가공 중에만 재생).\n" +
                      $"  포트/콜라이더/사운드 구성은 용해로 규격 그대로.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 설비 규격을 이루는 부분인지(= 남겨야 하는지).
    // 이름이 아니라 '무엇이 붙어 있는가'로 판단한다 - 아트 이름이 바뀌어도 안전하다.
    private static bool IsStructuralPart(GameObject go)
    {
        if (go.GetComponentInChildren<BuildPort>(true) != null) return true;   // 입출력 포트
        if (go.name.Contains("[Build]")) return true;                          // 배치 판정 콜라이더
        // 받침대(5x5_objet)는 프리팹 인스턴스로 들어와 있다. 포트를 품고 있으면 위에서 이미 걸린다.
        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (src != null && AssetDatabase.GetAssetPath(src).Contains("5x5_objet")) return true;
        return false;
    }
}

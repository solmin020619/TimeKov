using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 프리팹을 굽는 빌더들이 공유하는 조각. 체력바 부착 + 시간 흡수 부착.
///
/// 기존 적 19종은 BaseEnemy.prefab 안에 체력바가 이미 들어 있어서 신경 쓸 일이 없었는데,
/// 모델에서 통째로 조립하는 신규 빌더(자폭거미/헬 몬스터/필드몹)는 이걸 안 붙이면 체력바가 없다.
/// 보스는 화면 상단 전용 바(BossHealthBarUI)를 쓰므로 붙이지 않는다.
/// </summary>
public static class EnemyBuildUtil
{
    const string HpBarPath = "Assets/05.Prefabs/UI/HP_Bar_World.prefab";

    // 시간 흡수 트레일 VFX.
    // ★[07-22] 예전엔 빌더들이 Enemy_DarknessSpider 프리팹에서 이 참조를 긁어왔는데,
    //   그 거미를 삭제하기로 하면서 에셋을 직접 가리키게 바꿨다. 흡수 연출을 바꾸려면 여기만 고치면 된다.
    const string AbsorbVfxPath =
        "Assets/12.VFX/Item Pickup VFX URP/VFX/Particles/Trails/VFX_Item_Trail_Uncommon.prefab";

    /// <summary>흡수 VFX 에셋. 못 찾으면 null(연출만 빠지고 회복은 정상 동작).</summary>
    public static GameObject LoadAbsorbVfx() =>
        AssetDatabase.LoadAssetAtPath<GameObject>(AbsorbVfxPath);

    /// <summary>
    /// 시간 흡수(EnemyAbsorbOnDeath)를 붙이고 VFX 를 연결한다.
    /// ★시간=HP 게임이라 이게 빠지면 "잡아도 시간이 안 차는" 몹이 된다. 보스 포함 전 몹에 필요.
    /// 이미 붙어 있으면 컴포넌트는 그대로 두고 비어 있는 VFX 만 채운다(수동 설정 우선).
    /// </summary>
    public static void AttachTimeAbsorb(GameObject go, string logTag)
    {
        if (go == null) return;

        var absorb = go.GetComponent<EnemyAbsorbOnDeath>();
        if (absorb == null) absorb = go.AddComponent<EnemyAbsorbOnDeath>();

        var so = new SerializedObject(absorb);
        var p = so.FindProperty("absorbVfxPrefab");
        if (p == null) return;

        if (p.objectReferenceValue == null)
        {
            var vfx = LoadAbsorbVfx();
            if (vfx == null)
            {
                Debug.LogWarning($"[{logTag}] 흡수 VFX 를 못 찾았다: {AbsorbVfxPath} (회복은 되고 연출만 빠진다)");
                return;
            }
            p.objectReferenceValue = vfx;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 머리 위 체력바 + 이름표를 자식으로 붙인다.
    /// 이름은 EnemyHealth 가 Awake 에서 IEnemyDataSource -> Data.enemyName 으로 알아서 채운다.
    /// 프리팹 링크를 유지해서 붙이므로, 나중에 체력바 디자인을 고치면 전 몹에 같이 반영된다.
    /// </summary>
    public static void AttachWorldHpBar(GameObject go, string logTag)
    {
        if (go == null) return;

        if (go.GetComponentInChildren<EnemyWorldUI>(true) != null) return;   // 이미 있으면 그대로

        var barPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPath);
        if (barPrefab == null)
        {
            Debug.LogWarning($"[{logTag}] 체력바 프리팹을 못 찾았다: {HpBarPath} (체력바 없이 생성된다)");
            return;
        }

        var bar = (GameObject)PrefabUtility.InstantiatePrefab(barPrefab, go.transform);
        if (bar == null)
        {
            Debug.LogWarning($"[{logTag}] 체력바 부착 실패");
            return;
        }

        // 위치/크기는 EnemyWorldUI 가 런타임에 메시 높이를 재서 직접 잡는다.
        // 여기서 어설프게 맞춰두면 오히려 스케일이 곱해져 어긋난다.
        bar.transform.localPosition = Vector3.zero;
        bar.transform.localRotation = Quaternion.identity;
        bar.transform.localScale = Vector3.one;
    }
}

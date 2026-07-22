using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 프리팹을 굽는 빌더들이 공유하는 조각.
/// 지금은 머리 위 체력바 부착 하나뿐이다.
///
/// 기존 적 19종은 BaseEnemy.prefab 안에 체력바가 이미 들어 있어서 신경 쓸 일이 없었는데,
/// 모델에서 통째로 조립하는 신규 빌더(자폭거미/헬 몬스터)는 이걸 안 붙이면 체력바가 없다.
/// 보스는 화면 상단 전용 바(BossHealthBarUI)를 쓰므로 붙이지 않는다.
/// </summary>
public static class EnemyBuildUtil
{
    const string HpBarPath = "Assets/05.Prefabs/UI/HP_Bar_World.prefab";

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

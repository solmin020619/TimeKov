using UnityEngine;

/// <summary>
/// 전역 연료 설정. Resources/FuelConfig.asset 에 저장해 두면 런타임에 자동으로 로드된다.
/// 아직 연료 아이템이 없으면 fuelItemId에 임시 아이템 ID를 입력해 테스트할 수 있다.
/// 나중에 정식 연료 아이템이 추가되면 ID만 바꾸면 된다.
/// </summary>
[CreateAssetMenu(fileName = "FuelConfig", menuName = "TIMEKOV/Fuel Config")]
public class FuelConfig : ScriptableObject
{
    [Tooltip("연료로 사용할 아이템 ID.\n" +
             "아직 연료 아이템이 없으면 임시 아이템 ID를 입력하세요.\n" +
             "정식 연료 아이템 추가 후 이 값만 바꾸면 됩니다.")]
    public int fuelItemId = 4101;

    [Tooltip("연료 1개당 설비 가동 시간 (초)")]
    public float secondsPerFuel = 40f;

    // ── 싱글턴 접근 ────────────────────────────────────────────────────
    private static FuelConfig _instance;

    public static FuelConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<FuelConfig>("FuelConfig");

            if (_instance == null)
                Debug.LogWarning("[FuelConfig] Resources/FuelConfig.asset 을 찾지 못했습니다. " +
                                 "Assets/Resources/ 폴더에 FuelConfig 에셋을 생성해 주세요.");
            return _instance;
        }
    }

    // 에디터에서 에셋 값을 바꿀 때 캐시 무효화 (에디터 전용)
#if UNITY_EDITOR
    private void OnValidate() => _instance = null;
#endif
}

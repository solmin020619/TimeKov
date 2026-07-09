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

    // ── 런타임 오버라이드 (우주선 수리 보상 = 설비 연료 효율) ─────────────
    // SO 원본(secondsPerFuel)은 건드리지 않고, 진행 보상이 이 값을 덮어쓴다. 0 이하면 미설정.
    private static float _secondsOverride = 0f;

    /// <summary>런타임 연료 효율 오버라이드 설정. 0 이하면 SO 값을 사용.</summary>
    public static void SetSecondsOverride(float seconds) => _secondsOverride = seconds;

    /// <summary>실제 적용할 연료 1개당 가동 시간(초). 오버라이드 우선, 없으면 SO값, 그것도 없으면 40.</summary>
    public static float SecondsPerFuel
    {
        get
        {
            if (_secondsOverride > 0f) return _secondsOverride;
            return Instance != null ? Instance.secondsPerFuel : 40f;
        }
    }

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

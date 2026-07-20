using UnityEngine;

/// <summary>
/// 프롤로그 씬 전용 설정 컴포넌트.
/// Inspector에서 값을 조정하고 IntroVideoController가 참조한다.
/// </summary>
public class PrologueSettings : MonoBehaviour
{
    [Header("카메라 앵글 고정 (영상 종료 직후)")]
    [Tooltip("수평 회전각 (Yaw). 0=정면, 양수=오른쪽, 음수=왼쪽")]
    public float CameraLockYaw = 0f;

    [Tooltip("수직 회전각 (Pitch). 음수=위, 양수=아래")]
    [Range(-20f, 60f)]
    public float CameraLockPitch = -10f;

    [Tooltip("true: Inspector 값으로 고정 / false: 영상 종료 시점 카메라 앵글 그대로 고정")]
    public bool UseCustomAngle = true;
}

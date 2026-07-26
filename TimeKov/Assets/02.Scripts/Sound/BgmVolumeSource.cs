using UnityEngine;

/// <summary>
/// 이 컴포넌트가 붙은 오브젝트의 AudioSource는 VolumeSync(SFX 볼륨 일괄 동기화)에서 제외된다.
/// 런타임 생성 BGM 소스(예: BattleBgm의 [BattleBgm])가 SFX 볼륨에 덮여 BGM 슬라이더가 안 먹던 문제 방지용.
/// 인스펙터 excludeSources 에 못 넣는 동적 소스를 표시하는 용도.
/// </summary>
public class BgmVolumeSource : MonoBehaviour { }

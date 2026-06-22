using System.Collections.Generic;
using UnityEngine;

// 게임 UI/이벤트 효과음 ID. GameSfxConfig SO 에서 각 ID에 클립을 꽂는다.
public enum SfxId
{
    None = 0,
    QuestStart,
    TutorialHighlight,
    ToastShow,
    ChestOpenStart,
    ChestOpenComplete,
    FacilityUnlockStart,
    FacilityUnlockComplete,
    CodexOpen,
    CodexClose,
    CodexHover,
    CodexClick,
    SettingsClick,
}

// 런타임 UI/이벤트 효과음 재생기 (지연 싱글톤, 씬 세팅 불필요).
// 클립 지정 방법 2가지(둘 다 지원):
//   (1) 간단: Resources/Sfx/ 에 SfxId 이름과 같은 클립 파일을 떨군다(예: Resources/Sfx/CodexOpen.wav). SO 불필요.
//   (2) 조절: Resources/Sfx/GameSfxConfig SO 에서 ID별 클립+볼륨 지정(SO가 있으면 SO 우선).
// 클립 없는 ID는 무음(안전). 정지(timeScale 0) 중에도 오디오는 재생됨.
public class GameSfx : MonoBehaviour
{
    private static GameSfx _i;
    private static bool _quitting;

    public static GameSfx I
    {
        get
        {
            if (_i == null && !_quitting)
            {
                var go = new GameObject("[GameSfx]");
                _i = go.AddComponent<GameSfx>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    private GameSfxConfig _config;
    private AudioSource _source;
    private readonly Dictionary<SfxId, AudioClip> _nameCache = new Dictionary<SfxId, AudioClip>();   // 이름 폴백 로드 캐시(null=시도했으나 없음)

    private void Build()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.spatialBlend = 0f;   // 2D
        _source.playOnAwake = false;
        _config = Resources.Load<GameSfxConfig>("Sfx/GameSfxConfig");   // 없어도 됨(이름 폴백 사용)
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_i == this) _i = null; }

    // 효과음 재생(2D). 클립 없으면 조용히 무시.
    public static void Play(SfxId id)
    {
        if (_quitting || id == SfxId.None) return;
        I.PlayInternal(id);
    }

    // 3D(공간) 효과음 재생. 지정한 월드 위치에서 거리감 있게 들린다.
    public static void Play(SfxId id, Vector3 position)
    {
        if (_quitting || id == SfxId.None) return;
        I.PlayInternal3D(id, position);
    }

    private void PlayInternal(SfxId id)
    {
        if (Resolve(id, out var clip, out var volume)) _source.PlayOneShot(clip, volume);
    }

    private void PlayInternal3D(SfxId id, Vector3 position)
    {
        if (!Resolve(id, out var clip, out var volume)) return;
        var go = new GameObject("[GameSfx3D]");
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;                          // 3D
        src.minDistance  = 3f;                          // 설비 제작음(MachineLoopSound)과 동일한 거리 감쇠
        src.maxDistance  = 20f;
        src.rolloffMode  = AudioRolloffMode.Logarithmic;
        src.playOnAwake = false;
        src.Play();
        Destroy(go, clip.length + 0.1f);   // 재생 끝나면 임시 오브젝트 정리
    }

    // 클립+볼륨 해석. (1) SO 설정 우선(볼륨 조절 가능) (2) Resources/Sfx/<이름> 폴백.
    private bool Resolve(SfxId id, out AudioClip clip, out float volume)
    {
        volume = 1f;
        if (_config != null)
        {
            var e = _config.Get(id);
            if (e != null && e.clip != null) { clip = e.clip; volume = e.volume; return true; }
        }
        clip = LoadByName(id);
        return clip != null;
    }

    private AudioClip LoadByName(SfxId id)
    {
        if (_nameCache.TryGetValue(id, out var cached)) return cached;   // null 포함(중복 로드 방지)
        var clip = Resources.Load<AudioClip>("Sfx/" + id.ToString());
        _nameCache[id] = clip;
        return clip;
    }
}

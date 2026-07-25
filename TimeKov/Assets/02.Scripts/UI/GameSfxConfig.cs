using System.Collections.Generic;
using UnityEngine;

// 게임 UI/이벤트 효과음 설정 (인스펙터 편집). Resources/Sfx/GameSfxConfig 로 로드.
// 사운드/기획: 각 SfxId 에 클립을 꽂으면 코드 훅(GameSfx.Play)에서 재생된다. 안 꽂으면 무음.
[CreateAssetMenu(menuName = "TIMEKOV/Game Sfx Config")]
public class GameSfxConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip clip;
        [Tooltip("여러 개 지정하면 재생마다 이 중 하나를 랜덤 선택(발소리 등). 비어있으면 위 clip 사용.")]
        public List<AudioClip> clips = new List<AudioClip>();
        [Range(0f, 3f)] public float volume = 1f;   // 라우드니스 정규화로 1 초과(부스트) 가능
    }

    public List<Entry> sounds = new List<Entry>();

    public Entry Get(SfxId id)
    {
        foreach (var e in sounds)
            if (e != null && e.id == id) return e;
        return null;
    }
}

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
        [Range(0f, 1f)] public float volume = 1f;
    }

    public List<Entry> sounds = new List<Entry>();

    public Entry Get(SfxId id)
    {
        foreach (var e in sounds)
            if (e != null && e.id == id) return e;
        return null;
    }
}

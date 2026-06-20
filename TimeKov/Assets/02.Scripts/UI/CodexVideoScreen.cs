using UnityEngine;
using UnityEngine.Video;

// 도감 튜토리얼 본문의 인라인 영상 재생기. 클립을 RenderTexture에 재생해
// 도감 미디어 칸(RawImage)에 보여준다. VideoPlayer는 timeScale=0(정지) 영향 안 받아
// 도감 정지 중에도 재생됨(TutorialVideoUI와 동일). 무음 루프.
public class CodexVideoScreen : MonoBehaviour
{
    const int W = 1280, H = 720;
    VideoPlayer _vp;
    RenderTexture _rt;

    void Ensure()
    {
        if (_vp != null) return;
        _rt = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32) { name = "CodexTutorialRT" };
        _rt.filterMode = FilterMode.Bilinear;
        _rt.Create();

        _vp = gameObject.AddComponent<VideoPlayer>();
        _vp.playOnAwake = false;
        _vp.source = VideoSource.VideoClip;
        _vp.renderMode = VideoRenderMode.RenderTexture;
        _vp.targetTexture = _rt;
        _vp.isLooping = true;
        _vp.waitForFirstFrame = true;
        _vp.skipOnDrop = true;
        _vp.audioOutputMode = VideoAudioOutputMode.None;
    }

    public RenderTexture Play(VideoClip clip)
    {
        if (clip == null) return null;
        Ensure();
        if (_vp.clip != clip) _vp.clip = clip;
        _vp.Play();
        return _rt;
    }

    public void Stop()
    {
        if (_vp != null) _vp.Stop();
    }

    void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
    }
}

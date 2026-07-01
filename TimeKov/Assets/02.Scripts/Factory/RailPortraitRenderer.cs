using UnityEngine;

// 실제 레일(RailPiece straight 프리팹)을 림보(먼 곳)에서 전용 카메라로 RenderTexture 에 렌더 ->
// 설비 UI 의 RawImage 에 표시. CodexMonsterPortrait(도감 초상화) 패턴 그대로 개조.
// URP 언릿 셰이더/시안 글로우(_Fresnel_color)/화살표 UV 흐름이 그대로 나온다 = "가짜 선"이 아니라 실제 레일.
// 배경 투명(alpha 0) 이라 UI 패널 위에 레일만 얹힌다.
public class RailPortraitRenderer : MonoBehaviour
{
    static readonly Vector3 Limbo = new Vector3(6000f, -9000f, 0f);   // 월드와 안 겹치는 먼 곳

    Camera _cam;
    Transform _stage;
    Transform _holder;
    GameObject _current;
    RenderTexture _rt;

    [Header("프레이밍(캡쳐 보고 튜닝)")]
    public int   tileCount = 3;     // 레일 토막 개수(길이). 앞뒤로 이어붙임.
    public float tileGap   = 1f;    // 토막 간격(레일 한 칸 크기)
    public float camYaw    = 0f;    // 카메라 좌우 각
    public float camPitch  = 90f;   // 위에서 내려보는 각(90=탑뷰, 레퍼런스처럼 깔끔)
    public float railYaw   = 270f;  // 레일 스트립 회전(90/270=가로, 180도 차이로 흐름방향 반전). 270=오른쪽으로 흐름
    public float fov       = 28f;
    public float fill      = 0.82f; // 화면 채움(작을수록 크게)

    void EnsureRig()
    {
        if (_cam != null) return;

        _stage = new GameObject("RailStage").transform;
        _stage.SetParent(transform, false);
        _stage.position = Limbo;

        var camGo = new GameObject("RailCam");
        camGo.transform.SetParent(_stage, false);
        _cam = camGo.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // 투명 배경(레일만 남김)
        _cam.fieldOfView = fov;
        _cam.nearClipPlane = 0.03f;
        _cam.farClipPlane = 300f;
        _cam.cullingMask = ~0;      // 림보엔 이 레일 인스턴스만 존재
        _cam.allowHDR = true;       // 시안 글로우(HDR _Fresnel_color)가 살아나게
        _cam.allowMSAA = false;
        _cam.enabled = false;       // 표시할 때만 켬

        _holder = new GameObject("RailHolder").transform;
        _holder.SetParent(_stage, false);
    }

    /// <summary>레일 프리팹을 RT 에 렌더하고 그 RT(Texture)를 돌려준다. 없으면 null.</summary>
    public Texture Render(GameObject railPrefab, int w, int h)
    {
        if (railPrefab == null) return null;
        EnsureRig();
        Clear();

        if (_rt == null || _rt.width != w || _rt.height != h)
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "RailPortraitRT" };
            _rt.Create();
            _cam.targetTexture = _rt;
        }

        // 비활성 상태로 띄워 스크립트 끈 뒤 활성화(RailPiece 등 라이프사이클 안 굴러가게).
        _holder.gameObject.SetActive(false);
        _current = new GameObject("RailStrip");
        _current.transform.SetParent(_holder, false);
        _current.transform.localPosition = Vector3.zero;
        _current.transform.localRotation = Quaternion.Euler(0f, railYaw, 0f);   // 90 = 가로 흐름

        int n = Mathf.Max(1, tileCount);
        for (int i = 0; i < n; i++)
        {
            var tile = Instantiate(railPrefab, _current.transform);
            tile.transform.localPosition = new Vector3(0f, 0f, (i - (n - 1) * 0.5f) * tileGap);
            tile.transform.localRotation = Quaternion.identity;
            tile.transform.localScale = Vector3.one;
            // 흐름 연속성: 토막마다 _PathOffset (RailPiece 가 하던 것과 동일).
            foreach (var r in tile.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m].HasProperty("_PathOffset")) mats[m].SetFloat("_PathOffset", i);
                r.materials = mats;
            }
        }
        Strip(_current);
        _holder.gameObject.SetActive(true);

        FrameCamera();
        _cam.enabled = true;   // URP 는 enabled 카메라를 매 프레임 RT 에 렌더(흐름 애니 계속 돎)
        return _rt;
    }

    void FrameCamera()
    {
        var b = CalcBounds(_current);
        float radius = Mathf.Max(Mathf.Max(b.size.x, b.size.y), b.size.z) * 0.5f;
        float dist = radius / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.1f, fill);
        Quaternion rot = Quaternion.Euler(camPitch, camYaw, 0f);
        Vector3 dir = rot * Vector3.forward;
        _cam.transform.position = b.center - dir * dist;
        _cam.transform.rotation = rot;
        _cam.fieldOfView = fov;
    }

    static Bounds CalcBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static void Strip(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) mb.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        { rb.isKinematic = true; rb.detectCollisions = false; }
    }

    public void Clear()
    {
        if (_current != null) { Destroy(_current); _current = null; }
    }

    void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
    }
}

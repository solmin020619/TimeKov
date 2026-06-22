using UnityEngine;
using UnityEngine.UI;
using JeffGrawAssets.FlexibleUI;

// UIBlur(블러 캔버스의 BagBlurRegion)에 붙여 블러 강도를 인스펙터 슬라이더로 실시간 조절하는 개발 도구.
// 데모 방식: UIBlur + Screen Space-Camera 캔버스. (BlurredImage + Overlay 방식은 플러그인이 복잡한 셋업을 요구해서 폐기)
// 틴트는 여기서 안 함 - 인벤 패널(Overlay) Image 색이 틴트를 담당한다.
[ExecuteAlways]
public class InventoryBlurTuner : MonoBehaviour
{
    [Header("블러 (Play 중 실시간 조절)")]
    [Tooltip("블러 강도(addDist). 높을수록 배경 뭉개짐. 6 = 엔필처럼 배경 알아볼만큼만 부드럽게. 너무 높으면 배경 사라져 회색죽.")]
    [Range(0f, 16f)] public float blurStrength = 6f;

    [Tooltip("반복 횟수. 클수록 더 부드럽게 퍼짐.")]
    [Range(1, 8)] public int iterations = 5;

    [Tooltip("샘플 간격.")]
    [Range(0.5f, 4f)] public float sampleDistance = 1.5f;

    [Tooltip("채도. 0=배경색 자연스럽게 비침(엔필). 정석 글래스모피즘은 오히려 +로 올림. -로 내리면 회색죽.")]
    [Range(-1f, 1f)] public float vibrancy = 0f;

    [Tooltip("밝기. 블러 중앙만 떠서 링 생김 -> 0 권장. 전체 밝기는 패널 흰막으로 낸다.")]
    [Range(-1f, 1f)] public float brightness = 0.02f;

    private UIBlur _blur;
    private bool _logged;

    private void OnEnable()   { Resolve(); Apply(); }
    private void OnValidate() { Resolve(); Apply(); }
    private void Update()     { Resolve(); Apply(); Diagnose(); }

    private void Resolve()
    {
        if (_blur == null) _blur = GetComponent<UIBlur>();
        if (_blur == null || _blur.Common == null) return;

        var cam = PickScreenCamera();

        // UIBlur가 블러할 카메라
        var camRef = _blur.Common.cameraReference;
        if (camRef == null || !camRef.isActiveAndEnabled || camRef.targetTexture != null)
            _blur.Common.cameraReference = cam;

        // Screen Space-Camera 캔버스도 같은 카메라를 봐야 렌더됨 (worldCamera 비면 캔버스가 안 그려짐)
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera &&
            (canvas.worldCamera == null || !canvas.worldCamera.isActiveAndEnabled))
            canvas.worldCamera = cam;
    }

    private void Apply()
    {
        if (_blur == null || _blur.Common == null) return;
        var s = _blur.Common.blurInstanceSettings;
        if (s == null) return;

        s.blurAdditionalDistancePerIteration = blurStrength;
        s.vibrancy = vibrancy;
        s.brightness = brightness;
        if (s.blurSections != null)
            foreach (var sec in s.blurSections)
            {
                sec.iterations = Mathf.Max(1, iterations);
                sec.sampleDistance = sampleDistance;
            }
        _blur.Common.ValidateBlur();
    }

    // 화면에 렌더하는 카메라 중 depth가 가장 높은 것 (RenderTexture 대상 제외)
    private static Camera PickScreenCamera()
    {
        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled && main.targetTexture == null)
            return main;

        Camera best = null;
        foreach (var c in Camera.allCameras)
        {
            if (c.targetTexture != null) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best != null ? best : main;
    }

    // 진단 (자동 1회 + F8)
    private void Diagnose()
    {
        bool key = Input.GetKeyDown(KeyCode.F8);
        if ((_logged && !key) || !Application.isPlaying || _blur == null || _blur.Common == null) return;
        _logged = true;

        var cam = _blur.Common.cameraReference;
        int fnum = _blur.Common.featureNumber;
        bool passForCam = cam != null && FlexibleBlurFeature.GlobalFlexibleBlurPassDict.ContainsKey((cam, fnum));
        var s = _blur.Common.ActiveSettings;
        float addDist = s != null ? s.blurAdditionalDistancePerIteration : -1f;
    }
}

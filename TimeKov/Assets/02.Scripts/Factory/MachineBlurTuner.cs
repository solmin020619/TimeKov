using UnityEngine;
using JeffGrawAssets.FlexibleUI;

// 공장 설비 패널 PanelBlur(BlurredImage)용 런타임 카메라 동기화 + 블러 강도 라이브 튜너.
// ★빌드시점에 박은 cameraReference 가 Play 에서 어긋나면(비활성/타깃텍스처/다른 카메라)
//   BlurredImage 가 샘플링을 못 해 블러가 "납작한 반투명 패널"처럼 죽는다.
//   -> 매 프레임 화면 카메라(타깃텍스처 없는 최고 depth)로 교정한다.
// 인벤은 UIBlur 용 InventoryBlurTuner 가 같은 역할 — BlurredImage 엔 무관해서 이 전용 튜너가 필요.
// 강도 값은 슬라이더로 Play 중 실시간 조절(빌더 재실행 불필요).
[ExecuteAlways]
[RequireComponent(typeof(BlurredImage))]
public class MachineBlurTuner : MonoBehaviour
{
    [Header("블러 강도 (Play 중 실시간 조절)")]
    [Tooltip("★강도 메인 레버 = 반복마다 퍼짐 증가량. 렌더식: 샘플거리=(기본샘플 + 이값 x 반복). 클수록 강블러(형태 소멸). 인벤 6=보통, 엔필급 14~16.")]
    [Range(0f, 16f)] public float addDistancePerIteration = 14f;
    [Tooltip("★메인 블러 반복 횟수. 클수록 더 강하고 부드럽게.")]
    [Range(1, 16)] public int iterations = 8;
    [Tooltip("기본 샘플 간격.")]
    [Range(0.5f, 8f)] public float sampleDistance = 2.5f;
    [Tooltip("다운스케일(프리블러) 반복.")]
    [Range(1, 8)] public int downscaleIterations = 3;
    [Tooltip("다운스케일 샘플 간격.")]
    [Range(0.5f, 6f)] public float downscaleSampleDistance = 3f;
    [Tooltip("리샘플 기준해상도. 주로 성능/품질 - ★이 환경선 강도 효과 거의 없음(강도는 위 addDist/iterations). 낮으면 노이즈.")]
    [Range(64, 1080)] public int referenceResolution = 540;
    [Tooltip("저해상 노이즈 억제(7-tap 리샘플).")]
    public bool hqResample = true;

    private BlurredImage _blur;

    private void OnEnable()   { Resolve(); Apply(); }
    private void OnValidate() { Resolve(); Apply(); }
    private void Update()     { Resolve(); Apply(); }

    // 매 프레임 화면 카메라로 동기화(빌드시점 카메라 stale 방지 = 블러 죽음 방지).
    private void Resolve()
    {
        if (_blur == null) _blur = GetComponent<BlurredImage>();
        if (_blur == null || _blur.Common == null) return;

        var cam = PickScreenCamera();
        if (cam != null && _blur.Common.cameraReference != cam)
            _blur.Common.cameraReference = cam;
    }

    private void Apply()
    {
        if (_blur == null || _blur.Common == null) return;
        var s = _blur.Common.blurInstanceSettings;
        if (s == null) return;

        s.referenceResolution = referenceResolution;
        s.hqResample = hqResample;
        s.blurAdditionalDistancePerIteration = addDistancePerIteration;
        if (s.blurSections != null)
            foreach (var sec in s.blurSections) { sec.iterations = Mathf.Max(1, iterations); sec.sampleDistance = sampleDistance; }
        if (s.downscaleSections != null)
            foreach (var sec in s.downscaleSections) { sec.iterations = Mathf.Max(1, downscaleIterations); sec.sampleDistance = downscaleSampleDistance; }
        _blur.Common.ValidateBlur();
    }

    // 화면에 렌더하는 카메라 중 depth 가장 높은 것 (RenderTexture 대상 제외).
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
}

using UnityEngine;
using JeffGrawAssets.FlexibleUI;

// BlurredImage 블러 강도를 인스펙터 슬라이더로 실시간 조절하는 개발 도구.
// 사용법: BlurredImage가 붙은 패널(예: BagPanel)에 이 컴포넌트를 같이 부착.
//        Play 중에 아래 슬라이더를 드래그하면 블러가 바로 바뀐다.
// (먼저 패널 Image를 Convert to BlurredImage 해둘 것. 없으면 아무 동작 안 함.)
[ExecuteAlways]
public class InventoryBlurTuner : MonoBehaviour
{
    [Header("블러 (Play 중 실시간 조절)")]
    [Tooltip("블러 강도. 클수록 더 흐릿. HANDOFF 26px 느낌은 6~9 부근에서 시작해 맞춰보면 됨.")]
    [Range(0f, 16f)] public float blurStrength = 7f;

    [Tooltip("반복 횟수. 클수록 더 부드럽고 넓게 퍼짐(비용 증가).")]
    [Range(1, 8)] public int iterations = 4;

    [Tooltip("샘플 간격. 미세 조절용.")]
    [Range(0.5f, 4f)] public float sampleDistance = 1.5f;

    [Header("패널 틴트 (어둡기/불투명도)")]
    [Tooltip("높을수록 더 어둡고 묵직(밝은 배경에서도 또렷). 디자인 느낌은 0.7~0.85.")]
    [Range(0f, 1f)] public float tintAlpha = 0.78f;
    [Tooltip("패널 틴트 색 (네이비 계열).")]
    public Color tintColor = new Color(0.043f, 0.086f, 0.153f);

    private BlurredImage _blur;

    private void OnEnable()   { Resolve(); Apply(); }
    private void OnValidate() { Resolve(); Apply(); }   // 인스펙터 값 변경 시 즉시 반영
    private void Update()     { Apply(); }              // Play 중 드래그 반영

    private void Resolve()
    {
        if (_blur == null) _blur = GetComponent<BlurredImage>();
    }

    private void Apply()
    {
        if (_blur == null || _blur.Common == null) return;
        var s = _blur.Common.blurInstanceSettings;
        if (s == null) return;

        s.blurAdditionalDistancePerIteration = blurStrength;
        if (s.blurSections != null)
        {
            foreach (var sec in s.blurSections)
            {
                sec.iterations = Mathf.Max(1, iterations);
                sec.sampleDistance = sampleDistance;
            }
        }
        _blur.Common.ValidateBlur();

        // 패널 틴트(어둡기) - BlurredImage 색 알파로 묵직함 조절
        _blur.color = new Color(tintColor.r, tintColor.g, tintColor.b, tintAlpha);
    }
}

using UnityEngine;
using UnityEngine.UI;

// [08-02] UISpriteFactory 스프라이트를 실행 시 다시 넣어주는 부품.
//
// 왜 필요한가: UISpriteFactory 가 만드는 스프라이트는 에셋 파일이 아니라 메모리 생성물이다.
//   에디터에서 만들어 씬/프리팹에 저장해두면 유니티를 껐다 켤 때 사라진다(이 프로젝트에서 겪은 함정).
//   그래서 '구조는 씬에 저장, 스프라이트만 실행 시 재적용' 으로 나눈다.
//
// 사용법: 빌더가 Image 에 이걸 붙이고 모양 값만 넣어두면, 실행 시 스스로 sprite 를 채운다.
//   Image.type(Sliced/Filled 등)은 직렬화되므로 빌더가 그대로 저장하면 된다.
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class RuntimeGeneratedSprite : MonoBehaviour
{
    public enum Shape
    {
        RoundedRect,        // 둥근 사각형 (9-slice)
        Ring,               // 링 (속 빈 동그라미)
        Chevron,            // 화살표 '>' (왼쪽은 localScale.x 를 -1 로 뒤집어 쓴다)
        Disc,               // 소프트 원 (후광/글로우)
        VFade,              // 세로 알파 페이드 (주사선/비네트)
        RoundedRectVGrad,   // 세로 그라데이션 둥근 사각 (9-slice)
    }

    [Tooltip("UISpriteFactory 캐시 키(예: rr_48_16). 값이 있으면 아래 모양 설정보다 이게 우선한다.\n" +
             "런타임 생성 UI 를 씬으로 옮길 때 빌더가 자동으로 채워넣는다(호출부를 안 고쳐도 되게).")]
    public string sourceKey;

    [Tooltip("만들 모양. sourceKey 가 비어 있을 때만 쓴다.")]
    public Shape shape = Shape.RoundedRect;
    [Tooltip("생성할 텍스처 세로 크기(px). 모서리가 뭉개지면 키운다. Chevron 외에는 정사각.")]
    public int texSize = 64;
    [Tooltip("RoundedRect / RoundedRectVGrad 의 모서리 반경(px).")]
    public int radius = 4;
    [Tooltip("Ring 의 두께(px).")]
    public float ringThickness = 6f;
    [Tooltip("Chevron 의 선 두께(px).")]
    public float chevronThickness = 7f;
    [Tooltip("Chevron 의 가로/세로 비율. 0.75 면 texSize 64 -> 48x64 텍스처.")]
    public float chevronAspect = 0.75f;

    [Header("VFade / RoundedRectVGrad 전용")]
    [Tooltip("위쪽 색. VFade 는 알파만 쓴다.")]
    public Color topColor = Color.white;
    [Tooltip("아래쪽 색. VFade 는 알파만 쓴다.")]
    public Color bottomColor = new Color(1f, 1f, 1f, 0f);

    private void Awake() => Apply();

    /// <summary>설정한 모양대로 sprite 를 채운다. 실행 시 자동 호출되고,
    /// 빌더도 만든 직후 한 번 불러 에디터에서 모양이 보이게 한다(그 참조는 재시작 때 사라진다).</summary>
    public void Apply()
    {
        var img = GetComponent<Image>();
        if (img == null) return;
        if (!string.IsNullOrEmpty(sourceKey))
        {
            var byKey = UISpriteFactory.FromKey(sourceKey);
            if (byKey != null) { img.sprite = byKey; return; }
        }
        img.sprite = shape switch
        {
            Shape.Ring => UISpriteFactory.Ring(texSize, ringThickness),
            Shape.Chevron => UISpriteFactory.Chevron(
                Mathf.Max(1, Mathf.RoundToInt(texSize * chevronAspect)), texSize, chevronThickness),
            Shape.Disc => UISpriteFactory.Disc(texSize),
            Shape.VFade => UISpriteFactory.VFade(Alpha(topColor), Alpha(bottomColor), texSize),
            Shape.RoundedRectVGrad => UISpriteFactory.RoundedRectVGrad(topColor, bottomColor, texSize, radius),
            _ => UISpriteFactory.RoundedRect(texSize, radius),
        };
    }

    private static byte Alpha(Color c) => (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
}

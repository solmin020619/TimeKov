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
    public enum Shape { RoundedRect, Ring, Chevron }

    [Tooltip("RoundedRect = 둥근 사각형 / Ring = 링(아이콘용) / Chevron = 화살표 '>' (왼쪽은 localScale.x 를 -1 로 뒤집어 쓴다)")]
    public Shape shape = Shape.RoundedRect;
    [Tooltip("생성할 텍스처 세로 크기(px). 모서리가 뭉개지면 키운다. RoundedRect/Ring 은 정사각.")]
    public int texSize = 64;
    [Tooltip("RoundedRect 의 모서리 반경(px).")]
    public int radius = 4;
    [Tooltip("Ring 의 두께(px).")]
    public float ringThickness = 6f;
    [Tooltip("Chevron 의 선 두께(px).")]
    public float chevronThickness = 7f;
    [Tooltip("Chevron 의 가로/세로 비율. 0.75 면 texSize 64 -> 48x64 텍스처.")]
    public float chevronAspect = 0.75f;

    private void Awake()
    {
        var img = GetComponent<Image>();
        if (img == null) return;
        img.sprite = shape switch
        {
            Shape.Ring => UISpriteFactory.Ring(texSize, ringThickness),
            Shape.Chevron => UISpriteFactory.Chevron(
                Mathf.Max(1, Mathf.RoundToInt(texSize * chevronAspect)), texSize, chevronThickness),
            _ => UISpriteFactory.RoundedRect(texSize, radius),
        };
    }
}

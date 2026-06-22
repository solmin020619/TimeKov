using UnityEngine;
using UnityEngine.UI;

// 가방 패널 간유리 PNG를 Play 중 키로 갈아끼며 비교(A/B)하는 개발 도구.
// skins[] = 빌더가 sprites 폴더의 panel_* 스프라이트로 자동 채움.
// (개발자 단축키 F9/F10 제거됨) 인스펙터 index/alpha 로만 비교. color 알파는 PNG 무관 유지.
// 톤 확정되면 PanelSpritePath만 그걸로 박고 이 컴포넌트는 제거하면 됨.
public class InventoryPanelSkinSwapper : MonoBehaviour
{
    public Sprite[] skins;
    public int index;
    [Range(0f, 1f)] public float alpha = 0.55f;

    private Image _img;

    void OnEnable() { Apply(); }

    void Update()
    {
        if (_img == null) _img = GetComponent<Image>();
        // 알파 라이브 반영 (Play 중 슬라이더 드래그하면 즉시 적용)
        if (_img != null && !Mathf.Approximately(_img.color.a, alpha))
        {
            var c = _img.color; c.a = alpha; _img.color = c;
        }
    }

    void Apply()
    {
        if (_img == null) _img = GetComponent<Image>();
        if (_img == null || skins == null || skins.Length == 0) return;
        index = Mathf.Clamp(index, 0, skins.Length - 1);
        if (skins[index] != null)
        {
            _img.sprite = skins[index];
            _img.type = Image.Type.Sliced;
        }
        var c = Color.white; c.a = alpha;
        _img.color = c;
    }
}

using UnityEngine;
using UnityEngine.UI;

// 가방 패널 간유리 PNG를 Play 중 키로 갈아끼며 비교(A/B)하는 개발 도구.
// skins[] = 빌더가 sprites 폴더의 panel_* 스프라이트로 자동 채움.
// F9 = 다음, F10 = 이전. color 알파(비침 배율)는 PNG 무관 유지 -> 톤/그레인만 비교됨.
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
        if (!Application.isPlaying || skins == null || skins.Length == 0) return;
        if (Input.GetKeyDown(KeyCode.F9)) Step(1);
        else if (Input.GetKeyDown(KeyCode.F10)) Step(-1);
    }

    void Step(int dir)
    {
        index = (index + dir + skins.Length) % skins.Length;
        Apply();
        var n = skins[index] != null ? skins[index].name : "null";
        Debug.Log($"[PanelSkin] {index + 1}/{skins.Length} = {n} (alpha {alpha:0.00})");
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

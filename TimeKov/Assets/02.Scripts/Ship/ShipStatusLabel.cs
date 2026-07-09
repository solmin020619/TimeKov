using TMPro;
using UnityEngine;

// 폐우주선 위에 상시 표시되는 수리 진행 라벨.
// 상호작용은 부품 있을 때만 켜지므로(arming), 진행도는 안 열어도 여기서 보이게 한다.
//   평소     : "우주선 수리 Lv.N/M"
//   부품 준비 : "우주선 수리 Lv.N/M" + "수리 준비 - F" (색 강조)
//   최종 완료 : "우주선 수리 완료"
// ShipRepairManager.OnChanged 를 구독해 갱신, 카메라를 향해 빌보드.
// (라벨은 우주선 스케일을 안 물려받게 독립 오브젝트로 생성)
public class ShipStatusLabel : MonoBehaviour
{
    [Tooltip("라벨 높이 (월드 m).")]
    [SerializeField] private float height = 4f;
    [Tooltip("라벨 글자 크기.")]
    [SerializeField] private float fontSize = 20f;

    private static readonly Color NormalCol = new Color(0.80f, 0.88f, 0.96f, 1f);
    private static readonly Color ReadyCol  = new Color(0.42f, 0.82f, 1.00f, 1f);
    private static readonly Color DoneCol   = new Color(0.45f, 0.85f, 0.55f, 1f);

    private RectTransform _root;
    private TMP_Text _text;
    private Transform _camTr;

    private void OnEnable()  => ShipRepairManager.OnChanged += Refresh;
    private void OnDisable() => ShipRepairManager.OnChanged -= Refresh;

    private void Start()
    {
        Build();
        Refresh();
    }

    private void Build()
    {
        var go = new GameObject("ShipStatusLabel");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        _root = (RectTransform)go.transform;
        _root.sizeDelta   = new Vector2(280f, 64f);
        _root.localScale  = Vector3.one * 0.01f;   // 부모 스케일 비상속 → 항상 일정 크기
        _root.position    = transform.position + Vector3.up * height;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(_root, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        _text = txtGo.AddComponent<TextMeshProUGUI>();
        _text.alignment     = TextAlignmentOptions.Center;
        _text.fontSize      = fontSize;
        _text.fontStyle     = FontStyles.Bold;
        _text.raycastTarget = false;
        _text.color         = NormalCol;
    }

    private void Refresh()
    {
        if (_text == null) return;

        var mgr = ShipRepairManager.Instance;
        if (mgr == null) { _text.text = ""; return; }

        if (mgr.IsFullyRepaired)
        {
            _text.text  = "우주선 수리 완료";
            _text.color = DoneCol;
        }
        else if (mgr.CanRepairNext())
        {
            _text.text  = $"우주선 수리 Lv.{mgr.CurrentLevel}/{mgr.MaxLevel}\n<size=80%>수리 준비 - F</size>";
            _text.color = ReadyCol;
        }
        else
        {
            _text.text  = $"우주선 수리 Lv.{mgr.CurrentLevel}/{mgr.MaxLevel}";
            _text.color = NormalCol;
        }
    }

    private void LateUpdate()
    {
        if (_root == null) return;

        _root.position = transform.position + Vector3.up * height;

        if (_camTr == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _camTr = cam.transform;
        }
        _root.forward = _camTr.forward;
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }
}

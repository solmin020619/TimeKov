// =====================================================================
// BlueprintModeButton.cs
// 건축 모드 힌트 알약 "청사진 모드 [N]" 의 런타임 동작.
// 해제 모드 알약을 복제한 오브젝트에 붙는다(생성 = 시트 메뉴가 아니라
// Tools/TIMEKOV/UI/청사진 버튼 생성). 클릭하면 청사진 모드 토글.
//
// [클릭 배선이 런타임인 이유]
//   이 오브젝트는 프리팹(QuickSlotPanel) 안에 있고 BuildManager 는 씬에 있다.
//   프리팹 에셋은 씬 오브젝트를 영구 참조할 수 없어서 onClick 을 에디터에서 못 꽂는다.
//   그래서 Awake 에서 코드로 연결한다.
// =====================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BlueprintModeButton : MonoBehaviour
{
    [Tooltip("알약의 문구 라벨. 언어 변경 시 '청사진 모드' 로 갱신된다(빌더가 연결).")]
    [SerializeField] private TextMeshProUGUI label;

    private BuildManager _buildManager;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;   // 복제 원본(힌트 알약)에 셀렉터블 스프라이트가 없다
        btn.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        Refresh();
        Loc.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        if (label != null) label.text = Loc.Get("청사진 모드");
    }

    private void OnClick()
    {
        if (_buildManager == null)
            _buildManager = FindAnyObjectByType<BuildManager>();
        _buildManager?.ToggleBlueprintMode();
    }
}

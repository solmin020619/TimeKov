using UnityEngine;
using TMPro;

/// TMP_Dropdown은 Show() 시 Template.anchoredPosition을 런타임에 덮어씀.
/// 이 컴포넌트는 Template이 활성화되는 프레임의 LateUpdate에서 오프셋을 재적용해
/// 버튼과 드롭다운 목록이 하나로 합쳐진 것처럼 보이게 한다.
[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownOverlapFixer : MonoBehaviour
{
    [SerializeField] float overlapPx = 28f;

    TMP_Dropdown _dd;
    RectTransform _templateRt;
    bool _wasActive;

    void Awake()
    {
        _dd = GetComponent<TMP_Dropdown>();
        _templateRt = _dd.template;
    }

    void LateUpdate()
    {
        if (_templateRt == null) return;
        bool active = _templateRt.gameObject.activeSelf;
        if (active && !_wasActive)
            _templateRt.anchoredPosition += new Vector2(0f, overlapPx);
        _wasActive = active;
    }
}

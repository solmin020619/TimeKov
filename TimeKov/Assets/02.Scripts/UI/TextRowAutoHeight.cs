using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 줄 수가 런타임에 정해지는 TMP 라벨을 레이아웃 안에서 제 높이로 잡아준다.
//
// [왜 필요한가]
//   세로 레이아웃(ChildControlHeight=1)은 자식의 preferredHeight 를 물어 높이를 정한다.
//   TMP 가 돌려주는 preferredHeight 는 '마지막으로 그렸을 때의 폭' 기준이라, 글자를 바꾼
//   직후처럼 폭이 아직 안 잡힌 순간에는 틀린 값이 나온다. 그러면 상자가 글자보다 짧아지고,
//   세로 정렬이 가운데인 라벨은 넘친 만큼 위아래로 삐져나가 윗줄을 덮어버린다.
//   -> LayoutElement.preferredHeight 를 우리가 직접 재서 박는다. 이건 TMP 값보다 우선한다.
//
// [핵심 = 언제 재느냐]
//   ★Instantiate 직후나 글자 대입 직후에 재면 안 된다. 그때 폭은 아직 프리팹 값이다.
//     폭이 실제로 정해지는 순간에 오는 콜백이 OnRectTransformDimensionsChange 라, 여기서 잰다.
//     (퀘스트 목표 줄 ObjectiveLine 이 같은 함정을 같은 방법으로 피한다)
//
// [무한루프 주의]
//   높이를 쓰면 레이아웃이 다시 돌고 -> 콜백이 또 오고 -> 또 쓰는 고리가 생긴다.
//   그래서 값이 실제로 달라질 때만 쓴다(Epsilon).
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class TextRowAutoHeight : MonoBehaviour
{
    const float Epsilon = 0.5f;   // 이만큼도 안 바뀌면 안 쓴다(레이아웃 <-> 콜백 되먹임 차단)

    [Tooltip("글자 위아래로 더 확보할 여백(px).")]
    public float verticalPadding = 0f;

    private TMP_Text _text;
    private LayoutElement _layout;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _layout = GetComponent<LayoutElement>();
        if (_layout == null) _layout = gameObject.AddComponent<LayoutElement>();
    }

    private void OnEnable() => Fit();

    private void OnRectTransformDimensionsChange() => Fit();

    /// <summary>글자를 바꾼 쪽에서 직접 부를 수도 있다(폭이 이미 잡혀 있으면 즉시 반영된다).</summary>
    public void Fit()
    {
        if (_text == null || _layout == null || !isActiveAndEnabled) return;

        float w = _text.rectTransform.rect.width;
        if (w <= 1f) return;   // 아직 폭이 안 잡혔다. 잡히면 이 콜백이 다시 온다.

        float want = _text.GetPreferredValues(_text.text, w, 0f).y + verticalPadding;
        if (Mathf.Abs(_layout.preferredHeight - want) < Epsilon) return;
        _layout.preferredHeight = want;
    }
}

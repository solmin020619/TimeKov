using UnityEngine;
using TMPro;

// TMP 라벨에 붙이면 언어 변경 시 자동으로 번역된 텍스트로 갱신된다.
// Inspector의 _koreanKey에 한글 원문을 입력한다 (Loc.Get()의 키).
[RequireComponent(typeof(TMP_Text))]
public class LocalizedLabel : MonoBehaviour
{
    [SerializeField] string _koreanKey;

    private TMP_Text _text;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        Loc.OnLanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Loc.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        if (_text != null)
            _text.text = Loc.Get(_koreanKey);
    }

    // 런타임에서 키를 바꿔야 할 때 (예: 동적으로 생성되는 UI)
    public void SetKey(string koreanKey)
    {
        _koreanKey = koreanKey;
        Refresh();
    }
}

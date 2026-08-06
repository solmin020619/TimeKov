using UnityEngine;
using TMPro;

// TMP 라벨에 붙이면 언어 변경 시 자동으로 번역된 텍스트로 갱신된다.
// Inspector의 _koreanKey에 한글 원문을 입력한다 (Loc.Get()의 키).
[RequireComponent(typeof(TMP_Text))]
public class LocalizedLabel : MonoBehaviour
{
    [SerializeField] string _koreanKey;
    [SerializeField] string _prefix;   // 번역 앞에 붙는 고정 접두어 (예: "+  ")

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
            _text.text = _prefix + Loc.Get(_koreanKey);
    }

    public void SetKey(string koreanKey)
    {
        _koreanKey = koreanKey;
        Refresh();
    }
}

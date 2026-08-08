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
    private string _lastWritten;       // 내가 마지막으로 써넣은 값
    private bool _yielded;             // 코드가 이 라벨을 직접 쓴다고 판단 -> 관여 중단

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
        if (_text == null || _yielded) return;

        // ★내가 쓴 값이 남의 손에 바뀌어 있으면, 그 라벨은 코드가 매번 갱신하는 것이다.
        //   (예: "용량 0 / 35" 처럼 값이 들어가는 줄). 여기서 원문 번역으로 되돌리면
        //   코드가 채운 숫자를 지워버린다. 한 번이라도 감지되면 영구히 손을 뗀다.
        //   AttachToStaticLabels 로 자동 부착할 때 오작동을 막는 안전장치다.
        if (_lastWritten != null && _text.text != _lastWritten) { _yielded = true; return; }

        _lastWritten = _prefix + Loc.Get(_koreanKey);
        _text.text = _lastWritten;
    }

    public void SetKey(string koreanKey)
    {
        _koreanKey = koreanKey;
        _yielded = false;
        _lastWritten = null;
        if (_text == null) _text = GetComponent<TMP_Text>();
        Refresh();
    }

    /// <summary>
    /// 루트 아래 '씬에 구운 정적 라벨'에만 LocalizedLabel 을 달아 준다.
    ///   씬/프리팹에 박힌 TMP 는 코드가 다시 쓰지 않으면 시트에 번역이 있어도 영원히
    ///   한국어로 남는다. 그런 라벨을 일일이 찾아 붙이는 대신 소유 컴포넌트가 한 줄 호출한다.
    /// ★번역 테이블의 키인 문구만 붙인다. "용량 0 / 35" 같은 런타임 값은 키가 아니므로
    ///   자동으로 걸러진다. 혹시 걸러지지 않아도 위 _yielded 안전장치가 받아낸다.
    /// 테이블이 아직 안 왔으면 아무것도 안 하고, 도착한 뒤 다시 부르면 된다.
    /// </summary>
    public static void AttachToStaticLabels(GameObject root)
    {
        if (root == null) return;
        var labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            var t = labels[i];
            if (t == null || t.GetComponent<LocalizedLabel>() != null) continue;
            if (string.IsNullOrWhiteSpace(t.text)) continue;
            if (!Loc.HasKey(t.text)) continue;
            t.gameObject.AddComponent<LocalizedLabel>().SetKey(t.text);
        }
    }
}

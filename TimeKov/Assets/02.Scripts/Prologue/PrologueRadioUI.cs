using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrologueRadioUI : MonoBehaviour
{
    public enum RadioTrigger { OnQuestShown, OnQuestCompleted }

    [System.Serializable]
    public class RadioEntry
    {
        public RadioTrigger trigger  = RadioTrigger.OnQuestShown;
        public QuestSO      quest;
        public Sprite       portrait;
        public string       speakerName = "알 수 없음";
        [TextArea(2, 5)]
        public string       message;
        [Tooltip("번역 키. 비워두면 message 원문을 키로 사용. 짧은 영문 ID 권장 (예: radio_intro_start)")]
        public string       locKey;
    }

    [Header("대화 목록")]
    [SerializeField] RadioEntry[] radioEntries;

    [Header("UI 레퍼런스 (Builder 자동 연결)")]
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] Image       _portraitImage;
    [SerializeField] TMP_Text    _speakerText;
    [SerializeField] TMP_Text    _messageText;
    [SerializeField] Image       _signalBar1;
    [SerializeField] Image       _signalBar2;
    [SerializeField] Image       _signalBar3;

    [Header("타이프라이터")]
    [SerializeField] float _typewriterSpeed = 13f;

    [Header("애니메이션")]
    [SerializeField] float _slideInDuration  = 0.30f;
    [SerializeField] float _fadeOutDuration  = 0.35f;

    // 퀘스트 완료 후 다음 대사 없을 때 닫히기까지 대기
    [SerializeField] float _hideDelay = 1.5f;
    // 타이프라이터 완료 후 플레이어가 읽을 시간 (초) — 다음 메시지 등장 전 최소 대기
    [SerializeField] float _readHoldDuration = 2.0f;

    Coroutine  _showCoroutine;
    Coroutine  _signalCoroutine;
    bool       _subscribed;
    bool       _isShowing;        // 현재 대사 표시 중
    RadioEntry _pendingEntry;     // 대기 중인 다음 대사 (가장 최신 것만 유지)

    // 패널 기준 anchoredPosition (슬라이드 기준점)
    Vector2 _restPos;

    // ── 초기화 ────────────────────────────────────────────────────────
    void Awake()
    {
        var rt = GetComponent<RectTransform>();
        if (rt) _restPos = rt.anchoredPosition;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
        }
        if (_messageText != null) _messageText.text = "";
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();

        var qm = QuestManager.Instance;
        if (qm == null || !qm.IsReady) return;
        foreach (var rt in qm.Runtimes)
            if (!rt.IsCategoryDone && rt.activeObjectives != null)
                TryShow(RadioTrigger.OnQuestShown, rt.data.quests[rt.activeQuestIndex]);
    }

    void TrySubscribe()
    {
        if (_subscribed) return;
        var qm = QuestManager.Instance;
        if (qm == null) return;
        qm.OnQuestShown     += HandleQuestShown;
        qm.OnQuestCompleted += HandleQuestCompleted;
        _subscribed = true;
    }

    void OnDestroy()
    {
        if (!_subscribed) return;
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestShown     -= HandleQuestShown;
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }
    }

    // ── 이벤트 ───────────────────────────────────────────────────────
    void HandleQuestShown(CategoryRuntime _, QuestSO quest)
    {
        TryShow(RadioTrigger.OnQuestShown, quest);
    }

    void HandleQuestCompleted(CategoryRuntime _, QuestSO quest)
    {
        if (!TryShow(RadioTrigger.OnQuestCompleted, quest))
        {
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(HideAfterDelay(_hideDelay));
        }
    }

    bool TryShow(RadioTrigger trigger, QuestSO quest)
    {
        foreach (var entry in radioEntries)
        {
            if (entry.trigger == trigger && entry.quest == quest)
            {
                Show(entry);
                return true;
            }
        }
        return false;
    }

    // ── 공개 API ─────────────────────────────────────────────────────
    public void ShowByIndex(int index)
    {
        if (index >= 0 && index < radioEntries.Length)
            Show(radioEntries[index]);
    }

    public void Hide()
    {
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        if (_signalCoroutine != null) StopCoroutine(_signalCoroutine);
        _showCoroutine = StartCoroutine(FadeOut());
    }

    // ── 내부 ─────────────────────────────────────────────────────────
    void Show(RadioEntry entry)
    {
        if (_isShowing)
        {
            // 현재 표시 중이면 대기열에 저장 (가장 최신 것만 유지)
            _pendingEntry = entry;
            return;
        }
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        _showCoroutine = StartCoroutine(ShowCoroutine(entry));
    }

    IEnumerator ShowCoroutine(RadioEntry entry)
    {
        _isShowing = true;
        _pendingEntry = null;

        if (_portraitImage != null)
        {
            _portraitImage.sprite  = entry.portrait;
            _portraitImage.enabled = entry.portrait != null;
        }
        if (_speakerText != null) _speakerText.text = Loc.Get(entry.speakerName);
        if (_messageText != null) { _messageText.text = ""; _messageText.maxVisibleCharacters = 0; }

        if (_signalCoroutine != null) StopCoroutine(_signalCoroutine);
        _signalCoroutine = StartCoroutine(SignalAnim());

        bool animDone = false;
        SlideIn(() => animDone = true);
        yield return new WaitUntil(() => animDone);

        string msg = !string.IsNullOrEmpty(entry.locKey)
            ? (Loc.CurrentLanguage == LanguageCode.KO ? entry.message : Loc.Get(entry.locKey))
            : Loc.Get(entry.message);
        yield return StartCoroutine(Typewriter(msg));

        // 타이프라이터 완료 후 최소 읽기 시간 확보
        yield return new WaitForSeconds(_readHoldDuration);

        _isShowing = false;

        // 대기 중인 다음 메시지가 있으면 이어서 표시
        if (_pendingEntry != null)
        {
            var next = _pendingEntry;
            _pendingEntry = null;
            Show(next);
        }
    }

    void SlideIn(System.Action onComplete)
    {
        if (_canvasGroup == null) { onComplete?.Invoke(); return; }

        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;

        _canvasGroup.DOFade(1f, _slideInDuration)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() => onComplete?.Invoke());
    }

    IEnumerator HideAfterDelay(float delay)
    {
        // 읽기 대기 중이면 그 시간도 포함되므로 isShowing 끝날 때까지 대기
        yield return new WaitUntil(() => !_isShowing);
        yield return new WaitForSeconds(delay);
        if (_pendingEntry == null)   // 새 메시지가 큐에 들어왔으면 닫지 않음
            yield return StartCoroutine(FadeOut());
    }

    IEnumerator Typewriter(string text)
    {
        if (_messageText == null) yield break;
        // 전체 텍스트를 먼저 설정하고 maxVisibleCharacters로 한 글자씩 공개.
        // text += c 방식은 TMP가 매 프레임 mesh를 재계산하면서 한국어 음절을 뒤섞는 버그 발생.
        _messageText.text = text;
        _messageText.maxVisibleCharacters = 0;
        float delay = _typewriterSpeed > 0f ? 1f / _typewriterSpeed : 0f;
        int total = text.Length;
        for (int i = 1; i <= total; i++)
        {
            _messageText.maxVisibleCharacters = i;
            if (delay > 0f) yield return new WaitForSeconds(delay);
        }
    }

    IEnumerator FadeOut()
    {
        if (_signalCoroutine != null) { StopCoroutine(_signalCoroutine); _signalCoroutine = null; }
        SetSignalBars(0f, 0f, 0f);
        if (_canvasGroup == null) yield break;

        bool done = false;
        _canvasGroup.DOFade(0f, _fadeOutDuration)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        _canvasGroup.interactable   = false;
                        _canvasGroup.blocksRaycasts = false;
                        done = true;
                    });
        yield return new WaitUntil(() => done);
    }

    IEnumerator SignalAnim()
    {
        float[] pattern = { 0.4f, 0.7f, 1f, 0.7f, 1f, 0.4f, 1f };
        int i = 0;
        while (true)
        {
            float v = pattern[i % pattern.Length];
            SetSignalBars(v * 0.5f, v * 0.75f, v);
            i++;
            yield return new WaitForSeconds(0.18f);
        }
    }

    void SetSignalBars(float a, float b, float c)
    {
        SetBarAlpha(_signalBar1, a);
        SetBarAlpha(_signalBar2, b);
        SetBarAlpha(_signalBar3, c);
    }

    void SetBarAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var col = img.color; col.a = alpha; img.color = col;
    }
}

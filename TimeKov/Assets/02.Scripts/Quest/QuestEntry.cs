using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestEntry : MonoBehaviour
{
    [Header("Single layout")]
    [SerializeField] GameObject singleRow;
    [SerializeField] TMP_Text singleLabel;
    [SerializeField] RectTransform singleStrike;
    [SerializeField] Image singleFlash;
    [SerializeField] Image singleCheck;

    [Header("Multi layout")]
    [SerializeField] GameObject multiRow;
    [SerializeField] TMP_Text multiTitle;
    [SerializeField] Transform multiObjectiveList;
    [SerializeField] ObjectiveLine objectiveLinePrefab;
    [SerializeField] RectTransform multiStrike;
    [SerializeField] Image multiFlash;
    [SerializeField] Image multiCheck;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip completeSfx;

    [Header("Layout")]
    [SerializeField] float defaultHeight = 40f;

    QuestSO _quest;
    CategoryRuntime _rt;
    bool _isMulti;

    // Single 모드 핸들러 — 명명 메서드 보관
    ObjectiveSO _singleObj;
    Action<float> _singleProgressHandler;
    Action<ObjectiveSO> _singleCompletedHandler;

    void Awake()
    {
        var le = GetComponent<LayoutElement>();
        if (le != null && le.preferredHeight > 0) defaultHeight = le.preferredHeight;
    }

    public void PlaySlideIn(QuestSO q, CategoryRuntime rt)
    {
        _quest = q; _rt = rt;
        _isMulti = q.objectives != null && q.objectives.Length >= 2;

        singleRow.SetActive(!_isMulti);
        multiRow.SetActive(_isMulti);

        if (_isMulti)
        {
            multiTitle.text = q.title;
            foreach (var o in rt.activeObjectives)
            {
                var line = Instantiate(objectiveLinePrefab, multiObjectiveList);
                line.Bind(o);
            }
            ResetVisuals(multiStrike, multiCheck, multiFlash);
        }
        else
        {
            _singleObj = rt.activeObjectives[0];
            _singleProgressHandler = OnSingleProgress;
            _singleCompletedHandler = OnSingleCompleted;
            _singleObj.OnProgressChanged += _singleProgressHandler;
            _singleObj.OnCompleted += _singleCompletedHandler;
            RefreshSingleLabel();
            ResetVisuals(singleStrike, singleCheck, singleFlash);
        }

        StartCoroutine(SlideInAndActivate());
    }

    void OnSingleProgress(float _) => RefreshSingleLabel();
    void OnSingleCompleted(ObjectiveSO _) => RefreshSingleLabel();

    void RefreshSingleLabel()
    {
        if (_singleObj != null && singleLabel != null)
            singleLabel.text = _singleObj.GetDisplayLabel();
    }

    static void ResetVisuals(RectTransform strike, Image check, Image flash)
    {
        if (strike) strike.sizeDelta = new Vector2(0, strike.sizeDelta.y);
        if (check) check.transform.localScale = Vector3.zero;
        if (flash) { var c = flash.color; c.a = 0; flash.color = c; }
    }

    IEnumerator SlideInAndActivate()
    {
        var le = GetComponent<LayoutElement>();
        if (le != null)
        {
            float target = defaultHeight;
            le.preferredHeight = 0f;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                le.preferredHeight = Mathf.Lerp(0f, target, t / 0.3f);
                yield return null;
            }
            le.preferredHeight = target;
        }

        // ★ UI 슬라이드 인 끝나는 시점에 Activate 호출 — 입력 카운트 시작
        QuestManager.Instance.ActivateObjectives(_rt);
    }

    public void PlayCompleteSequence(Action onDone)
    {
        StartCoroutine(WaitSlideThenComplete(onDone));
    }

    IEnumerator WaitSlideThenComplete(Action onDone)
    {
        yield return new WaitForSecondsRealtime(0.35f);   // 슬라이드 인 0.3 + 여유
        yield return CompleteRoutine(onDone);
    }

    IEnumerator CompleteRoutine(Action onDone)
    {
        if (!_isMulti && _singleObj != null)
        {
            _singleObj.OnProgressChanged -= _singleProgressHandler;
            _singleObj.OnCompleted -= _singleCompletedHandler;
        }

        var strike = _isMulti ? multiStrike : singleStrike;
        var check = _isMulti ? multiCheck : singleCheck;
        var flash = _isMulti ? multiFlash : singleFlash;
        var labelTextRef = _isMulti ? multiTitle : singleLabel;

        if (labelTextRef == null) { onDone?.Invoke(); yield break; }

        if (audioSource && completeSfx) audioSource.PlayOneShot(completeSfx);

        labelTextRef.ForceMeshUpdate();
        float fullWidth = labelTextRef.preferredWidth + 4f;

        // 0.0 ~ 0.3s — 취소선 + 플래시
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            float p = t / 0.3f;
            if (strike) strike.sizeDelta = new Vector2(Mathf.Lerp(0f, fullWidth, p), strike.sizeDelta.y);
            if (flash)
            {
                float fa = (p < 0.5f) ? Mathf.Lerp(0f, 0.3f, p * 2f) : Mathf.Lerp(0.3f, 0f, (p - 0.5f) * 2f);
                var c = flash.color; c.a = fa; flash.color = c;
            }
            yield return null;
        }

        // 0.3 ~ 0.5s — 체크 팝
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            float p = t / 0.2f;
            float scale = p < 0.6f ? Mathf.Lerp(0f, 1.2f, p / 0.6f) : Mathf.Lerp(1.2f, 1.0f, (p - 0.6f) / 0.4f);
            if (check) check.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.2f);

        // 0.7 ~ 1.1s — 페이드 + 슬라이드 업
        var cg = GetComponent<CanvasGroup>();
        var le = GetComponent<LayoutElement>();
        var rect = (RectTransform)transform;
        Vector2 startPos = rect.anchoredPosition;
        float startH = le != null ? le.preferredHeight : rect.rect.height;

        t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            float p = t / 0.4f;
            if (cg) cg.alpha = 1f - p;
            rect.anchoredPosition = startPos + Vector2.up * Mathf.Lerp(0f, 10f, p);
            if (le) le.preferredHeight = Mathf.Lerp(startH, 0f, p);
            yield return null;
        }

        onDone?.Invoke();
    }

    void OnDestroy()
    {
        if (!_isMulti && _singleObj != null && _singleProgressHandler != null)
        {
            _singleObj.OnProgressChanged -= _singleProgressHandler;
            _singleObj.OnCompleted -= _singleCompletedHandler;
        }
    }
}

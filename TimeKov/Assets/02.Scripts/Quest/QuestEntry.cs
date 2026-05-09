using System;
using System.Collections.Generic;
using DG.Tweening;
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

    [Header("Animation timing")]
    [Tooltip("ObjectiveLine 완료 후 vanish 애니 시작까지 hold 시간 (체크 보여주는 시간)")]
    [SerializeField] float vanishHoldTime = 0.25f;
    [Tooltip("ObjectiveLine vanish (alpha + height to 0) 애니 길이")]
    [SerializeField] float vanishDuration = 0.3f;
    [Tooltip("슬라이드 인 fade 길이")]
    [SerializeField] float slideInDuration = 0.3f;
    [Tooltip("완료 시퀀스 시작 전 hold (슬라이드 인 끝나길 기다림)")]
    [SerializeField] float completeStartDelay = 0.35f;

    QuestSO _quest;
    CategoryRuntime _rt;
    bool _isMulti;

    // Single 모드 핸들러 — 명명 메서드 보관
    ObjectiveSO _singleObj;
    Action<float> _singleProgressHandler;
    Action<ObjectiveSO> _singleCompletedHandler;

    // Multi 모드: ObjectiveSO 인스턴스 → ObjectiveLine
    readonly Dictionary<ObjectiveSO, ObjectiveLine> _objLines = new();
    Action<ObjectiveSO> _multiObjCompletedHandler;

    public void PlaySlideIn(QuestSO q, CategoryRuntime rt)
    {
        _quest = q; _rt = rt;
        _isMulti = q.objectives != null && q.objectives.Length >= 2;

        singleRow.SetActive(!_isMulti);
        multiRow.SetActive(_isMulti);

        if (_isMulti)
        {
            multiTitle.text = q.title;
            _multiObjCompletedHandler = OnMultiObjectiveCompleted;
            foreach (var o in rt.activeObjectives)
            {
                var line = Instantiate(objectiveLinePrefab, multiObjectiveList);
                line.Bind(o);
                _objLines[o] = line;
                o.OnCompleted += _multiObjCompletedHandler;
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

        // Nested CSF chain은 동적으로 추가된 자식에 대해 초기 layout pass 누락 가능
        // 부모 체인 모두 강제 rebuild — 한 번만, init 시점.
        RebuildLayoutChainUp();

        SlideInAndActivate();
    }

    void RebuildLayoutChainUp()
    {
        RectTransform cur = transform as RectTransform;
        int safety = 20;
        while (cur != null && safety-- > 0)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cur);
            cur = cur.parent as RectTransform;
        }
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

    // 슬라이드 인 — alpha fade. 높이는 CSF chain이 자동 결정.
    void SlideInAndActivate()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;

        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (cg != null) seq.Append(cg.DOFade(1f, slideInDuration));
        seq.OnComplete(() => QuestManager.Instance.ActivateObjectives(_rt));
    }

    // ─── Multi 모드 라인별 vanish — CSF가 부모 reflow 자동 처리 ──
    void OnMultiObjectiveCompleted(ObjectiveSO o)
    {
        if (_objLines.TryGetValue(o, out var line) && line != null)
            VanishLine(line);
    }

    void VanishLine(ObjectiveLine line)
    {
        if (line == null) return;

        var cg = line.GetComponent<CanvasGroup>();
        if (cg == null) cg = line.gameObject.AddComponent<CanvasGroup>();

        var le = line.GetComponent<LayoutElement>();
        float startH = le != null ? le.preferredHeight : 24f;

        // hold 후 alpha + height 동시에 0으로
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(line.gameObject);
        seq.AppendInterval(vanishHoldTime);
        if (cg != null) seq.Append(cg.DOFade(0f, vanishDuration));
        if (le != null)
        {
            seq.Join(DOTween.To(
                () => le.preferredHeight,
                v => le.preferredHeight = v,
                0f, vanishDuration));
        }
    }

    public void PlayCompleteSequence(Action onDone)
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

        if (labelTextRef == null) { onDone?.Invoke(); return; }

        labelTextRef.ForceMeshUpdate();
        float fullWidth = labelTextRef.preferredWidth + 4f;

        var cg = GetComponent<CanvasGroup>();

        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 슬라이드 인 끝나길 기다림
        seq.AppendInterval(completeStartDelay);
        seq.AppendCallback(() =>
        {
            if (audioSource && completeSfx) audioSource.PlayOneShot(completeSfx);
        });

        // Phase 1 (0.0~0.3s): 취소선 width 0 → fullWidth, flash 0→0.3→0 (peak at half)
        if (strike != null)
        {
            float strikeY = strike.sizeDelta.y;
            seq.Append(DOTween.To(
                () => strike.sizeDelta.x,
                v => strike.sizeDelta = new Vector2(v, strikeY),
                fullWidth, 0.3f));
        }
        else
        {
            seq.AppendInterval(0.3f);
        }

        if (flash != null)
        {
            // flash는 Phase 1과 병렬 (앞 0.15s 페이드 인, 뒤 0.15s 페이드 아웃)
            // Sequence 내부 경과 = completeStartDelay 후 시작
            seq.Insert(completeStartDelay, flash.DOFade(0.3f, 0.15f));
            seq.Insert(completeStartDelay + 0.15f, flash.DOFade(0f, 0.15f));
        }

        // Phase 2 (0.3~0.5s): 체크 팝 0 → 1.2 → 1.0
        if (check != null)
        {
            var checkTr = check.transform;
            seq.Append(checkTr.DOScale(1.2f, 0.12f).SetEase(Ease.OutQuad));
            seq.Append(checkTr.DOScale(1.0f, 0.08f).SetEase(Ease.InQuad));
        }
        else
        {
            seq.AppendInterval(0.2f);
        }

        // Phase 3 (0.5~0.7s): pause
        seq.AppendInterval(0.2f);

        // Phase 4 (0.7~1.1s): alpha fade out
        if (cg != null) seq.Append(cg.DOFade(0f, 0.4f));

        seq.OnComplete(() => onDone?.Invoke());
    }

    void OnDestroy()
    {
        if (!_isMulti && _singleObj != null && _singleProgressHandler != null)
        {
            _singleObj.OnProgressChanged -= _singleProgressHandler;
            _singleObj.OnCompleted -= _singleCompletedHandler;
        }

        if (_isMulti && _multiObjCompletedHandler != null)
        {
            foreach (var kv in _objLines)
            {
                if (kv.Key != null)
                    kv.Key.OnCompleted -= _multiObjCompletedHandler;
            }
            _objLines.Clear();
        }
    }
}

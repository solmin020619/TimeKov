using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 완료 시 띄우는 "업데이트 완료" 토스트 알림.
/// 좌측 슬라이드 인, hold, fade out 시퀀스.
/// 아이콘/박스/텍스트는 인스펙터에서 sprite/스타일 드래그.
/// QuestPanelUI가 OnQuestCompleted 시점에 Show() 호출.
/// </summary>
public class ToastNotification : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform root;
    [SerializeField] TMP_Text labelText;
    [Tooltip("토스트 좌측 아이콘. 인스펙터에서 sprite 드래그")]
    [SerializeField] Image iconImage;

    [Header("Animation")]
    [Tooltip("슬라이드 인 시작 시 X 오프셋 (음수 = 좌측에서 들어옴)")]
    [SerializeField] float slideOffsetX = -120f;
    [SerializeField] float slideInDuration = 0.25f;
    [Tooltip("슬라이드 인 끝나고 페이드 아웃 시작까지 대기")]
    [SerializeField] float holdDuration = 1.5f;
    [SerializeField] float fadeOutDuration = 0.3f;

    Vector2 _restPos;
    bool _initialized;
    Sequence _activeSeq;

    void Awake()
    {
        if (!_initialized) Initialize();
    }

    void Initialize()
    {
        if (root == null) root = transform as RectTransform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (root != null) _restPos = root.anchoredPosition;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
        _initialized = true;
    }

    /// <summary>
    /// 토스트 표시. 연속 호출 시 직전 시퀀스 Kill 후 재시작 (마지막 호출만 보임).
    /// delay > 0: 그 시간만큼 invisible 상태로 대기 후 슬라이드 인 시작.
    /// onHideStart: hold 끝나고 좌측 슬라이드 아웃 시작 시점에 호출. iconAlert 사라짐 동기화용.
    /// </summary>
    public void Show(string message, float delay = 0f, Action onHideStart = null)
    {
        if (!_initialized) Initialize();
        if (root == null) return;

        if (labelText != null) labelText.text = message;

        _activeSeq?.Kill();

        // delay 동안 invisible 유지. alpha 0으로. GameObject는 활성 (시퀀스 진행 보장).
        gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        _activeSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (delay > 0f) _activeSeq.AppendInterval(delay);
        _activeSeq.AppendCallback(() =>
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            root.anchoredPosition = _restPos + new Vector2(slideOffsetX, 0f);
        });
        _activeSeq.Append(root.DOAnchorPos(_restPos, slideInDuration).SetEase(Ease.OutCubic));
        _activeSeq.AppendInterval(holdDuration);

        // 사라짐 시작 시점 콜백 (iconAlert fade out 동시 트리거)
        _activeSeq.AppendCallback(() => onHideStart?.Invoke());

        // 좌측으로 슬라이드 아웃 + fade. 등장과 같은 방향으로 빠짐.
        _activeSeq.Append(root.DOAnchorPos(_restPos + new Vector2(slideOffsetX, 0f), fadeOutDuration).SetEase(Ease.InCubic));
        if (canvasGroup != null)
            _activeSeq.Join(canvasGroup.DOFade(0f, fadeOutDuration));

        _activeSeq.OnComplete(OnSequenceFinished);
    }

    void OnSequenceFinished()
    {
        _activeSeq = null;
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        _activeSeq?.Kill();
        _activeSeq = null;
    }
}

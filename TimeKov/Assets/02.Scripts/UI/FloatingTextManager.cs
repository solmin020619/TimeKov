// =====================================================================
// FloatingTextManager.cs
// 화면에 떠올랐다 사라지는 숫자 텍스트 (시간 감소 -N / 회복 +N 등).
// 전역 싱글톤 — 최초 호출 시 자동 생성(런타임 캔버스). 씬 세팅 불필요.
//
// 호출: FloatingTextManager.Show("-2", Color.red, screenPos);
//   screenPos = 화면 픽셀 좌표 (Overlay 캔버스의 RectTransform.position 그대로 사용 가능).
// =====================================================================

using DG.Tweening;
using TMPro;
using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    private static FloatingTextManager _i;
    public static FloatingTextManager I
    {
        get
        {
            if (_i == null)
            {
                var go = new GameObject("[FloatingTextManager]");
                _i = go.AddComponent<FloatingTextManager>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    // 연출 값
    const float RiseDistance = 64f;   // 떠오르는 거리(px)
    const float LifeTime = 0.9f;      // 표시 시간
    const float FontSize = 30f;
    const float JitterX = 22f;        // 좌우 랜덤 분산

    private RectTransform _root;

    public static void Show(string text, Color color, Vector3 screenPos)
        => I.ShowInternal(text, color, screenPos);

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5400; // HUD 위, 토스트(5500) 아래
        // CanvasScaler 없음 → 1 단위 = 1 화면 픽셀. 전달받은 screenPos를 그대로 배치.

        var rootGo = new GameObject("FloatingTextLayer", typeof(RectTransform));
        _root = (RectTransform)rootGo.transform;
        _root.SetParent(transform, false);
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.zero;
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;
    }

    private void ShowInternal(string text, Color color, Vector3 screenPos)
    {
        if (string.IsNullOrEmpty(text)) return;

        var go = new GameObject("FloatingText", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        float jitter = Random.Range(-JitterX, JitterX);
        Vector2 start = new Vector2(screenPos.x + jitter, screenPos.y);
        rt.anchoredPosition = start;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = FontSize;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // 떠오르며 페이드아웃 후 제거 (타임스케일 무시 — 일시정지 중에도 정상)
        rt.DOAnchorPosY(start.y + RiseDistance, LifeTime).SetUpdate(true).SetEase(Ease.OutCubic);
        cg.alpha = 1f;
        cg.DOFade(0f, LifeTime).SetUpdate(true).SetEase(Ease.InCubic)
            .OnComplete(() => { if (go != null) Destroy(go); });
    }
}

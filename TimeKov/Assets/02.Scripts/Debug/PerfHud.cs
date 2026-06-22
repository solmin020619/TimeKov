// =====================================================================
// PerfHud.cs
// 화면 좌상단에 성능 수치를 숫자로 띄우는 간이 계기판 (디버그 전용).
// 빈 GameObject 에 붙이면 됨.  F3 = 표시 토글,  F4 = 누적 측정 리셋.
//
// [실시간] FPS/ms, GC/frame(KB), SetPass, DrawCalls, Tris
// [누적]   F4 리셋 후 ~ 지금까지의 총 GC / 프레임수 / 최대 ms
//          -> 건축 클릭 같은 "찰나 동작" 비교용: F4 누르고 동작하고 총 GC 읽기.
//
// 커밋 노트 객관 수치: GC(낮을수록 좋음, 온도 무관), DrawCalls. FPS는 체감용.
// 출시 전엔 제거하거나 비활성화할 것.
// =====================================================================

using UnityEngine;
using Unity.Profiling;

public class PerfHud : MonoBehaviour
{
    [SerializeField] private bool visible = false;   // 개발자키(F3/F4) 제거됨 — 측정 쓰려면 인스펙터에서 visible 켜기
    [SerializeField] private float refreshInterval = 0.25f; // 표시 갱신 주기(초)

    private ProfilerRecorder gcAlloc;
    private ProfilerRecorder setPass;
    private ProfilerRecorder drawCalls;
    private ProfilerRecorder tris;

    private float avgMs;
    private float peakMs;
    private float timer;

    // 누적(F4 리셋 사이) — 찰나 동작 총량 측정용
    private long accumGC;
    private int accumFrames;
    private float accumPeakMs;
    private float accumTime;

    private string text = "측정 중...";
    private GUIStyle style;

    private void OnEnable()
    {
        gcAlloc   = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        setPass   = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        tris      = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
    }

    private void OnDisable()
    {
        gcAlloc.Dispose();
        setPass.Dispose();
        drawCalls.Dispose();
        tris.Dispose();
    }

    private void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        avgMs = avgMs <= 0f ? ms : Mathf.Lerp(avgMs, ms, 0.05f);
        if (ms > peakMs) peakMs = ms;

        // 매 프레임 누적
        long gcThis = gcAlloc.Valid ? gcAlloc.LastValue : 0;
        accumGC += gcThis;
        accumFrames++;
        accumTime += Time.unscaledDeltaTime;
        if (ms > accumPeakMs) accumPeakMs = ms;

        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        // 표시 텍스트는 refreshInterval 마다만 갱신 (HUD 자체 GC 최소화)
        float fps = avgMs > 0f ? 1000f / avgMs : 0f;
        text =
            $"FPS {fps:0}    {avgMs:0.0} ms (peak {peakMs:0.0})\n" +
            $"GC/frame {gcThis / 1024f:0.0} KB\n" +
            (setPass.Valid   ? $"SetPass {setPass.LastValue}\n"     : "") +
            (drawCalls.Valid ? $"DrawCalls {drawCalls.LastValue}\n" : "") +
            (tris.Valid      ? $"Tris {tris.LastValue / 1000}k\n"   : "") +
            $"-- 누적(F4 리셋) --\n" +
            $"GC합 {accumGC / 1024f:0} KB  /  {accumFrames}f  /  {accumTime:0.0}s\n" +
            $"구간 peak {accumPeakMs:0.0} ms";

        peakMs = 0f; // 실시간 peak 은 구간마다 리셋
    }

    private void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(6, 6, 320, 215), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 12, 320, 215), text, style);
    }
}

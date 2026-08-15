// UITween.cs — DOTween 없이 코루틴으로 도는 트윈 러너
// (MonoBehaviour는 클래스명과 파일명이 같아야 유니티가 스크립트 에셋으로 인식한다.
//  한 파일에 몰아두면 씬/프리팹에 저장할 때 참조가 끊겨 Missing Script가 된다.)
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GameSettingsUI
{
    public class UITween : MonoBehaviour
    {
        static UITween I;
        readonly Dictionary<string, Coroutine> running = new Dictionary<string, Coroutine>();

        public static void Ensure()
        {
            if (I != null) return;
            // DontDestroyOnLoad는 재생 중에만 쓸 수 있다. 에디터 베이크에서는 러너 없이
            // 값만 확정되면 되므로(아래 Run 참고) 만들지 않는다.
            if (!Application.isPlaying) return;
            var go = new GameObject("~UITween");
            DontDestroyOnLoad(go);
            I = go.AddComponent<UITween>();
        }
        static string K(UnityEngine.Object o, string ch) => o.GetInstanceID() + ":" + ch;
        static void Run(UnityEngine.Object o, string ch, IEnumerator co)
        {
            // 대상이 이미 파괴됐으면 시작하지 않는다. 러너는 DontDestroyOnLoad라 씬이 바뀌어도
            // 살아 있어서, 이 검사가 없으면 파괴된 오브젝트에 접근해 MissingReferenceException이 난다.
            if (!o) return;
            Ensure();
            if (I == null)
            {
                // 에디터(베이크) — 코루틴 러너가 없다. dur<=0인 트윈은 첫 스텝에서 최종값을
                // 확정하고 바로 끝나므로 한 번만 돌려주면 결과가 씬에 남는다.
                // 시간이 있는 트윈은 에디터에서 의미가 없어 첫 스텝까지만 진행한다.
                co.MoveNext();
                return;
            }
            var k = K(o, ch);
            if (I.running.TryGetValue(k, out var c) && c != null) I.StopCoroutine(c);
            I.running[k] = I.StartCoroutine(co);
        }
        public static void Stop(UnityEngine.Object o, string ch)
        {
            if (I == null) return;
            var k = K(o, ch);
            if (I.running.TryGetValue(k, out var c) && c != null) I.StopCoroutine(c);
            I.running.Remove(k);
        }

        // 이 러너는 DontDestroyOnLoad라 씬이 바뀌어도 살아남는다. 진행 중이던 트윈은
        // 대상이 씬과 함께 파괴되므로 전부 정리한다(코루틴 내부 검사와 이중 안전장치).
        // 람다로 구독하면 -= 할 때 다른 인스턴스라 해제가 안 된다. 반드시 이름 있는 메서드로.
        void OnEnable()  { SceneManager.sceneUnloaded += OnSceneUnloaded; }
        void OnDisable() { SceneManager.sceneUnloaded -= OnSceneUnloaded; }

        void OnSceneUnloaded(Scene s)
        {
            StopAllCoroutines();
            running.Clear();
        }

        public static void AnchorX(RectTransform rt, float to, float dur, Ease e) => Run(rt, "ax", Anchor(rt, to, dur, e, true));
        public static void AnchorY(RectTransform rt, float to, float dur, Ease e) => Run(rt, "ay", Anchor(rt, to, dur, e, false));
        static IEnumerator Anchor(RectTransform rt, float to, float dur, Ease e, bool x)
        {
            if (dur <= 0) { var p0 = rt.anchoredPosition; if (x) p0.x = to; else p0.y = to; rt.anchoredPosition = p0; yield break; }
            float t = 0; var s = rt.anchoredPosition; float from = x ? s.x : s.y;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Easing.E(e, t / dur);
                var p = rt.anchoredPosition;
                if (x) p.x = Mathf.LerpUnclamped(from, to, k); else p.y = Mathf.LerpUnclamped(from, to, k);
                rt.anchoredPosition = p; yield return null;
                if (!rt) yield break;   // 씬 전환 등으로 대상이 파괴되면 중단
            }
            var f = rt.anchoredPosition; if (x) f.x = to; else f.y = to; rt.anchoredPosition = f;
        }

        public static void Fade(CanvasGroup cg, float to, float dur, Ease e) => Run(cg, "fade", FadeCo(cg, to, dur, e));
        static IEnumerator FadeCo(CanvasGroup cg, float to, float dur, Ease e)
        {
            if (dur <= 0) { cg.alpha = to; yield break; }
            float t = 0, from = cg.alpha;
            while (t < dur) { t += Time.unscaledDeltaTime; cg.alpha = Mathf.LerpUnclamped(from, to, Easing.E(e, t / dur)); yield return null; if (!cg) yield break; }
            cg.alpha = to;
        }

        public static void Color(Graphic g, Color to, float dur, Ease e) => Run(g, "col", ColCo(g, to, dur, e));
        /// 진행 중인 색 트윈 중단. 트윈이 끝나며 덮어쓰는 걸 막아야 할 때 쓴다.
        public static void StopColor(Graphic g) => Stop(g, "col");
        static IEnumerator ColCo(Graphic g, Color to, float dur, Ease e)
        {
            if (dur <= 0) { g.color = to; yield break; }
            float t = 0; Color from = g.color;
            while (t < dur) { t += Time.unscaledDeltaTime; g.color = UnityEngine.Color.LerpUnclamped(from, to, Easing.E(e, t / dur)); yield return null; if (!g) yield break; }
            g.color = to;
        }

        public static void Scale(RectTransform rt, float to, float dur, Ease e) => Run(rt, "scl", ScaleCo(rt, to, dur, e));
        static IEnumerator ScaleCo(RectTransform rt, float to, float dur, Ease e)
        {
            if (dur <= 0) { rt.localScale = Vector3.one * to; yield break; }
            float t = 0, from = rt.localScale.x;
            while (t < dur) { t += Time.unscaledDeltaTime; rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, Easing.E(e, t / dur)); yield return null; if (!rt) yield break; }
            rt.localScale = Vector3.one * to;
        }
        // 1 -> 확대 -> 1. 이미 자리에 있는 요소를 "한 번 튀게" 해서 눈에 띄게 한다.
        //   (Pop 은 0.6에서 시작해 등장용이라, 상시 표시 중인 문구에 쓰면 확 쪼그라들었다 커진다)
        //   같은 "scl" 채널이라 연타하면 이전 것이 끊기고 처음부터 다시 튄다.
        public static void Punch(RectTransform rt, float peak = 1.14f, float dur = 0.34f)
            { Run(rt, "scl", PunchCo(rt, peak, dur)); }
        static IEnumerator PunchCo(RectTransform rt, float peak, float dur)
        {
            float up = dur * 0.35f, down = dur - up, t = 0;
            while (t < up)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(1f, peak, Easing.E(Ease.OutQuad, t / up));
                yield return null; if (!rt) yield break;
            }
            t = 0;
            while (t < down)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(peak, 1f, Easing.E(Ease.OutBack, t / down));
                yield return null; if (!rt) yield break;
            }
            rt.localScale = Vector3.one;
        }

        // 0.6 -> overshoot -> 1 팝
        public static void Pop(RectTransform rt) { Run(rt, "scl", PopCo(rt)); }
        static IEnumerator PopCo(RectTransform rt)
        {
            float dur = UIAnim.IconPop, t = 0;
            while (t < dur) { t += Time.unscaledDeltaTime; float k = Easing.E(Ease.OutBack, t / dur); rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, k); yield return null; if (!rt) yield break; }
            rt.localScale = Vector3.one;
        }

        public static void RotateZ(RectTransform rt, float to, float dur, Ease e) => Run(rt, "rot", RotCo(rt, to, dur, e));
        static IEnumerator RotCo(RectTransform rt, float to, float dur, Ease e)
        {
            if (dur <= 0) { rt.localEulerAngles = new Vector3(0, 0, to); yield break; }
            float t = 0, from = rt.localEulerAngles.z; if (from > 180) from -= 360;
            while (t < dur) { t += Time.unscaledDeltaTime; rt.localEulerAngles = new Vector3(0, 0, Mathf.LerpUnclamped(from, to, Easing.E(e, t / dur))); yield return null; if (!rt) yield break; }
            rt.localEulerAngles = new Vector3(0, 0, to);
        }

        public static void Run_Width(RectTransform rt, float to, float dur) => Run(rt, "w", WidthCo(rt, to, dur));
        static IEnumerator WidthCo(RectTransform rt, float to, float dur)
        {
            if (dur <= 0) { var s0 = rt.sizeDelta; s0.x = to; rt.sizeDelta = s0; yield break; }
            float t = 0, from = rt.sizeDelta.x;
            while (t < dur) { t += Time.unscaledDeltaTime; var s = rt.sizeDelta; s.x = Mathf.LerpUnclamped(from, to, Easing.E(Ease.OutQuad, t / dur)); rt.sizeDelta = s; yield return null; if (!rt) yield break; }
            var f = rt.sizeDelta; f.x = to; rt.sizeDelta = f;
        }

        public static void Pulse(CanvasGroup cg) => Run(cg, "pulse", PulseCo(cg));
        static IEnumerator PulseCo(CanvasGroup cg)
        {
            float half = UIAnim.Pulse * 0.5f, t = 0; bool down = true;
            while (cg)   // 대상이 파괴되면 즉시 종료. 무한 루프라 검사가 없으면 영영 안 끝난다.
            {
                t += Time.unscaledDeltaTime;
                float k = Easing.E(Ease.InOutSine, Mathf.Clamp01(t / half));
                cg.alpha = down ? Mathf.Lerp(1f, 0.35f, k) : Mathf.Lerp(0.35f, 1f, k);
                if (t >= half) { t = 0; down = !down; }
                yield return null;
            }
        }
    }
}

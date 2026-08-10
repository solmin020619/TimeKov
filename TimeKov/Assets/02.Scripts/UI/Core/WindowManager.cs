// =====================================================================
// WindowManager.cs
// 모든 UI 창의 단일 통제 지점.
//   - Open / Close 일원화
//   - ESC 키 디스패치 통합
//   - Time.timeScale, Cursor, 플레이어 입력 차단 단일 통제
//   - 레이어별 컨테이너 분리:
//       Window 레이어 = Set  (인벤·퀘스트 동시 OK)
//       Modal  레이어 = Stack (ESC 시 최근 모달부터 닫음)
//
// 사용 흐름:
//   1. 씬에 GO 하나 두고 WindowManager 부착, sortingSettings 연결
//   2. 각 UI는 IWindow 구현 (또는 어댑터로 감쌈)
//   3. 본체 OnEnable에서 WindowManager.I.Register(this),
//      OnDisable에서 WindowManager.I.Unregister(this)
//   4. 열고 닫기는 WindowManager.I.Open(id) / Close(id)
//   5. ESC는 한 곳에서 WindowManager.I.HandleEscape() 호출
//      (기존 매니저들의 ESC 직접 처리는 제거)
//
// 마이그레이션은 점진적으로:
//   기존 GameUIController, BuildManager 등은 동작 유지하면서
//   하나씩 어댑터를 통해 WindowManager로 합류.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TimeKov.UI
{
    public class WindowManager : MonoBehaviour
    {
        public static WindowManager I { get; private set; }

        [Header("Settings")]
        [Tooltip("Layer → SortingOrder 매핑 SO (필수)")]
        public WindowSortingSettings sortingSettings;

        [Tooltip("창 열기 시 동반/충돌 룰 SO (선택)")]
        public WindowOpenPolicy openPolicy;

        [Header("ESC 동작")]
        [Tooltip("ESC를 눌렀을 때 아무것도 안 열려있으면 열 기본 창 ID (비우면 비활성)")]
        public string defaultEscapeWindowId = "Settings";

        [Tooltip("WindowManager가 직접 ESC 입력을 듣고 디스패치할지 여부. " +
                 "false면 외부(GameUIController 등)에서 HandleEscape()를 호출해야 함. " +
                 "마이그레이션 중에는 false 권장.")]
        public bool listenEscapeKey = false;

        [Header("Debug")]
        [Tooltip("Open(string) 호출 시 미등록 ID에 대해 LogWarning 출력 (각 ID당 1번만).")]
        public bool logUnregisteredOpens = true;

        [Tooltip("F11 키로 WindowManager 상태 오버레이 토글. 등록된 창·열림 상태·timeScale·Cursor 표시.")]
        public bool enableDebugOverlay = true;

        bool _debugOverlayVisible;
        readonly HashSet<string> _warnedIds = new();

        // ── 컨테이너 ─────────────────────────────────────────────────
        readonly HashSet<IWindow> _openWindows = new();         // Window: 공존 가능
        readonly Stack<IWindow>   _modalStack  = new();         // Modal: LIFO
        readonly List<IWindow>    _openOrder   = new();         // Window 닫기 우선순위(최근 우선)
        readonly Dictionary<string, IWindow> _registry = new(); // ID로 조회

        public WindowSortingSettings SortingSettings => sortingSettings;

        public bool IsAnyModalOpen  => _modalStack.Count > 0;
        public bool IsAnyWindowOpen => IsAnyModalOpen || _openWindows.Count > 0;

        // ── 라이프사이클 ─────────────────────────────────────────────
        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void OnEnable()
        {
            // 씬의 모든 CanvasLayerAssigner를 강제 재적용
            // (Script Execution Order에 의존하지 않고 안정적으로 정렬)
            var assigners = FindObjectsByType<CanvasLayerAssigner>(FindObjectsSortMode.None);
            for (int i = 0; i < assigners.Length; i++)
                assigners[i]?.Apply();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Update()
        {
            // 사망 오버레이 중엔 ESC 무시(부활 화면 위로 설정 모달이 뜨는 것 방지).
            if (listenEscapeKey && Input.GetKeyDown(KeyCode.Escape) && !DeathOverlayUI.IsOpen)
                HandleEscape();
        }

        void OnGUI()
        {
            if (!enableDebugOverlay || !_debugOverlayVisible) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== WindowManager (F11 toggle) ===");
            sb.AppendLine($"timeScale={Time.timeScale:F2}  cursor.visible={Cursor.visible}  cursor.lockState={Cursor.lockState}");
            sb.AppendLine($"PlayerInput.IsBlocked={PlayerInputComponent.IsBlocked}");
            sb.AppendLine();
            sb.AppendLine($"Registered ({_registry.Count}): {string.Join(", ", _registry.Keys)}");
            sb.AppendLine();
            sb.Append($"Open Windows ({_openOrder.Count}): ");
            for (int i = 0; i < _openOrder.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_openOrder[i].WindowId);
            }
            sb.AppendLine();
            sb.Append($"Modal Stack ({_modalStack.Count}, top→bottom): ");
            foreach (var m in _modalStack)
                sb.Append(m.WindowId).Append(" ");

            GUI.Box(new Rect(10, 10, 700, 180), sb.ToString());
        }

        // ── 등록 / 해제 ──────────────────────────────────────────────
        public void Register(IWindow w)
        {
            if (w == null || string.IsNullOrEmpty(w.WindowId)) return;
            _registry[w.WindowId] = w;
        }

        public void Unregister(IWindow w)
        {
            if (w == null) return;
            if (IsOpen(w)) ForceCloseInternal(w);
            if (!string.IsNullOrEmpty(w.WindowId)
                && _registry.TryGetValue(w.WindowId, out var found)
                && found == w)
            {
                _registry.Remove(w.WindowId);
            }
        }

        public IWindow Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _registry.TryGetValue(id, out var w);
            return w;
        }

        // ── 상태 조회 ────────────────────────────────────────────────
        public bool IsOpen(IWindow w)
        {
            if (w == null) return false;
            if (w.Layer == UILayer.Modal) return _modalStack.Contains(w);
            return _openWindows.Contains(w);
        }

        public bool IsOpen(string id) => IsOpen(Find(id));

        // ── 열기 ─────────────────────────────────────────────────────
        public void Open(string id)
        {
            var w = Find(id);
            if (w == null)
            {
                // 같은 ID에 대해 한 번만 LogWarning (시끄러움 방지)
                if (logUnregisteredOpens && _warnedIds.Add(id))
                    Debug.LogWarning($"[WindowManager] Open 실패 — 등록되지 않은 ID '{id}'. 해당 어댑터 컴포넌트가 씬에 부착되어야 함.");
                return;
            }
            Open(w);
        }

        public void Open(IWindow w)
        {
            if (w == null || IsOpen(w)) return;

            // 정책: closeOthers 먼저 처리
            var rule = openPolicy != null ? openPolicy.FindRule(w.WindowId) : null;
            if (rule != null)
            {
                bool prev = SuppressPanelSfx;
                SuppressPanelSfx = true;   // 다른 창 열며 자동 닫히는 창의 닫기음은 억제(중복음 방지)
                try
                {
                    for (int i = 0; i < rule.closeOthers.Count; i++)
                    {
                        var other = Find(rule.closeOthers[i]);
                        if (other != null && IsOpen(other)) Close(other);
                    }
                }
                finally { SuppressPanelSfx = prev; }
            }

            if (w.Layer == UILayer.Modal)
                _modalStack.Push(w);
            else
            {
                _openWindows.Add(w);
                _openOrder.Add(w);
            }

            try { w.OnOpen(); }
            catch (System.Exception e) { Debug.LogException(e); }

            PlayWindowSfx(w.WindowId, true);   // 패널 열기음(창 id별)

            // 정책: alsoOpen 나중 처리 (자기 자신 다 켜진 후)
            if (rule != null)
            {
                for (int i = 0; i < rule.alsoOpen.Count; i++)
                {
                    var other = Find(rule.alsoOpen[i]);
                    if (other != null && !IsOpen(other)) Open(other);
                }
            }

            ApplyGlobalState();
        }

        // ── 닫기 ─────────────────────────────────────────────────────
        public void Close(string id) => Close(Find(id));

        public void Close(IWindow w)
        {
            if (w == null || !IsOpen(w)) return;

            if (w.Layer == UILayer.Modal)
            {
                if (_modalStack.Count > 0 && _modalStack.Peek() == w)
                {
                    _modalStack.Pop();
                }
                else
                {
                    // top이 아닌 모달 닫기 요청 — 스택 재구성
                    var temp = new List<IWindow>(_modalStack);
                    _modalStack.Clear();
                    // Stack은 Pop 순서가 반대 → temp[0]이 원래 top
                    for (int i = temp.Count - 1; i >= 0; i--)
                        if (temp[i] != w) _modalStack.Push(temp[i]);
                }
            }
            else
            {
                _openWindows.Remove(w);
                _openOrder.Remove(w);
            }

            try { w.OnClose(); }
            catch (System.Exception e) { Debug.LogException(e); }

            PlayWindowSfx(w.WindowId, false);   // 패널 닫기음(창 id별)

            ApplyGlobalState();
        }

        // 창 id별 열/닫 효과음. 지정 안 된 창은 무음(도감·인벤토리 등은 자체/미지정).
        //   스탯·전송기·수리는 열/닫 공용 1클립, 설정·창고는 열/닫 분리.
        // CloseAll / 자동 연쇄 닫힘 / 외부 일괄 닫기(GameUIController.CloseAll 등) 중엔 닫기음 억제.
        public static bool SuppressPanelSfx;

        private static void PlayWindowSfx(string id, bool opening)
        {
            if (SuppressPanelSfx) return;
            // 스탯·설정·창고 열닫음은 각 컨트롤러(GameUIController·InventoryUIController)에서 직접 재생하도록 이관.
            //   (이 씬에는 WindowManager 오브젝트가 없어 이 경로가 안 타므로, 씬 의존을 제거하고 컨트롤러 직결로 통일)
            //   WindowManager 가 다시 씬에 추가되어도 중복음이 안 나도록 여기서는 재생하지 않는다.
        }

        public void CloseAll(bool includeModals = true)
        {
            bool prev = SuppressPanelSfx;
            SuppressPanelSfx = true;   // 일괄 닫기 — 개별 닫기음 억제(사망/전환 시 우르르 방지)
            try
            {
                // 최근 열린 것부터
                for (int i = _openOrder.Count - 1; i >= 0; i--)
                    Close(_openOrder[i]);

                if (includeModals)
                {
                    while (_modalStack.Count > 0)
                        Close(_modalStack.Peek());
                }
            }
            finally { SuppressPanelSfx = prev; }
        }

        // Unregister 시 강제 정리 (OnClose 호출 없이 컨테이너에서만 제거)
        void ForceCloseInternal(IWindow w)
        {
            if (w.Layer == UILayer.Modal)
            {
                var temp = new List<IWindow>(_modalStack);
                _modalStack.Clear();
                for (int i = temp.Count - 1; i >= 0; i--)
                    if (temp[i] != w) _modalStack.Push(temp[i]);
            }
            else
            {
                _openWindows.Remove(w);
                _openOrder.Remove(w);
            }
            ApplyGlobalState();
        }

        // ── ESC 디스패치 ─────────────────────────────────────────────
        // 우선순위:
        //   1. Modal Stack top
        //   2. 가장 최근에 연 Window
        //   3. 기본 설정창 열기 (defaultEscapeWindowId가 등록되어 있을 때)
        //
        // 반환: true면 처리 완료, false면 처리할 게 없었음 (호출자가 폴백 가능).
        public bool HandleEscape()
        {
            if (_modalStack.Count > 0)
            {
                Close(_modalStack.Peek());
                return true;
            }

            if (_openOrder.Count > 0)
            {
                Close(_openOrder[_openOrder.Count - 1]);
                return true;
            }

            if (!string.IsNullOrEmpty(defaultEscapeWindowId))
            {
                var w = Find(defaultEscapeWindowId);
                if (w != null) { Open(w); return true; }
            }

            return false;
        }

        // ── 글로벌 상태 적용 ─────────────────────────────────────────
        // 열려있는 창들의 플래그를 OR해서 timeScale / Cursor / 입력 차단을 한 번에 결정.
        // 이 메서드 외에는 timeScale·Cursor를 만지지 않는 것이 원칙.
        void ApplyGlobalState()
        {
            bool pauseGame = false;
            bool lockInput = false;

            foreach (var w in _openWindows)
            {
                if (w.PausesGame)          pauseGame = true;
                if (w.LocksGameplayInput)  lockInput = true;
            }
            foreach (var w in _modalStack)
            {
                if (w.PausesGame)          pauseGame = true;
                if (w.LocksGameplayInput)  lockInput = true;
            }

            Time.timeScale = pauseGame ? 0f : 1f;

            // 게임이 멈추면 소리도 함께 '일시정지'한다(정지가 아니라 멈춤 — 재개하면 이어서 재생).
            //   AudioListener.pause 는 재생 위치를 유지한 채 전체 오디오를 멈춘다.
            //   ★UI 조작음은 계속 들려야 하므로, UI 사운드 소스만 ignoreListenerPause 로 예외 처리돼 있다
            //     (GameSfx 2D 소스 / UISoundManager). 그쪽을 건드릴 땐 이 예외를 같이 확인할 것.
            AudioListener.pause = pauseGame;

            // 기존 코드와 동일한 채널로 입력 차단
            PlayerInputComponent.IsBlocked = lockInput;

            Cursor.visible = lockInput;
            Cursor.lockState = lockInput ? CursorLockMode.None : CursorLockMode.Locked;
        }

        // ── 디버그 유틸 ──────────────────────────────────────────────
        public string DumpState()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[WindowManager] Open Windows: ");
            foreach (var w in _openOrder) sb.Append(w.WindowId).Append(' ');
            sb.Append("| Modals: ");
            foreach (var w in _modalStack) sb.Append(w.WindowId).Append(' ');
            sb.Append($"| timeScale={Time.timeScale} cursor={Cursor.visible}");
            return sb.ToString();
        }
    }
}

using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private Vector2 position = new Vector2(10f, 10f);

    private float  _elapsed;
    private int    _frames;
    private int    _fps;
    private int    _ping;

    private GUIStyle _style;

    // GC Alloc 0 — 문자열 미리 생성
    private readonly string[] _fpsLabels  = new string[301];
    private readonly string[] _pingLabels = new string[1001];

    private void Awake()
    {
        for (int i = 0; i <= 300;  i++) _fpsLabels[i]  = i + " FPS";
        for (int i = 0; i <= 1000; i++) _pingLabels[i] = i + " ping";
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        _frames++;

        if (_elapsed >= updateInterval)
        {
            _fps  = Mathf.Clamp(Mathf.RoundToInt(_frames / _elapsed), 0, 300);
            _ping = Mathf.Clamp(Mathf.RoundToInt(_elapsed / _frames * 1000f), 0, 1000);
            _elapsed = 0f;
            _frames  = 0;
        }
    }

    private void OnGUI()
    {
        if (_style == null)
        {
            _style           = new GUIStyle(GUI.skin.label);
            _style.fontSize  = 22;
            _style.fontStyle = FontStyle.Bold;
        }

        _style.normal.textColor = _fps >= 60 ? Color.green
                                : _fps >= 30 ? Color.yellow
                                :              Color.red;

        GUI.Label(new Rect(position.x, position.y,      160, 30), _fpsLabels[_fps],   _style);
        GUI.Label(new Rect(position.x, position.y + 28, 160, 30), _pingLabels[_ping], _style);
    }
}

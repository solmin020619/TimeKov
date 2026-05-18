using System.Collections.Generic;
using UnityEngine;

public class LootBoxScanner : MonoBehaviour
{
    [SerializeField] private DropPickupPanel panel;
    [SerializeField] private float showRange = 3.5f;

    [Tooltip("패널 기준점이 박스에서 얼마나 위에 있을지 (m)")]
    [SerializeField] private float worldHeightOffset = 0.5f;

    [Tooltip("패널을 박스 오른쪽으로 얼마나 띄울지 (화면 픽셀)")]
    [SerializeField] private float screenRightOffset = 40f;

    private Transform _player;
    private Camera _cam;
    private bool _showing;

    private readonly List<LootBox> _inRange = new List<LootBox>();
    private readonly List<LootBox> _shownBoxes = new List<LootBox>();
    private readonly List<(int itemId, int count)> _merged = new List<(int itemId, int count)>();

    void LateUpdate()
    {
        if (panel == null) return;

        Transform player = GetPlayer();
        if (player == null) return;

        LootBox nearest = ScanInRange(player.position);

        if (_inRange.Count > 0)
        {
            // 범위 안 박스 구성이 바뀌었을 때만 패널을 다시 그린다
            if (InRangeChanged())
            {
                panel.Show(BuildMergedContents());
                _shownBoxes.Clear();
                _shownBoxes.AddRange(_inRange);
            }
            UpdatePanelPosition(nearest);
            _showing = true;
        }
        else if (_showing)
        {
            panel.Hide();
            _shownBoxes.Clear();
            _showing = false;
        }
    }

    // showRange 안 박스를 _inRange 에 모으고 가장 가까운 박스를 반환
    private LootBox ScanInRange(Vector3 pos)
    {
        _inRange.Clear();
        LootBox nearest = null;
        float rangeSqr = showRange * showRange;
        float nearestSqr = float.MaxValue;

        List<LootBox> all = LootBox.All;
        for (int i = 0; i < all.Count; i++)
        {
            LootBox b = all[i];
            if (b == null) continue;

            float d = (b.transform.position - pos).sqrMagnitude;
            if (d > rangeSqr) continue;

            _inRange.Add(b);
            if (d < nearestSqr)
            {
                nearestSqr = d;
                nearest = b;
            }
        }
        return nearest;
    }

    // 이번 프레임 범위 안 박스 구성이 직전에 표시한 것과 다른지
    private bool InRangeChanged()
    {
        if (_inRange.Count != _shownBoxes.Count) return true;
        for (int i = 0; i < _inRange.Count; i++)
            if (_inRange[i] != _shownBoxes[i]) return true;
        return false;
    }

    // _inRange 박스들의 내용물을 itemId 별로 합친다
    private List<(int itemId, int count)> BuildMergedContents()
    {
        _merged.Clear();
        for (int i = 0; i < _inRange.Count; i++)
        {
            IReadOnlyList<(int itemId, int count)> contents = _inRange[i].Contents;
            for (int j = 0; j < contents.Count; j++)
            {
                int id = contents[j].itemId;
                int cnt = contents[j].count;

                int idx = -1;
                for (int k = 0; k < _merged.Count; k++)
                    if (_merged[k].itemId == id) { idx = k; break; }

                if (idx >= 0) _merged[idx] = (id, _merged[idx].count + cnt);
                else _merged.Add((id, cnt));
            }
        }
        return _merged;
    }

    // F 입력 시 LootBox.Interact 에서 호출 — showRange 안 박스를 전부 수집
    public void CollectAllInRange(Player player)
    {
        if (player == null) return;

        Vector3 pos = player.transform.position;
        float rangeSqr = showRange * showRange;

        List<LootBox> all = LootBox.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            LootBox b = all[i];
            if (b == null) continue;
            if ((b.transform.position - pos).sqrMagnitude > rangeSqr) continue;
            b.Collect(player);
        }
    }

    // 박스의 월드 위치를 화면 좌표로 변환해 패널을 그 자리로 옮긴다
    private void UpdatePanelPosition(LootBox box)
    {
        Camera cam = GetCamera();
        if (cam == null || box == null) return;

        Vector3 world = box.transform.position + Vector3.up * worldHeightOffset;
        Vector3 screen = cam.WorldToScreenPoint(world);

        // 카메라 뒤에 있으면 화면 밖으로 치운다
        if (screen.z < 0f)
        {
            panel.SetScreenPosition(new Vector3(-10000f, -10000f, 0f));
            return;
        }

        // 박스 오른쪽으로 살짝 띄운다
        screen.x += screenRightOffset;
        panel.SetScreenPosition(screen);
    }

    private Transform GetPlayer()
    {
        if (_player == null)
        {
            Player p = FindAnyObjectByType<Player>();
            if (p != null) _player = p.transform;
        }
        return _player;
    }

    private Camera GetCamera()
    {
        if (_cam == null) _cam = Camera.main;
        return _cam;
    }
}

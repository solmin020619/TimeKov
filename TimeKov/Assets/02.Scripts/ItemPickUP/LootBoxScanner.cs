using System.Collections.Generic;
using UnityEngine;

public class LootBoxScanner : MonoBehaviour
{
    [SerializeField] private DropPickupPanel panel;
    [SerializeField] private float showRange = 3.5f;

    private Transform _player;
    private LootBox _shownBox;
    private bool _showing;

    void Update()
    {
        if (panel == null) return;

        Transform player = GetPlayer();
        if (player == null) return;

        LootBox nearest = FindNearest(player.position);

        if (nearest != null)
        {
            if (!_showing || nearest != _shownBox)
            {
                panel.Show(nearest.Contents);
                _shownBox = nearest;
                _showing = true;
            }
        }
        else if (_showing)
        {
            panel.Hide();
            _shownBox = null;
            _showing = false;
        }
    }

    private LootBox FindNearest(Vector3 pos)
    {
        LootBox best = null;
        float bestSqr = showRange * showRange;

        List<LootBox> all = LootBox.All;
        for (int i = 0; i < all.Count; i++)
        {
            LootBox b = all[i];
            if (b == null) continue;

            float d = (b.transform.position - pos).sqrMagnitude;
            if (d <= bestSqr)
            {
                bestSqr = d;
                best = b;
            }
        }
        return best;
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
}

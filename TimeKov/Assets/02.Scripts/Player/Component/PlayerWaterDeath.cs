using UnityEngine;

// 플레이어가 Water 레이어에 닿으면 즉사.
// 물 콜라이더가 Trigger든 Solid든 잡도록 OnTriggerEnter/OnCollisionEnter 둘 다 처리.
// Player.Awake에서 자동 부착(수동 부착도 OK - 그때는 인스펙터에서 레이어명 조절 가능).
[RequireComponent(typeof(Player))]
public class PlayerWaterDeath : MonoBehaviour
{
    [Tooltip("이 레이어에 닿으면 사망. 기본 Water.")]
    [SerializeField] private string waterLayerName = "Water";

    private Player _player;
    private int _waterLayer;

    void Awake()
    {
        _player = GetComponent<Player>();
        _waterLayer = LayerMask.NameToLayer(waterLayerName);   // 레이어 없으면 -1 (오작동 방지)
    }

    void OnTriggerEnter(Collider other) => CheckWater(other.gameObject);
    void OnCollisionEnter(Collision col) => CheckWater(col.gameObject);

    private void CheckWater(GameObject go)
    {
        if (_waterLayer < 0) return;
        if (_player == null || _player.Stat == null || _player.Stat.IsDead) return;
        if (go.layer == _waterLayer) _player.Stat.Kill();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 스킬/평타/히트 VFX 오브젝트 풀.
// 기존: 시전/타격마다 Object.Instantiate + lifeTime 후 Destroy -> 시전 순간 hitch + GC 스파이크.
// 변경: 풀에서 꺼내 재사용(파티클 Clear+Play로 재시작), lifeTime 후 비활성으로 반납(파괴 X).
// VfxUtils.Spawn 이 전부 이 풀을 거친다. 런타임 lazy 싱글톤(씬 무관 유지).
//
// ★풀링 전제: 파티클이 스스로 파괴/비활성되면 풀이 깨지므로 생성 시 모든 PS의 StopAction을 None 으로 강제.
//   수명 관리는 이 풀이 lifeTime 타이머로 직접 한다.
public class VfxPool : MonoBehaviour
{
    private static VfxPool _instance;
    private static bool _quitting;

    public static VfxPool I
    {
        get
        {
            if (_quitting) return null;
            if (_instance == null)
            {
                var go = new GameObject("[VfxPool]");
                _instance = go.AddComponent<VfxPool>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    private class Item
    {
        public GameObject prefab;          // 어느 풀로 반납할지 키
        public GameObject go;              // 인스턴스
        public ParticleSystem[] roots;     // 최상위 PS만(자식은 withChildren로 함께 재생)
    }

    private readonly Dictionary<GameObject, Stack<Item>> _free = new();   // prefab -> 비활성 재고
    // 활성 인스턴스는 반납 코루틴이 Item을 직접 들고 있어 별도 추적 딕셔너리가 필요 없다
    // (예전 _active 딕셔너리는 시전자 파괴 시 stale 엔트리가 남는 누수가 있어 제거).

    /// <summary>풀에서 꺼내 배치/재생. lifeTime 후 자동 반납. VfxUtils.Spawn 전용.</summary>
    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent, float lifeTime)
    {
        if (prefab == null) return null;

        Item item = Rent(prefab);
        var tr = item.go.transform;
        tr.SetParent(parent, false);
        tr.SetPositionAndRotation(pos, rot);
        item.go.SetActive(true);
        Restart(item);

        if (lifeTime > 0f) StartCoroutine(ReturnAfter(item, lifeTime));
        return item.go;
    }

    /// <summary>로딩 시점에 미리 만들어 풀에 적재(첫 시전 셰이더/머티리얼 인스턴스화 hitch 방지).</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        var stack = GetStack(prefab);
        for (int i = 0; i < count; i++)
        {
            var item = Create(prefab);
            item.go.SetActive(false);
            item.go.transform.SetParent(transform, false);
            stack.Push(item);
        }
    }

    private Item Rent(GameObject prefab)
    {
        if (_free.TryGetValue(prefab, out var stack))
        {
            while (stack.Count > 0)
            {
                var it = stack.Pop();
                if (it != null && it.go != null) return it;
                // 파괴된 재고(부모 따라 사라진 경우 등)는 버리고 계속
            }
        }
        return Create(prefab);
    }

    private Item Create(GameObject prefab)
    {
        var go = Object.Instantiate(prefab);
        var all = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var main = all[i].main;
            main.stopAction = ParticleSystemStopAction.None;   // 풀링 위해 자가 파괴/비활성 차단
        }
        return new Item { prefab = prefab, go = go, roots = FindRoots(go.transform, all) };
    }

    // 자식 PS는 부모를 withChildren로 재생하면 같이 도므로, 최상위 PS만 추려 재시작 대상으로.
    private static ParticleSystem[] FindRoots(Transform instanceRoot, ParticleSystem[] all)
    {
        var roots = new List<ParticleSystem>();
        foreach (var ps in all)
        {
            if (ps == null) continue;
            bool hasParentPs = false;
            for (Transform p = ps.transform.parent; p != null; p = p.parent)
            {
                if (p.GetComponent<ParticleSystem>() != null) { hasParentPs = true; break; }
                if (p == instanceRoot) break;
            }
            if (!hasParentPs) roots.Add(ps);
        }
        return roots.ToArray();
    }

    private static void Restart(Item item)
    {
        if (item.roots == null) return;
        for (int i = 0; i < item.roots.Length; i++)
        {
            var ps = item.roots[i];
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private IEnumerator ReturnAfter(Item item, float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        Release(item);
    }

    private void Release(Item item)
    {
        // item.go 가 외부에서 파괴됐으면(부모 시전자 사망 등) 재고로 안 넣는다 -> 누수 없음(Item은 GC됨).
        if (item == null || item.go == null) return;
        item.go.SetActive(false);
        item.go.transform.SetParent(transform, false);
        GetStack(item.prefab).Push(item);
    }

    private Stack<Item> GetStack(GameObject prefab)
    {
        if (!_free.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Item>();
            _free[prefab] = stack;
        }
        return stack;
    }
}

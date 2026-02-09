using UnityEngine;

public class AnimatorDebugClipLogger : MonoBehaviour
{
    public Animator animator;
    public int layerIndex = 0;

    private string _last;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (animator == null) return;

        var clips = animator.GetCurrentAnimatorClipInfo(layerIndex);
        if (clips == null || clips.Length == 0) return;

        string s = "";
        for (int i = 0; i < clips.Length; i++)
        {
            var c = clips[i].clip;
            s += c ? c.name : "null";
            if (i < clips.Length - 1) s += ", ";
        }

        if (s == _last) return;
        _last = s;

        Debug.Log($"[ClipLogger] {gameObject.name} layer{layerIndex} => {s}");
    }
}

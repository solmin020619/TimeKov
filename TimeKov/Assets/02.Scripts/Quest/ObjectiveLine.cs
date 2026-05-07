using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveLine : MonoBehaviour
{
    [SerializeField] TMP_Text labelText;
    [SerializeField] Image checkmark;

    ObjectiveSO _o;

    public void Bind(ObjectiveSO o)
    {
        _o = o;
        _o.OnProgressChanged += OnProgress;
        _o.OnCompleted += OnDone;
        Refresh();
        if (checkmark) checkmark.enabled = false;
    }

    void OnProgress(float _) => Refresh();

    void OnDone(ObjectiveSO _)
    {
        if (checkmark) checkmark.enabled = true;
        if (labelText) { var c = labelText.color; c.a = 0.5f; labelText.color = c; }
    }

    void Refresh()
    {
        if (_o == null || labelText == null) return;
        labelText.text = _o.GetDisplayLabel();
    }

    void OnDestroy()
    {
        if (_o == null) return;
        _o.OnProgressChanged -= OnProgress;
        _o.OnCompleted -= OnDone;
    }
}

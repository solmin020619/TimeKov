using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class VattalusPlayerDetector : MonoBehaviour
{
    private bool _playerInsideCollider = false;
    public bool PlayerInsideCollider { get { return _playerInsideCollider; } }

    [Header("Event Callbacks")] //events for when the player enters/exits the bounds collider
    public UnityEvent OnPlayerEnter = new UnityEvent();
    public UnityEvent OnPlayerExit = new UnityEvent();

    private void OnTriggerEnter(Collider other)
    {
        _playerInsideCollider = true;
        //we can safely ignore collission that happen at the very beggining, this would break certain initializations further up the event chain
        if (Time.time <= 0.1f) return;

        if (other.GetComponent<VattalusFirstPersonCamera>())
        {
            if (OnPlayerEnter != null) OnPlayerEnter.Invoke();
        }
    }


    private void OnTriggerExit(Collider other)
    {
        _playerInsideCollider = false;
        if (other.GetComponent<VattalusFirstPersonCamera>())
        {
            if (OnPlayerExit != null) OnPlayerExit.Invoke();
        }
    }
}

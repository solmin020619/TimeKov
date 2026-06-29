using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴의 "게임 종료" 버튼. 클릭하면 바로 종료하지 않고 확인 모달("QuitConfirmModal",
/// 캔버스 바로 아래 — MainMenuButtonsBuilder.BuildQuitConfirmModal이 생성)을 띄운다.
/// 모달 참조도 이름으로 직접 찾아 쓰므로 onClick과 마찬가지로 인스펙터 와이어링이 필요 없다.
/// </summary>
[RequireComponent(typeof(Button))]
public class MainMenuQuitButton : MonoBehaviour
{
    private GameObject _confirmModal;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OpenConfirm);
        // transform.root 대신 가장 가까운 Canvas를 직접 찾는다 — 캔버스 GameObject 자체가
        // 항상 씬의 최상위 루트라고 가정하면 안 되기 때문(부모가 생기면 transform.root가 깨짐).
        var canvas = GetComponentInParent<Canvas>();
        _confirmModal = canvas != null ? canvas.transform.Find("QuitConfirmModal")?.gameObject : null;
    }

    private void OpenConfirm()
    {
        if (_confirmModal != null) _confirmModal.SetActive(true);
    }

    public void CancelQuit()
    {
        if (_confirmModal != null) _confirmModal.SetActive(false);
    }

    public void ConfirmQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

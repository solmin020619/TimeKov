// CreditsPanelController.cs
// 메인메뉴 "제작진" 패널 루트에 부착. MainMenuCreditsPanelBuilder가 생성.
using UnityEngine;

public class CreditsPanelController : MonoBehaviour
{
    public void OpenCredits() => gameObject.SetActive(true);
    public void CloseCredits() => gameObject.SetActive(false);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) CloseCredits();
    }
}

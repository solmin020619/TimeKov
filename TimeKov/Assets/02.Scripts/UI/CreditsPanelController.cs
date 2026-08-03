// CreditsPanelController.cs
// 메인메뉴 "제작진" 패널 루트에 부착. 패널은 MainMenu 씬에 실물로 있다
// (생성용 에디터 빌더는 08-03 에 팀 합의로 제거).
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

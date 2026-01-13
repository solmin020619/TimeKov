using UnityEngine;

// Base_Scene(허브)에서 출격 버튼이 호출할 함수 모음.
public class BaseActions : MonoBehaviour
{
    public void StartRaid1()
    {
        SceneLoader.Instance.LoadTo("Raid1_Scene");
    }

    public void StartRaid2()
    {
        SceneLoader.Instance.LoadTo("Raid2_Scene");
    }
}

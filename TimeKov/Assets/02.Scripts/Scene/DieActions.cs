using UnityEngine;

// Die_Scene에서 복귀 버튼 또는 자동 복귀가 호출할 함수.
public class DieActions : MonoBehaviour
{
    public void BackToBase()
    {
        SceneLoader.Instance.LoadTo("Base_Scene");
    }
}

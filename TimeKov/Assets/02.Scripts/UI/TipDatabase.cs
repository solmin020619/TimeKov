using UnityEngine;

[CreateAssetMenu(menuName = "Timekov/Tip Database", fileName = "TipDatabase")]
public class TipDatabase : ScriptableObject
{
    
    /// 로딩 화면에서 랜덤으로 출력될 팁 문장 리스트
    /// TextArea 속성으로 Inspector에서 여러 줄 입력이 가능해짐.
    /// (최소 2줄 ~ 최대 4줄 높이로 표시)
    [TextArea(2, 4)]
    public string[] tips;
}

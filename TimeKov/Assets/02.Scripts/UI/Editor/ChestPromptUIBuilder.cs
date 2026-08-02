using UnityEditor;

// [08-02] 상자 프롬프트 실물 생성기. 레이아웃은 설비 프롬프트와 동일해서 PromptPanelBuilder 공유.
public static class ChestPromptUIBuilder
{
    [MenuItem("Tools/TIMEKOV/상자 프롬프트 UI 생성")]
    public static void Build() => PromptPanelBuilder.BuildPrompt<ChestPromptUI>("ChestPrompt", "여는 중");
}

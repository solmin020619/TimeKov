using System.Collections.Generic;
using UnityEngine;

// 도감 튜토리얼 탭 데이터. 기존 영상 튜토리얼(TutorialVideoObjective)의 페이지들을
// 그대로 복붙해 담는다(영상 클립 + 제목 + 본문). 게임에서 넘긴 영상을 도감에서 다시 보게 함.
// Resources/Codex/CodexTutorialConfig 에 두면 자동 로드. populate 메뉴로 자동 채움.
[CreateAssetMenu(fileName = "CodexTutorialConfig", menuName = "TIMEKOV/도감/튜토리얼 영상 설정")]
public class CodexTutorialConfig : ScriptableObject
{
    // VideoTutorialPage = 기존 영상 튜토 페이지 클래스(clip/title/body) 재사용.
    public List<VideoTutorialPage> pages = new List<VideoTutorialPage>();
}

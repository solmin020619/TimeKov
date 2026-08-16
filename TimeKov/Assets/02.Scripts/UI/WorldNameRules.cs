// =====================================================================
// WorldNameRules.cs
// 월드 이름에 대한 규칙을 한 곳에 모은다. 입력창(WorldSelectUI)·목록 표시
// (WorldSelectRow)·삭제 확인창(MenuConfirmDialog)이 전부 같은 판정을 쓰게 하려는 것.
//
// [왜 따로 두는가]
//   - 이름은 meta.json 에 그대로 굳고 목록에도 그대로 그려진다. 한 번 이상한 문자가
//     들어가면 게임 안에서는 고칠 방법이 없다(이름 변경 기능이 없다).
//   - TMP 는 꺾쇠(<b> 등)를 리치텍스트 태그로 해석한다. 이름에 꺾쇠가 있으면 목록에서
//     그 부분이 통째로 사라지거나 서식이 옆줄까지 번진다.
//   - 중복은 "눈으로 같아 보이면 같다"여야 한다. 앞뒤 공백이나 대소문자 차이만으로
//     구분되는 월드가 두 개 생기면 목록에서 어느 쪽이 어느 쪽인지 알 수 없다.
// =====================================================================

using System.Collections.Generic;
using System.Text;

public static class WorldNameRules
{
    /// <summary>표시 영역(입력칸 460px)과 목록 한 줄에 무리 없이 들어가는 길이.</summary>
    public const int MaxLength = 20;

    const char ZeroWidthSpace = '\u200B';   // TMP_InputField 가 빈 입력에도 넣어두는 문자
    const char ByteOrderMark  = '\uFEFF';

    /// <summary>저장·비교에 쓸 최종 형태로 다듬는다. 입력 도중에 호출해도 된다.</summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new StringBuilder(raw.Length);
        bool lastWasSpace = false;
        foreach (char c in raw)
        {
            if (c == '<' || c == '>') continue;                       // TMP 리치텍스트로 해석되는 문자
            if (c == ZeroWidthSpace || c == ByteOrderMark) continue;  // 보이지 않지만 길이는 차지한다
            if (char.IsControl(c)) continue;                          // 줄바꿈·탭 등

            if (char.IsWhiteSpace(c))
            {
                if (sb.Length == 0 || lastWasSpace) continue;         // 앞 공백 / 연속 공백은 하나로
                sb.Append(' ');
                lastWasSpace = true;
                continue;
            }
            sb.Append(c);
            lastWasSpace = false;
        }

        string s = sb.ToString().TrimEnd();
        if (s.Length > MaxLength)
        {
            int cut = MaxLength;
            // 이모지처럼 char 두 개가 한 글자인 경우, 그 사이를 자르면 깨진 글자가 남는다.
            if (char.IsHighSurrogate(s[cut - 1])) cut--;
            s = s.Substring(0, cut).TrimEnd();
        }
        return s;
    }

    /// <summary>중복 비교용 키. 대소문자·군더더기 공백을 무시한다.</summary>
    public static string Normalize(string name) => Sanitize(name).ToLowerInvariant();

    /// <summary>이미 쓰이고 있는 이름인가. taken 은 Normalize 를 거친 키 집합이어야 한다.</summary>
    public static bool IsTaken(ICollection<string> taken, string name)
    {
        if (taken == null || taken.Count == 0) return false;
        string key = Normalize(name);
        return !string.IsNullOrEmpty(key) && taken.Contains(key);
    }

    /// <summary>TMP 에 그려도 안전한 형태. Sanitize 가 막기 전에 만들어진 옛 세이브에는
    /// 꺾쇠가 남아 있을 수 있어, 그리는 쪽에서도 한 번 더 거른다. 지우지 않고 비슷하게
    /// 생긴 홑화살괄호로 바꿔 이름이 달라 보이지 않게 한다.</summary>
    public static string Display(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        if (name.IndexOf('<') < 0 && name.IndexOf('>') < 0) return name;
        return name.Replace('<', '‹').Replace('>', '›');
    }
}

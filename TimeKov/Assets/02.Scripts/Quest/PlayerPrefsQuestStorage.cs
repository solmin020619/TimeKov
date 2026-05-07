using UnityEngine;

public class PlayerPrefsQuestStorage : IQuestSaveStorage
{
    readonly string _prefix;
    public PlayerPrefsQuestStorage(string prefix) { _prefix = prefix; }

    string K(string s) => $"{_prefix}.{s}";

    public int GetCategoryIndex(string id, int def = 0) => PlayerPrefs.GetInt(K($"cat.{id}"), def);
    public void SetCategoryIndex(string id, int v) => PlayerPrefs.SetInt(K($"cat.{id}"), v);
    public void Flush() => PlayerPrefs.Save();
}

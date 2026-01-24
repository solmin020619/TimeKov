using UnityEngine;
using System.Collections.Generic;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance;

    public List<PuzzleStatue> statues;
    public GameObject bossObject;
    public GameObject completeEffect;

    private bool isSolved = false;

    void Awake()
    {
        Instance = this;
        if (bossObject) bossObject.SetActive(false);
    }

    public void CheckPuzzle()
    {
        if (isSolved) return;

        foreach (var statue in statues)
        {
            if (!statue.IsCorrect()) return;
        }

        PuzzleSolved();
    }

    void PuzzleSolved()
    {
        isSolved = true;

        if (completeEffect) completeEffect.SetActive(true);

        if (bossObject)
        {
            bossObject.SetActive(true);
        }
    }
}
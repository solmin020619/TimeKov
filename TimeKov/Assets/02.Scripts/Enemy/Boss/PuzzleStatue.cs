using UnityEngine;

public class PuzzleStatue : MonoBehaviour
{
    [Header("Settings")]
    public int currentDirIndex = 0;
    public int correctDirIndex = 0;

    public void Interact()
    {
        currentDirIndex = (currentDirIndex + 1) % 4;
        transform.rotation = Quaternion.Euler(0, currentDirIndex * 90f, 0);
        PuzzleManager.Instance.CheckPuzzle();
    }

    public bool IsCorrect()
    {
        return currentDirIndex == correctDirIndex;
    }
}
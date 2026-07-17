using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecallCharacter02 : MonoBehaviour
{
    [SerializeField] private Transform originalPosition;
    [SerializeField] private float recallTime = 2f;
    [SerializeField] private GameObject[] recallParticles;
    [SerializeField] private GameObject[] spawnParticles;
    [SerializeField] private float recallParticlesDestroyTime = 5f;
    [SerializeField] private float spawnParticlesDestroyTime = 2f;

    private bool isRecalling = false;
    private GameObject recallSystem;
    private GameObject spawnSystem;
    private Coroutine recallCoroutine;
    private int currentIndex = 0;

    void Update()
    {
        CheckInput();
    }

    private void CheckInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isRecalling)
        {
            recallCoroutine = StartCoroutine(Recall());
        }

        // Check for other input while recalling
        if (isRecalling && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)))
        {
            StopRecall();
        }

        if (Input.GetKeyDown(KeyCode.Q) && !isRecalling)
        {
            currentIndex--;
            if (currentIndex < 0)
            {
                currentIndex = recallParticles.Length - 1;
            }
        }
        else if (Input.GetKeyDown(KeyCode.E) && !isRecalling)
        {
            currentIndex++;
            if (currentIndex >= recallParticles.Length)
            {
                currentIndex = 0;
            }
        }
    }

    IEnumerator Recall()
    {
        isRecalling = true;

        Transform myTransform = transform;
        Vector3 startPosition = myTransform.position;

        recallSystem = Instantiate(recallParticles[currentIndex], myTransform.position, Quaternion.identity);
        Destroy(recallSystem, recallParticlesDestroyTime);

        yield return new WaitForSeconds(recallTime);

        myTransform.position = originalPosition.position;

        spawnSystem = Instantiate(spawnParticles[currentIndex], myTransform.position, Quaternion.identity);
        Destroy(spawnSystem, spawnParticlesDestroyTime);

        isRecalling = false;
    }

    // Method to stop recalling and destroy particle systems if they exist
    private void StopRecall()
    {
        if (recallSystem != null)
        {
            Destroy(recallSystem);
        }

        if (spawnSystem != null)
        {
            Destroy(spawnSystem);
        }

        isRecalling = false;

        if (recallCoroutine != null)
        {
            StopCoroutine(recallCoroutine);
        }
    }
}

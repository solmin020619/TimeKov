using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class TitleManager : MonoBehaviour
{
    public string nextSceneName = "GameScene";
    public TextMeshProUGUI startText;
    public float blinkSpeed = 2f;

    [Header("���� ����")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    private bool _isStarting = false;

    private void Update()
    {
        if (startText != null)
        {
            Color c = startText.color;
            c.a = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
            startText.color = c;
        }

        // UI 버튼(게임 종료 등) 위를 클릭한 경우에만 시작을 막는다.
        if (!_isStarting && Input.anyKeyDown)
        {
            bool over = IsPointerOverInteractiveUI();
            if (!over)
            {
                _isStarting = true;
                StartCoroutine(StartGameRoutine());
            }
        }
    }

    // 기존엔 IsPointerOverGameObject()로 '아무 UI'나 위에 있으면 막았다.
    // 그런데 풀스크린 배경/로고/페이드 같은 장식 Image도 Raycast Target이면 화면 전체를 덮어
    // 어디를 눌러도 시작이 안 되던 문제 -> 실제 상호작용 UI(Selectable=버튼)만 차단하게 변경.
    private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
    private static bool IsPointerOverInteractiveUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;

        var data = new PointerEventData(es) { position = Input.mousePosition };
        _uiHits.Clear();
        es.RaycastAll(data, _uiHits);
        for (int i = 0; i < _uiHits.Count; i++)
        {
            var go = _uiHits[i].gameObject;
            if (go == null) continue;
            var sel = go.GetComponentInParent<Selectable>();
            if (sel != null && sel.interactable)
                return true;   // 버튼 등 위 = 시작 차단
        }
        return false;
    }

    private IEnumerator StartGameRoutine()
    {
        if (startText != null)
        {
            startText.color = new Color(1, 1, 1, 1);
            startText.text = "�ε� ��...";
        }

        // 메인메뉴가 timeScale=0(정지)이면 WaitForSeconds(스케일타임)는 영원히 안 끝나 LoadScene에 도달 못함.
        // -> Realtime 으로 대기해야 함. (좌클릭해도 안 넘어가던 진짜 원인)
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
            yield return new WaitForSecondsRealtime(clickSound.length);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Time.timeScale = 1f;   // 게임 시작 전 시간 정상화(메뉴에서 멈춰 있었을 수 있음)
        SceneManager.LoadScene(nextSceneName);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Turn_Move : MonoBehaviour {
	public int TurnX;
	public int TurnY;
	public int TurnZ;

	public int MoveX;
	public int MoveY;
	public int MoveZ;

	public bool World;

	// ── 기믹 연동 (스위치로 켜기 + 서서히 가속) ─────────────────────────────
	[Header("기믹 연동")]
	[Tooltip("체크: 처음부터 계속 움직인다(기존 동작). 해제: Activate() 를 부르기 전까지 멈춰 있다(스위치로 켬).")]
	public bool autoStart = true;
	[Tooltip("활성화 후 원래 속도까지 서서히 빨라지는 시간(초). 0 이면 즉시 원속도.")]
	public float rampUpTime = 2f;

	private bool  _running;
	private float _rampT;      // 0 → rampUpTime 로 증가

	void Start () {
		_running = autoStart;
		_rampT   = autoStart ? rampUpTime : 0f;   // 자동 시작이면 처음부터 원속도(램프 생략)
	}

	// 스위치 등이 호출: 이제부터 움직이기 시작(천천히 가속). 이미 돌고 있으면 무시.
	public void Activate()
	{
		if (_running) return;
		_running = true;
		_rampT   = 0f;   // 0속도에서 서서히
	}

	// 멈춤(토글 스위치를 다시 끌 때 등).
	public void Deactivate()
	{
		_running = false;
	}

	// Update is called once per frame
	void Update () {
		if (!_running) return;

		// 0 → 1 로 서서히 오르는 속도 배율. SmoothStep 이라 '천천히 시작 → 점점 빨라져' 원속도에 안착.
		float factor = 1f;
		if (rampUpTime > 0f && _rampT < rampUpTime)
		{
			_rampT += Time.deltaTime;
			factor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_rampT / rampUpTime));
		}

		float dt = Time.deltaTime * factor;
		Space space = World ? Space.World : Space.Self;
		transform.Rotate(TurnX * dt, TurnY * dt, TurnZ * dt, space);
		transform.Translate(MoveX * dt, MoveY * dt, MoveZ * dt, space);
	}
}

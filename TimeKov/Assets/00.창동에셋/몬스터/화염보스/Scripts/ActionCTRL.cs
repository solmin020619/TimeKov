using UnityEngine;
using System.Collections;

public class ActionCTRL : MonoBehaviour {


	public float rotateSpeed = 1.0f;

	[System.Serializable]
	public class actControlParam
	{
		public KeyCode key = new KeyCode();
		public string paramName;
	}
	public actControlParam[] actionPlayKeys;


	[System.Serializable]
	public class eyesControlParam
	{
		public KeyCode key = new KeyCode();
		public string paramName;
	}
	public eyesControlParam[] eyesToggleKeys;


	[System.Serializable]
	public class mouseControlParam
	{
		public KeyCode key = new KeyCode();
		public string paramName;
	}
	public mouseControlParam[] mouseToggleKeys;


	private Animator curAnimator;


	// Use this for initialization
	void Start () 
	{
		if ( null != this.gameObject.GetComponent<Animator>() )
		{
			curAnimator = this.gameObject.GetComponent<Animator> ();;
		}
	}

	// Update is called once per frame
	void Update () 
	{
		if ( curAnimator != null )
		{	
			for (int i = 0; i < actionPlayKeys.Length; i++) 
			{
				if ( Input.GetKeyDown( actionPlayKeys[i].key ) )
				{
					curAnimator.SetBool (actionPlayKeys [i].paramName, true);
				}

				if (Input.GetKeyUp(actionPlayKeys [i].key)) 
				{
					curAnimator.SetBool (actionPlayKeys [i].paramName, false);
				}
			}

			for (int i = 0; i < eyesToggleKeys.Length; i++) 
			{
				if ( Input.GetKeyDown( eyesToggleKeys[i].key ) )
				{
					if (curAnimator.GetBool (eyesToggleKeys [i].paramName) == false) 
					{
						curAnimator.SetBool (eyesToggleKeys [i].paramName, true);
					}
					else if (curAnimator.GetBool (eyesToggleKeys [i].paramName) == true) 
					{
						curAnimator.SetBool (eyesToggleKeys [i].paramName, false);
					}

					for (int j = 0; j < eyesToggleKeys.Length; j++) 
					{
						if (j != i) 
						{
							curAnimator.SetBool( eyesToggleKeys[j].paramName, false);
						}
					}
				}
			}

			for (int i = 0; i < mouseToggleKeys.Length; i++) 
			{
				if ( Input.GetKeyDown( mouseToggleKeys[i].key ) )
				{
					if (curAnimator.GetBool (mouseToggleKeys [i].paramName) == false) 
					{
						curAnimator.SetBool (mouseToggleKeys [i].paramName, true);
					}
					else if (curAnimator.GetBool (mouseToggleKeys [i].paramName) == true) 
					{
						curAnimator.SetBool (mouseToggleKeys [i].paramName, false);
					}

					for (int j = 0; j < mouseToggleKeys.Length; j++) 
					{
						if (j != i) 
						{
							curAnimator.SetBool( mouseToggleKeys[j].paramName, false);
						}
					}
				}
			}
		}

		if ( Input.GetKey(KeyCode.LeftArrow))
		{
			transform.Rotate( 0, Time.deltaTime * (rotateSpeed), 0);
		}

		if (Input.GetKey(KeyCode.RightArrow))
		{
			transform.Rotate(0, Time.deltaTime * (-rotateSpeed), 0);
		}
	}

	bool CheckAniClip ( string clipname )
	{	
		if (this.GetComponent<Animation> ().GetClip (clipname) == null) 
		{
			return false;
		} 
		else if (this.GetComponent<Animation> ().GetClip (clipname) != null) 
		{
			return true;
		}

		return false;
	}
}

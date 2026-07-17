using UnityEngine;
using System.Collections;

public class uv_move : MonoBehaviour {


    public Vector2[] uvSpeedSet;
	//public Vector2 uvSpeed;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {

        for (int i = 0; i < uvSpeedSet.Length; i++)
        {
            //Vector2 newUVset = this.gameObject.GetComponent<Renderer>().material.mainTextureOffset + (uvSpeed * Time.deltaTime);
            //newUVset = this.gameObject.GetComponent<Renderer>().materials[i].GetTextureOffset("_BumpMap") + (uvSpeedSet[i] * Time.deltaTime);
            //this.gameObject.GetComponent<Renderer>().materials[i].SetTextureOffset("_BumpMap", newUVset);

            Vector2 newUV = this.gameObject.GetComponent<Renderer>().materials[i].mainTextureOffset + (uvSpeedSet[i] * Time.deltaTime);

            this.gameObject.GetComponent<Renderer>().materials[i].mainTextureOffset = newUV;
        }

		//Vector2 newUV = this.gameObject.GetComponent<Renderer> ().material.mainTextureOffset + (uvSpeed * Time.deltaTime);

		//this.gameObject.GetComponent<Renderer> ().material.mainTextureOffset = newUV;

	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    TODO
        
*/

public class DecentScaleBLEMonoBehavior : MonoBehaviour
{
	void Awake()
	{
		Debug.Log("Initialized");
	}

	[SerializeField]
	public DecentScaleBLE decentScale;

	public void Connect()
	{
		decentScale.Connect();
	}

	// Start is called before the first frame update
	void Start()
	{
		decentScale.Start();
	}

	// Update is called once per frame
	void Update()
	{
		decentScale.Update();
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeskBehaviourScript : MonoBehaviour
{
	private TMPro.TMP_Text loggerText;
	private GameObject marker;
	private Vector3 markerPosition;
	private EyeInteractable eyeInteractable;

	// Start is called before the first frame update
	void Start()
	{
		loggerText = GameObject.Find("logger").GetComponent<TMPro.TMP_Text>(); ;
		marker = GameObject.Find("marker");
		eyeInteractable = GetComponent<EyeInteractable>();
	}

	// Update is called once per frame
	void Update()
	{
		if (eyeInteractable.IsHovered)
		{
			//loggerText.text = eyeInteractable.hitPoint.ToString();
			//loggerText.text = marker.transform.position.ToString();
			markerPosition.Set(eyeInteractable.hitPoint.x, eyeInteractable.hitPoint.y, eyeInteractable.hitPoint.z);
			marker.transform.position = markerPosition;
		}
	}
}

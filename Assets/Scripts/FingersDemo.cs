using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FingersDemo : MonoBehaviour
{
	public TapStrapBLEMonoBehavior tapStrapBLEMonoBehavior;

	[SerializeField]
	private Material DefaultMaterial;
	[SerializeField]
	private Material TapMaterial;

	private Transform[] children = new Transform[5];
	private MeshRenderer[] meshRenderers = new MeshRenderer[5];

	// Start is called before the first frame update
	void Start()
	{
		tapStrapBLEMonoBehavior.tapStrap.tapEvents.accelerometer.AddListener(OnAccelerometerData);
		tapStrapBLEMonoBehavior.tapStrap.tapEvents.tapData.AddListener(OnTapData);

		for (int i = 0; i < transform.childCount; i++)
		{
			children[i] = transform.GetChild(i);
			meshRenderers[i] = children[i].GetComponent<MeshRenderer>();
		}
	}

	bool shouldUpdateFingerRotation = false;

	// Update is called once per frame
	void Update()
	{
		if (shouldUpdateFingerRotation)
		{
			updateFingerRotation();
			shouldUpdateFingerRotation = false;
		}

		if (!didClearTaps && Time.time - latestTapDataTime > 0.3)
		{
			didClearTaps = true;
			for (int i = 0; i < 5; i++)
			{
				MeshRenderer meshRenderer = meshRenderers[i];
				meshRenderer.material = DefaultMaterial;
			}
		}
	}

	Vector3[] vectors = new Vector3[5];
	public void updateFingerRotation()
	{
		for (int i = 0; i < 5; i++)
		{
			Transform child = children[i];
			Vector3 vector = vectors[i];
			Vector3 aVector = tapStrapBLEMonoBehavior.tapStrap.accelerometerData[i];
			vector.Set(aVector.x, aVector.y, aVector.z);
			vector.Normalize();
			vector *= 10;
			vector += child.position;
			child.LookAt(vector);
		}
	}

	public void OnAccelerometerData()
	{
		shouldUpdateFingerRotation = true;
	}

	private float latestTapDataTime;
	private bool didClearTaps = true;
	public void OnTapData()
	{
		for (int i = 0; i < 5; i++)
		{
			MeshRenderer meshRenderer = meshRenderers[i];
			meshRenderer.material = tapStrapBLEMonoBehavior.tapStrap.tapData[(TapStrap.TapDataEnumeration)i] ? TapMaterial : DefaultMaterial;
		}

		latestTapDataTime = Time.time;
		didClearTaps = false;
	}
}

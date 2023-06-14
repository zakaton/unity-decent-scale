using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[RequireComponent(typeof(LineRenderer))]
public class EyeTrackingRay : MonoBehaviour
{
	[SerializeField]
	private float rayDistance = 1.0f;

	[SerializeField]
	private float rayWidth = 0.01f;

	[SerializeField]
	private LayerMask layersToInclude;

	[SerializeField]
	private Color rayColorDefaultState = Color.yellow;
	[SerializeField]
	private Color rayColorHoverState = Color.red;

	private LineRenderer lineRenderer;

	private List<EyeInteractable> eyeInteractables = new();

	[SerializeField]
	public UnityEvent<EyeInteractable> OnObjectHoverUpdate;

	// Start is called before the first frame update
	void Start()
	{
		lineRenderer = GetComponent<LineRenderer>();
		SetupRay();
	}

	void SetupRay()
	{
		lineRenderer.useWorldSpace = false;
		lineRenderer.positionCount = 2;
		lineRenderer.startWidth = rayWidth;
		lineRenderer.endWidth = 2;
		lineRenderer.startColor = rayColorDefaultState;
		lineRenderer.endColor = rayColorDefaultState;
		lineRenderer.SetPosition(0, transform.position);
		lineRenderer.SetPosition(1, new Vector3(transform.position.x, transform.position.y, transform.position.z + rayDistance));
	}

	private void FixedUpdate()
	{
		RaycastHit[] hits;

		Vector3 raycastDirection = transform.TransformDirection(Vector3.forward) * rayDistance;

		hits = Physics.RaycastAll(transform.position, raycastDirection, 50.0F, layersToInclude);
		if (hits.Length > 0)
		{
			UnSelect();
			for (int i = 0; i < hits.Length; i++)
			{
				RaycastHit hit = hits[i];

				lineRenderer.startColor = rayColorHoverState;
				lineRenderer.endColor = rayColorHoverState;

				var eyeInteractable = hit.transform.GetComponent<EyeInteractable>();
				if (eyeInteractable && !eyeInteractable.ShouldIgnore)
				{
					eyeInteractables.Add(eyeInteractable);
					eyeInteractable.IsHovered = true;
					eyeInteractable.hitPoint.Set(hit.point.x, hit.point.y, hit.point.z);
					OnObjectHoverUpdate.Invoke(eyeInteractable);
				}
			}
		}
		else
		{
			lineRenderer.startColor = rayColorDefaultState;
			lineRenderer.endColor = rayColorDefaultState;
			UnSelect(true);
		}
	}

	void UnSelect(bool clear = false)
	{
		foreach (var eyeInteractable in eyeInteractables)
		{
			if (eyeInteractable.deleted)
			{
				eyeInteractables.Remove(eyeInteractable);
			}
			else
			{
				eyeInteractable.IsHovered = false;
				OnObjectHoverUpdate.Invoke(eyeInteractable);
			}
		}
		if (clear)
		{
			eyeInteractables.Clear();
		}
	}

	// Update is called once per frame
	void Update()
	{

	}
}
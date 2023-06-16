using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.GraphicsBuffer;

public class SelectableObjectBehavior : MonoBehaviour
{
	[SerializeField]
	private EyeTrackingRay[] eyeTrackingRays;
	private List<EyeInteractable> eyeInteractables = new();
	private EyeInteractable selectedEyeInteractable = null;
	private EyeInteractable hoveredButton = null;
	private float selectedScale = 1.1F;

	[SerializeField]
	private TMPro.TMP_Text loggerText;

	private GameObject marker;

	[SerializeField]
	private GameObject mainMenu;
	[SerializeField]
	private GameObject recipeMenu;

	[SerializeField]
	private Camera _camera;

	private Vector3 markerPosition;

	public enum Mode
	{
		NONE,
		SELECTING_OBJECT,
		SELECTED_OBJECT,
		MOVING_OBJECT,
		ROTATING_OBJECT,
	}
	private Mode _mode = Mode.NONE;
	public Mode mode
	{
		get
		{
			return _mode;
		}
		set
		{
			if (_mode != value)
			{
				_mode = value;
				loggerText.text = String.Format("Mode: {0}", _mode.ToString());
				OnModeUpdate();
			}
		}
	}

	public enum ObjectType
	{
		cube,
		sphere,
		capsule,
		cylinder
	}
	[Serializable]
	public struct ObjectTypeStruct
	{
		public ObjectType type;
		public GameObject prefab;
	}
	[SerializeField]
	private ObjectTypeStruct[] objectTypeStructs;
	private Dictionary<ObjectType, GameObject> objectTypes = new();

	// Start is called before the first frame update
	void Start()
	{
		marker = GameObject.Find("marker");
		foreach (var eyeTrackingRay in eyeTrackingRays)
		{
			eyeTrackingRay.OnObjectHoverUpdate.AddListener(OnObjectHoverUpdate);
		}
		mode = Mode.NONE;

		for (int i = 0; i < objectTypeStructs.Length; i++)
		{
			var objectTypeStruct = objectTypeStructs[i];
			objectTypes[objectTypeStruct.type] = objectTypeStruct.prefab;
		}
	}

	private bool IsWaitingForObjectToInstantiate = false;

	// Update is called once per frame
	void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			OnPointerTap();
		}
		if (Input.GetMouseButtonDown(1))
		{
			OnMiddleTap();
		}

		if (IsWaitingForObjectToInstantiate && selectedEyeInteractable != null && selectedEyeInteractable.initialized)
		{
			//loggerText.text = "instantiated!";
			IsWaitingForObjectToInstantiate = false;
		}

		if (!IsWaitingForObjectToInstantiate && selectedEyeInteractable != null)
		{
			var parent = selectedEyeInteractable.transform.parent;
			switch (mode)
			{
				case Mode.MOVING_OBJECT:
					parent.position = marker.transform.position;
					break;
				case Mode.ROTATING_OBJECT:
					var vector3 = (parent.position - marker.transform.position);
					var angle = (float)-Math.Atan2(vector3.z, vector3.x) * Mathf.Rad2Deg;
					//loggerText.text = angle.ToString();
					var rotation = Quaternion.Euler(0, angle, 0);
					parent.rotation = rotation;
					break;
			}
		}
	}

	private void OnObjectHoverUpdate(EyeInteractable eyeInteractable)
	{
		if (eyeInteractable.IsSelectable)
		{
			if (!eyeInteractables.Contains(eyeInteractable))
			{
				eyeInteractables.Add(eyeInteractable);
			}

			UpdateObjectHighlight(eyeInteractable);
		}

		if (eyeInteractable.IsButton)
		{
			hoveredButton = eyeInteractable;
		}
		else
		{
			if (eyeInteractable == hoveredButton)
			{
				hoveredButton = null;
			}
		}
	}

	private void UpdateObjectHighlight(EyeInteractable eyeInteractable)
	{
		if (!eyeInteractable.initialized)
		{
			return;
		}

		var isLarge = eyeInteractable.IsHovered;
		if (isLarge != eyeInteractable.isLarge)
		{
			eyeInteractable.isLarge = isLarge;
			var transform = eyeInteractable.transform.parent;
			if (eyeInteractable.isLarge && mode == Mode.SELECTING_OBJECT)
			{
				eyeInteractable._scale = transform.localScale;
				transform.localScale *= selectedScale;
			}
			else
			{
				transform.localScale = eyeInteractable._scale;
			}
		}

	}
	private void UpdateObjectHighlights()
	{
		foreach (var eyeInteractable in eyeInteractables)
		{
			UpdateObjectHighlight(eyeInteractable);
		}
	}

	private void SelectObject(EyeInteractable eyeInteractable)
	{
		DeselectObject();
		selectedEyeInteractable = eyeInteractable;
		mode = Mode.SELECTED_OBJECT;
	}
	private void DeselectObject()
	{
		if (selectedEyeInteractable != null)
		{
			selectedEyeInteractable = null;
			mode = Mode.SELECTING_OBJECT;
		}
	}
	private void UpdateMenuTransform(GameObject menu)
	{
		Vector3 position = (menu == mainMenu) ? selectedEyeInteractable.transform.position : marker.transform.position;
		if (menu == mainMenu)
		{
			position += _camera.transform.right * 0.1f;
			position += _camera.transform.up * 0.0f;
		}
		menu.transform.position = position;
		//loggerText.text = position.ToString();

		var target = _camera.transform.position;
		target.y = menu.transform.position.y;
		menu.transform.LookAt(target);
		menu.transform.RotateAround(menu.transform.position, transform.up, 180f);
	}

	private void OnPointerTap()
	{
		switch (mode)
		{
			case Mode.NONE:
				break;
			case Mode.SELECTING_OBJECT:
				var closestSelectedObject = GetClosestHoveredObject();
				if (closestSelectedObject != null)
				{
					SelectObject(closestSelectedObject);
				}
				break;
			case Mode.SELECTED_OBJECT:
				if (hoveredButton != null)
				{
					var mainMenuText = hoveredButton.GetComponentInChildren<TMPro.TMP_Text>();
					OnMainMenuTap(mainMenuText.text);
					hoveredButton = null;
				}
				break;
			case Mode.MOVING_OBJECT:
				mode = Mode.SELECTING_OBJECT;
				break;
			case Mode.ROTATING_OBJECT:
				mode = Mode.SELECTING_OBJECT;
				break;
		}
	}

	private EyeInteractable GetClosestHoveredObject()
	{
		EyeInteractable closestObject = null;
		float closestDistance = 10F;
		foreach (var eyeInteractable in eyeInteractables)
		{
			if (eyeInteractable.IsHovered)
			{
				var distance = (eyeInteractable.transform.position - _camera.transform.position).magnitude;
				if (distance < closestDistance)
				{
					closestDistance = distance;
					closestObject = eyeInteractable;
				}
			}
		}
		return closestObject;
	}

	private void OnMiddleTap()
	{

	}

	private void OnMainMenuTap(string text)
	{
		loggerText.text = String.Format("edit: {0}", text);
		switch (text)
		{
			case "move":
				mode = Mode.MOVING_OBJECT;
				break;
			case "rotate":
				mode = Mode.ROTATING_OBJECT;
				break;
			case "tare":
				// FILL
				break;
			case "recipe":
				// FILL
				break;
			case "close":
				mode = Mode.SELECTING_OBJECT;
				break;
		}
	}

	private void OnModeUpdate()
	{
		bool shouldShowMainMenu = false;
		bool shouldShowRecipeMenu = false;
		bool shouldShowMarker = false;

		switch (mode)
		{
			case Mode.NONE:
				break;
			case Mode.SELECTING_OBJECT:
				break;
			case Mode.SELECTED_OBJECT:
				shouldShowMainMenu = true;
				break;
			case Mode.MOVING_OBJECT:
				break;
			case Mode.ROTATING_OBJECT:
				break;
		}

		if (shouldShowMainMenu != mainMenu.activeSelf)
		{
			if (shouldShowMainMenu)
			{
				UpdateMenuTransform(mainMenu);
			}
			mainMenu.SetActive(shouldShowMainMenu);
		}
		if (shouldShowRecipeMenu != recipeMenu.activeSelf)
		{
			if (shouldShowRecipeMenu)
			{
				UpdateMenuTransform(recipeMenu);
			}
			recipeMenu.SetActive(shouldShowRecipeMenu);
		}

		if (shouldShowMarker != marker.activeSelf)
		{
			if (!shouldShowMarker)
			{
				markerPosition = marker.transform.position;
			}
			marker.SetActive(shouldShowMarker);
		}

		UpdateObjectHighlights();
	}
}
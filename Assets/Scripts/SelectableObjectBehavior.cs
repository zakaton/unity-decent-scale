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

	[SerializeField]
	private TapStrapBLEMonoBehavior TapStrapBLEMonoBehavior;

	private GameObject marker;

	[SerializeField]
	private GameObject createMenu;
	[SerializeField]
	private GameObject editMenu;
	[SerializeField]
	private GameObject colorMenu;

	[SerializeField]
	private Camera _camera;

	private Vector3 markerPosition;

	public enum Mode
	{
		NONE,
		CREATING_OBJECT_POSITION,
		CREATING_OBJECT,
		SELECTING_OBJECT,
		SELECTED_OBJECT,
		MOVING_OBJECT,
		ROTATING_OBJECT,
		SCALING_OBJECT,
		COLORING_OBJECT
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
		TapStrapBLEMonoBehavior.tapStrap.tapEvents.tapData.AddListener(OnTapData);
		mode = Mode.CREATING_OBJECT_POSITION;

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
				case Mode.SCALING_OBJECT:
					var distance = (parent.position - marker.transform.position).magnitude;
					var scaleScalar = (float)Math.Clamp(distance, 0.1, 0.5);
					scaleScalar *= 7.5f;
					var scale = new Vector3(scaleScalar, scaleScalar, scaleScalar);
					parent.localScale = selectedEyeInteractable._scale = scale;
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
		Vector3 position = (menu == editMenu || menu == colorMenu) ? selectedEyeInteractable.transform.position : marker.transform.position;
		if (menu == editMenu || menu == colorMenu)
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

	private void OnTapData()
	{
		if (TapStrapBLEMonoBehavior.tapStrap.tapData[TapStrap.TapDataEnumeration.pointer])
		{
			OnPointerTap();
		}
	}

	private void OnPointerTap()
	{
		switch (mode)
		{
			case Mode.NONE:
				break;
			case Mode.CREATING_OBJECT_POSITION:
				mode = Mode.CREATING_OBJECT;
				break;
			case Mode.CREATING_OBJECT:
				if (hoveredButton != null)
				{
					var createMenuText = hoveredButton.GetComponentInChildren<TMPro.TMP_Text>();
					OnCreateMenuTap(createMenuText.text);
					hoveredButton = null;
				}
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
					var editMenuText = hoveredButton.GetComponentInChildren<TMPro.TMP_Text>();
					OnEditMenuTap(editMenuText.text);
					hoveredButton = null;
				}
				break;
			case Mode.MOVING_OBJECT:
				mode = Mode.SELECTING_OBJECT;
				break;
			case Mode.SCALING_OBJECT:
				mode = Mode.SELECTING_OBJECT;
				break;
			case Mode.ROTATING_OBJECT:
				mode = Mode.SELECTING_OBJECT;
				break;
			case Mode.COLORING_OBJECT:
				if (hoveredButton != null)
				{
					var colorMenuText = hoveredButton.GetComponentInChildren<TMPro.TMP_Text>();
					OnColorMenuTap(colorMenuText.text);
				}
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
		switch (mode)
		{
			case Mode.NONE:
			case Mode.SELECTING_OBJECT:
				mode = Mode.CREATING_OBJECT_POSITION;
				break;
			case Mode.CREATING_OBJECT_POSITION:
				mode = Mode.SELECTING_OBJECT;
				break;
			default:
				mode = Mode.SELECTING_OBJECT;
				break;
		}
	}

	private void OnCreateMenuTap(string text)
	{
		loggerText.text = String.Format("create: {0}", text);
		switch (text)
		{
			case "cube":
				CreateObject(ObjectType.cube);
				break;
			case "sphere":
				CreateObject(ObjectType.sphere);
				break;
			case "capsule":
				CreateObject(ObjectType.capsule);
				break;
			case "cylinder":
				CreateObject(ObjectType.cylinder);
				break;
			case "close":
				mode = Mode.CREATING_OBJECT_POSITION;
				break;
		}
	}
	private void OnEditMenuTap(string text)
	{
		loggerText.text = String.Format("edit: {0}", text);
		switch (text)
		{
			case "move":
				mode = Mode.MOVING_OBJECT;
				break;
			case "scale":
				mode = Mode.SCALING_OBJECT;
				break;
			case "rotate":
				mode = Mode.ROTATING_OBJECT;
				break;
			case "color":
				mode = Mode.COLORING_OBJECT;
				break;
			case "copy":
				if (selectedEyeInteractable != null)
				{
					var selectedGameObject = Instantiate(selectedEyeInteractable.transform.parent.gameObject, transform);
					var position = selectedGameObject.transform.position;
					var scale = selectedGameObject.transform.localScale;
					position.x += 0.3F;
					selectedGameObject.transform.position = position;
					selectedGameObject.transform.localScale = scale;
					selectedEyeInteractable = selectedGameObject.GetComponentInChildren<EyeInteractable>();
					selectedEyeInteractable._scale = scale;
					selectedEyeInteractable.isLarge = false;
					selectedEyeInteractable.IsHovered = false;
					IsWaitingForObjectToInstantiate = true;
					mode = Mode.MOVING_OBJECT;
				}
				break;
			case "delete":
				if (selectedEyeInteractable != null)
				{
					var gameObjectToDestroy = selectedEyeInteractable.transform.parent.gameObject;
					selectedEyeInteractable.ShouldIgnore = true;
					selectedEyeInteractable.deleted = true;
					eyeInteractables.Remove(selectedEyeInteractable);
					selectedEyeInteractable = null;
					Destroy(gameObjectToDestroy);
					mode = Mode.SELECTING_OBJECT;
				}
				break;
			case "close":
				mode = Mode.SELECTING_OBJECT;
				break;
		}
	}

	private void OnColorMenuTap(string text)
	{
		loggerText.text = String.Format("color: {0}", text);
		var meshRenderer = selectedEyeInteractable.GetComponent<MeshRenderer>();

		switch (text)
		{
			case "white":
				meshRenderer.material.color = Color.white;
				break;
			case "black":
				meshRenderer.material.color = Color.black;
				break;
			case "red":
				meshRenderer.material.color = Color.red;
				break;
			case "blue":
				meshRenderer.material.color = Color.blue;
				break;
			case "green":
				meshRenderer.material.color = Color.green;
				break;
			case "yellow":
				meshRenderer.material.color = Color.yellow;
				break;
			case "back":
				mode = Mode.SELECTED_OBJECT;
				break;
			case "close":
				mode = Mode.SELECTING_OBJECT;
				break;
		}
	}

	private void CreateObject(ObjectType objectType)
	{
		var prefab = objectTypes[objectType];
		var position = markerPosition;
		var rotation = new Quaternion();
		var newObject = Instantiate(prefab, position, rotation, transform);
		selectedEyeInteractable = newObject.GetComponentInChildren<EyeInteractable>();
		IsWaitingForObjectToInstantiate = true;
		mode = Mode.SELECTED_OBJECT;
	}

	private void OnModeUpdate()
	{
		bool shouldShowCreateMenu = false;
		bool shouldShowEditMenu = false;
		bool shouldShowColorMenu = false;
		bool shouldShowMarker = false;

		switch (mode)
		{
			case Mode.NONE:
				break;
			case Mode.CREATING_OBJECT_POSITION:
				shouldShowMarker = true;
				break;
			case Mode.CREATING_OBJECT:
				shouldShowCreateMenu = true;
				break;
			case Mode.SELECTING_OBJECT:
				break;
			case Mode.SELECTED_OBJECT:
				shouldShowEditMenu = true;
				break;
			case Mode.MOVING_OBJECT:
				break;
			case Mode.SCALING_OBJECT:
				break;
			case Mode.ROTATING_OBJECT:
				break;
			case Mode.COLORING_OBJECT:
				shouldShowColorMenu = true;
				break;
		}

		if (shouldShowCreateMenu != createMenu.activeSelf)
		{
			if (shouldShowCreateMenu)
			{
				UpdateMenuTransform(createMenu);
			}
			createMenu.SetActive(shouldShowCreateMenu);
		}
		if (shouldShowEditMenu != editMenu.activeSelf)
		{
			if (shouldShowEditMenu)
			{
				UpdateMenuTransform(editMenu);
			}
			editMenu.SetActive(shouldShowEditMenu);
		}
		if (shouldShowColorMenu != colorMenu.activeSelf)
		{
			if (shouldShowColorMenu)
			{
				UpdateMenuTransform(colorMenu);
			}
			colorMenu.SetActive(shouldShowColorMenu);
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static DecentScale;
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
	private DecentScaleBLEMonoBehavior decentScaleBLEMonoBehavior;
	[HideInInspector]
	private DecentScale decentScale
	{
		get
		{
			return decentScaleBLEMonoBehavior.decentScale;
		}
	}
	[HideInInspector]
	private EyeInteractable decentScaleEyeInteractable;
	[HideInInspector]
	private MeshRenderer decentScaleMeshRenderer;

	private GameObject marker;
	private MeshRenderer markerMeshRenderer;

	private GameObject display;
	private TMPro.TMP_Text decentScaleWeightText;
	private TMPro.TMP_Text decentScaleMacrosText;
	private TMPro.TMP_Text decentScaleCaloriesText;

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
		SELECTING_RECIPE,
		WEIGHING_INGREDIENT
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

	private string _currentRecipe;
	private string currentRecipe
	{
		get
		{
			return _currentRecipe;
		}
		set
		{
			if (_currentRecipe != value)
			{
				_currentRecipe = value;
				OnRecipeUpdate();
			}
		}
	}
	private string _currentIngredient;
	private string currentIngredient
	{
		get
		{
			return _currentIngredient;
		}
		set
		{
			if (_currentIngredient != value)
			{
				_currentIngredient = value;
				OnIngredientUpdate();
			}
		}
	}

	private Dictionary<string, (float servingSize, float calories, float carbs, float protein, float fat)> nutritionFacts = new Dictionary<string, (float, float, float, float, float)>
	{
		{ "oatmeal", (40.0f, 150.0f, 27.0f, 5.0f, 3.0f) },
		{ "almond milk", (240.0f, 30.0f, 1.0f, 1.0f, 2.5f) },
		{ "blueberries", (140.0f, 80.0f, 20.0f, 1.0f, 0.0f) },
		{ "banana", (136f, 120f, 31f, 1f, 0f) },
		{ "walnuts", (28f, 190f, 4f, 4f, 18f) },
	};

	private (string ingredient, float weight)[] ingredients = new (string, float)[] {
		("oatmeal", 40.0f),
		("walnuts", 20.0f),
		("blueberries", 100.0f),
		("almond milk", 240.0f)
	};

	private Dictionary<string, float> ingredientWeights = new Dictionary<string, float>();

	// Start is called before the first frame update
	void Start()
	{
		marker = GameObject.Find("marker");
		markerMeshRenderer = marker.GetComponentInChildren<MeshRenderer>();
		decentScaleEyeInteractable = decentScaleBLEMonoBehavior.gameObject.GetComponent<EyeInteractable>();
		decentScaleMeshRenderer = decentScaleBLEMonoBehavior.gameObject.GetComponent<MeshRenderer>();
		display = GameObject.Find("DecentScaleDisplay");
		decentScaleWeightText = display.transform.Find("weight").GetComponent<TMPro.TMP_Text>();
		decentScaleMacrosText = display.transform.Find("macros").GetComponent<TMPro.TMP_Text>();
		decentScaleCaloriesText = display.transform.Find("calories").GetComponent<TMPro.TMP_Text>();

		foreach (var eyeTrackingRay in eyeTrackingRays)
		{
			eyeTrackingRay.OnObjectHoverUpdate.AddListener(OnObjectHoverUpdate);
		}
		mode = Mode.NONE;
		OnModeUpdate();
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
			//loggerText.text = marker.transform.position.ToString();
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
		Vector3 position = selectedEyeInteractable.transform.position;
		position += _camera.transform.right * 0.1f;
		position += _camera.transform.up * 0.0f;
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
			case Mode.SELECTING_RECIPE:
				if (hoveredButton != null)
				{
					var recipeMenuText = hoveredButton.GetComponentInChildren<TMPro.TMP_Text>();
					OnRecipeMenuTap(recipeMenuText.text);
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
		switch (mode)
		{
			case Mode.NONE:
			case Mode.SELECTING_OBJECT:
			case Mode.SELECTED_OBJECT:
				mode = Mode.MOVING_OBJECT;
				break;
		}
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
				decentScale.Tare();
				break;
			case "recipe":
				mode = Mode.SELECTING_RECIPE;
				break;
			case "close":
				mode = Mode.SELECTING_OBJECT;
				break;
		}
	}
	private void OnRecipeMenuTap(string text)
	{
		loggerText.text = String.Format("recipe: {0}", text);
		switch (text)
		{
			case "oatmeal":
				// FILL - start weighing first ingredient
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
		bool shouldShowDecentScale = false;
		bool shouldShowDecentScaleDisplay = false;

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
			case Mode.ROTATING_OBJECT:
				selectedEyeInteractable = decentScaleEyeInteractable;
				shouldShowDecentScale = true;
				break;
			case Mode.SELECTING_RECIPE:
				shouldShowRecipeMenu = true;
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

		if (shouldShowMarker != markerMeshRenderer.enabled)
		{
			if (!shouldShowMarker)
			{
				markerPosition = marker.transform.position;
			}
			markerMeshRenderer.enabled = shouldShowMarker;
		}

		if (shouldShowDecentScale != decentScaleMeshRenderer.enabled)
		{
			decentScaleMeshRenderer.enabled = shouldShowDecentScale;
		}

		if (shouldShowDecentScaleDisplay != display.activeSelf)
		{
			display.SetActive(shouldShowDecentScaleDisplay);
		}

		UpdateObjectHighlights();
	}

	private void UpdateWeightText()
	{
		if (display.activeSelf)
		{
			if (mode == Mode.WEIGHING_INGREDIENT)
			{
				decentScaleWeightText.text = String.Format("{0}/{1}g", decentScale.weight.ToString("N1"), 0.0f);
			}
			else
			{
				decentScaleWeightText.text = String.Format("{0}g", decentScale.weight);
			}
		}
	}
	private void UpdateMacrosText()
	{
		if (display.activeSelf)
		{
			// FILL
		}
	}
	private void UpdateCaloriesText()
	{
		if (display.activeSelf)
		{
			// FILL
		}
	}

	private void OnRecipeUpdate()
	{
		ingredientWeights.Clear();
		OnUpdateIngredientWeights();
		// FILL
	}
	private void OnIngredientUpdate()
	{
		decentScale.Tare();
		// FILL
	}

	private void OnUpdateIngredientWeights()
	{
		// FILL - update macros and UI
	}
}


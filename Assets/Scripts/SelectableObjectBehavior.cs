using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
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
	private TMPro.TMP_Text decentScaleTitleText;
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

	private string _currentRecipe = null;
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

	private Dictionary<string, (float servingSize, float calories, float carbs, float protein, float fat)> nutritionFacts = new Dictionary<string, (float, float, float, float, float)>
	{
		{ "oatmeal", (40.0f, 150.0f, 27.0f, 5.0f, 3.0f) },
		{ "almond milk", (240.0f, 30.0f, 1.0f, 1.0f, 2.5f) },
		{ "blueberries", (140.0f, 80.0f, 20.0f, 1.0f, 0.0f) },
		{ "banana", (136f, 120f, 31f, 1f, 0f) },
		{ "walnuts", (28f, 190f, 4f, 4f, 18f) },
	};

	private (string name, float weight)[] ingredients = new (string, float)[] {
		("oatmeal", 40.0f),
		("walnuts", 20.0f),
		("blueberries", 100.0f),
		("almond milk", 240.0f)
	};
	private float calories = 0;
	private (float protein, float carbs, float fat) macros = (0f, 0f, 0f);

	private int _ingredientIndex = 0;
	private int ingredientIndex
	{
		get { return _ingredientIndex; }
		set
		{
			_ingredientIndex = value;
			currentIngredient = ingredients[ingredientIndex];
			OnIngredientUpdate();
		}
	}
	private (string name, float weight) currentIngredient;

	private Dictionary<string, float> ingredientWeights = new Dictionary<string, float>();

	// Start is called before the first frame update
	void Start()
	{
		marker = GameObject.Find("marker");
		markerMeshRenderer = marker.GetComponentInChildren<MeshRenderer>();
		decentScaleEyeInteractable = decentScaleBLEMonoBehavior.gameObject.GetComponent<EyeInteractable>();
		decentScaleMeshRenderer = decentScaleBLEMonoBehavior.gameObject.GetComponent<MeshRenderer>();
		display = GameObject.Find("DecentScaleDisplay");
		decentScaleTitleText = display.transform.Find("title").GetComponent<TMPro.TMP_Text>();
		decentScaleWeightText = display.transform.Find("weight").GetComponent<TMPro.TMP_Text>();
		decentScaleMacrosText = display.transform.Find("macros").GetComponent<TMPro.TMP_Text>();
		decentScaleCaloriesText = display.transform.Find("calories").GetComponent<TMPro.TMP_Text>();

		foreach (var eyeTrackingRay in eyeTrackingRays)
		{
			eyeTrackingRay.OnObjectHoverUpdate.AddListener(OnObjectHoverUpdate);
		}

		mode = Mode.NONE;
		OnModeUpdate();

		decentScale.decentScaleEvents.weight.AddListener(OnWeightData);
		decentScale.connectionEvents.onConnect.AddListener(OnConnect);
	}

	private void OnConnect()
	{
		//currentRecipe = "oatmeal";
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
	private void UpdateTransform(Transform _transform, bool isLeft = false)
	{
		Vector3 position = selectedEyeInteractable.transform.position;
		position += _camera.transform.right * (isLeft ? -0.1f : 0.1f);
		position += _camera.transform.up * 0.0f;
		_transform.position = position;

		RotateToFaceCamera(_transform);
	}
	private void RotateToFaceCamera(Transform _transform)
	{
		var target = _camera.transform.position;
		//target.y = _transform.position.y;
		_transform.LookAt(target);
		_transform.RotateAround(_transform.position, _transform.up, 180f);
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
			case Mode.WEIGHING_INGREDIENT:
				decentScale.Tare();
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
				currentRecipe = "oatmeal";
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
		bool shouldShowDecentScaleDisplay = true;

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
				UpdateTransform(mainMenu.transform);
			}
			mainMenu.SetActive(shouldShowMainMenu);
		}
		if (shouldShowRecipeMenu != recipeMenu.activeSelf)
		{
			if (shouldShowRecipeMenu)
			{
				UpdateTransform(recipeMenu.transform);
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

		if (true || shouldShowDecentScaleDisplay != display.activeSelf)
		{
			display.SetActive(shouldShowDecentScaleDisplay);
			if (shouldShowDecentScaleDisplay)
			{
				RotateToFaceCamera(display.transform);
			}
		}

		UpdateObjectHighlights();
	}

	private void OnRecipeUpdate()
	{
		ingredientWeights.Clear();

		macros.protein = 0f;
		macros.fat = 0f;
		macros.carbs = 0f;
		OnUpdateIngredientWeights();

		ingredientIndex = 0;
		mode = Mode.WEIGHING_INGREDIENT;

		UpdateTitleText();
	}
	private void OnIngredientUpdate()
	{
		decentScale.Tare();
		UpdateTitleText();
	}

	private void OnUpdateIngredientWeights()
	{
		UpdateMacros();

		UpdateCaloriesText();
		UpdateMacrosText();
		UpdateWeightText();
	}

	private void UpdateMacros()
	{
		float _calories = 0;
		var _macros = (protein: 0f, carbs: 0f, fat: 0f);

		foreach (var ingredientWeight in ingredientWeights)
		{
			var name = ingredientWeight.Key;
			var weight = ingredientWeight.Value;

			if (nutritionFacts.ContainsKey(name))
			{
				var _nutritionFacts = nutritionFacts[name];
				var servings = ingredientWeights[name] / _nutritionFacts.servingSize;
				_calories += servings * _nutritionFacts.calories;
				_macros.protein += servings * _nutritionFacts.protein;
				_macros.carbs += servings * _nutritionFacts.carbs;
				_macros.fat += servings * _nutritionFacts.fat;
			}
		}
		calories = _calories;
		macros = _macros;
	}

	private void UpdateTitleText()
	{
		if (display.activeSelf)
		{
			if (mode == Mode.WEIGHING_INGREDIENT)
			{
				decentScaleTitleText.text = currentIngredient.name;
			}
			else
			{
				decentScaleTitleText.text = "weight";
			}
		}
	}
	private void UpdateWeightText()
	{
		if (display.activeSelf)
		{
			if (mode == Mode.WEIGHING_INGREDIENT)
			{
				decentScaleWeightText.text = String.Format("{0}/{1}g", decentScale.weight.ToString("N1"), Mathf.RoundToInt(currentIngredient.weight));
			}
			else
			{
				decentScaleWeightText.text = String.Format("{0}g", decentScale.weight.ToString("N1"));
			}
		}
	}
	private void UpdateMacrosText()
	{
		if (display.activeSelf)
		{
			if (currentRecipe == null)
			{
				decentScaleMacrosText.text = "";
			}
			else
			{
				decentScaleMacrosText.text = String.Format("{0}c/{1}p/{2}f", Mathf.RoundToInt(macros.carbs), Mathf.RoundToInt(macros.protein), Mathf.RoundToInt(macros.fat));
			}
		}
	}
	private void UpdateCaloriesText()
	{
		if (display.activeSelf)
		{
			if (currentRecipe == null)
			{
				decentScaleCaloriesText.text = "";
			}
			else
			{
				decentScaleCaloriesText.text = String.Format("{0} cal", Mathf.RoundToInt(calories));
			}
		}
	}

	private void OnWeightData(float weight, bool isStable, WeightTimestamp weightTimestamp)
	{
		if (mode == Mode.WEIGHING_INGREDIENT)
		{
			ingredientWeights[currentIngredient.name] = weight;
			// FILL - if stable for 2 seconds, move onto next ingredient
		}
		OnUpdateIngredientWeights();
	}
}
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEngine.UI;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EyeInteractable : MonoBehaviour
{
	[SerializeField]
	public UnityEvent<GameObject> OnObjectHover;

	[SerializeField]
	public UnityEvent<GameObject> OnObjectUnHover;

	[SerializeField]
	private Material OnHoverActiveMaterial;

	[SerializeField]
	private Material OnHoverInactiveMaterial;

	[SerializeField]
	public bool IsSelectable = false;

	[SerializeField]
	public bool IsButton = false;

	[HideInInspector]
	public Vector3 _scale = new(0F, 0F, 0F);

	private MeshRenderer meshRenderer;
	public Collider _collider;

	[HideInInspector]
	public bool deleted = false;

	[HideInInspector]
	public bool initialized = false;

	public Vector3 hitPoint;
	public bool didHoverFlag = false;

	[HideInInspector]
	public bool isLarge = false;

	private UnityEngine.UI.Image image;

	void Start()
	{
		if (_scale.magnitude == 0)
		{
			_scale.Set(1, 1, 1);
		}
		meshRenderer = GetComponent<MeshRenderer>();
		_collider = GetComponent<BoxCollider>();
		if (IsButton)
		{
			image = GetComponent<UnityEngine.UI.Image>();
		}
		initialized = true;
	}

	[SerializeField]
	public bool ShouldIgnore = false;

	private bool _IsHovered = false;
	public bool IsHovered
	{
		get
		{
			return _IsHovered;
		}
		set
		{
			didHoverFlag = true;
			if (_IsHovered != value)
			{
				_IsHovered = value;
				if (image != null)
				{
					image.color = _IsHovered ? Color.yellow : Color.white;
				}

				if (_IsHovered)
				{
					if (meshRenderer && OnHoverActiveMaterial)
					{
						meshRenderer.material = OnHoverActiveMaterial;
					}
					OnObjectHover.Invoke(gameObject);
				}
				else
				{
					if (meshRenderer && OnHoverInactiveMaterial)
					{
						meshRenderer.material = OnHoverInactiveMaterial;
					}
					OnObjectUnHover.Invoke(gameObject);
				}
			}
		}
	}
}
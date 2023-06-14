using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Collections;

/*
  TODO
    process haptics sequence
    process sensitivty sequence
*/

[Serializable]
public class TapStrap
{
	public TMPro.TMP_Text loggerText;

	[HideInInspector]
	public bool IsConnected = false;
	[HideInInspector]
	public bool IsConnecting = false;

	public enum HandSide { left, right }

	// https://github.com/zakaton/tap-strap-web-sdk/blob/main/TapStrap.js#L539
	public enum AirGestureEnumeration
	{
		NONE,
		GENERAL,
		UP_ONE_FINGER,
		UP_TWO_FINGERS,
		DOWN_ONE_FINGER,
		DOWN_TWO_FINGERS,
		LEFT_ONE_FINGER,
		LEFT_TWO_FINGERS,
		RIGHT_ONE_FINGER,
		RIGHT_TWO_FINGERS,
		THUMB_FINGER,
		THUMB_MIDDLE,
	}

	// https://github.com/zakaton/tap-strap-web-sdk/blob/main/TapStrap.js#L554
	public enum TapDataEnumeration
	{
		thumb, pointer, middle, ring, pinky
	}

	// https://github.com/zakaton/tap-strap-web-sdk/blob/main/TapStrap.js#L557
	public enum MouseModeEnumeration { STDBY, AIR_MOUSE, OPTICAL1, OPTICAL2 }

	// https://github.com/zakaton/tap-strap-web-sdk/blob/main/TapStrap.js#L560
	public enum InputMode { text, controller, controllerText, raw }
	public static readonly Dictionary<InputMode, byte[]> InputModeCode = new Dictionary<InputMode, byte[]>
	{
	 { InputMode.text, new byte[]{0x3, 0xc, 0x0, 0x0}},
	 { InputMode.controller, new byte[]{0x3, 0xc, 0x0, 0x1}},
	 { InputMode.controllerText, new byte[]{0x3, 0xc, 0x0, 0x3}},
	 { InputMode.raw, new byte[]{0x3, 0xc, 0x0, 0xa, 0x0, 0x0, 0x0}}
	};

	public byte[] inputModeCode
	{
		get
		{
			return InputModeCode[_inputMode];
		}
	}

	[SerializeField]
	public InputMode inputMode = InputMode.controller;
	[HideInInspector]
	private InputMode _inputMode = InputMode.controller;

	[SerializeField]
	public string deviceName = "Tap_D124264"; // Tap_D065264:Tap_D124264
	[SerializeField]
	public bool autoConnect = true;
	[SerializeField]
	public HandSide handSide = HandSide.right;

	public Logger logger = new Logger(Debug.unityLogger.logHandler);
	[SerializeField]
	public bool enableLogging = true;
	public void UpdateLogging()
	{
		logger.logEnabled = enableLogging;
	}

	private UInt16 ToUInt16(byte[] bytes, int byteOffset)
	{
		UInt16 value = BitConverter.ToUInt16(bytes, byteOffset);
		if (!BitConverter.IsLittleEndian)
		{
			value = BinaryPrimitives.ReverseEndianness(value);
		}
		return value;
	}
	private Int16 ToInt16(byte[] bytes, int byteOffset)
	{
		Int16 value = BitConverter.ToInt16(bytes, byteOffset);
		if (!BitConverter.IsLittleEndian)
		{
			value = BinaryPrimitives.ReverseEndianness(value);
		}
		return value;
	}
	private UInt32 ToUInt32(byte[] bytes, int byteOffset)
	{
		UInt32 value = BitConverter.ToUInt32(bytes, byteOffset);
		if (!BitConverter.IsLittleEndian)
		{
			value = BinaryPrimitives.ReverseEndianness(value);
		}
		return value;
	}
	private double ToDouble(byte[] bytes, int byteOffset)
	{
		if (!BitConverter.IsLittleEndian)
		{
			byte[] _bytes = new byte[8];
			for (int i = 0; i < _bytes.Length; i++)
			{
				_bytes[i] = bytes[_bytes.Length - 1 - i];
			}
			return BitConverter.ToDouble(_bytes, byteOffset);
		}
		else
		{
			return BitConverter.ToDouble(bytes, byteOffset);
		}
	}
	private float ToSingle(byte[] bytes, int byteOffset)
	{
		if (!BitConverter.IsLittleEndian)
		{
			byte[] _bytes = new byte[4];
			for (int i = 0; i < _bytes.Length; i++)
			{
				_bytes[i] = bytes[_bytes.Length - 1 - i];
			}
			return BitConverter.ToSingle(_bytes, byteOffset);
		}
		else
		{
			return BitConverter.ToSingle(bytes, byteOffset);
		}
	}

	[Serializable]
	public class ConnectionEvents
	{
		public UnityEvent onConnect = new();
		public UnityEvent onConnecting = new();
		public UnityEvent onStopConnecting = new();
		public UnityEvent onDisconnect = new();
	}
	public ConnectionEvents connectionEvents = new();

	[Serializable]
	public class TapEvents
	{
		public UnityEvent tapData = new();

		public UnityEvent mouse = new();

		public UnityEvent mouseMode = new();
		public UnityEvent airGesture = new();

		public UnityEvent accelerometer = new();
		public UnityEvent imu = new();
		public UnityEvent raw = new();
	}
	[SerializeField]
	public TapEvents tapEvents = new();

	public string StatusMessage
	{
		set
		{
			logger.Log(value);
			if (loggerText)
			{
				loggerText.text = value;
			}
		}
	}

	// Start is called before the first frame update
	public virtual void Start()
	{
		updateInputModeCoroutine = _UpdateInputModeCoroutine();
		UpdateLogging();
	}
	protected bool ShouldUpdateInputMode = false;
	public void UpdateInputMode()
	{
		ShouldUpdateInputMode = true;
	}
	protected void CheckUpdateInputMode()
	{
		if (ShouldUpdateInputMode)
		{
			ShouldUpdateInputMode = false;
			_UpdateInputMode();
		}
	}
	public void Update()
	{
		if (_inputMode != inputMode)
		{
			_inputMode = inputMode;
			resetCoroutine = true;
			UpdateInputMode();
		}
	}
	public virtual void Connect() { }
	public virtual void Disconnect() { }
	public virtual void _UpdateInputMode() { }

	[HideInInspector]
	public bool resetCoroutine = false;
	public IEnumerator updateInputModeCoroutine;
	private IEnumerator _UpdateInputModeCoroutine()
	{
		while (true)
		{
			StatusMessage = "UpdateInputMode";
			UpdateInputMode();
			yield return new WaitForSeconds(10f);
		}
	}

	public Dictionary<TapDataEnumeration, bool> tapData = new Dictionary<TapDataEnumeration, bool>();
	public void ProcessTapData(byte[] bytes)
	{
		StatusMessage = String.Format("ProcessTapData {0}", bytes.Length);
		uint byteOffset = 0;
		byte tapBitmask = bytes[byteOffset++];
		foreach (TapDataEnumeration tapDataEnumeration in Enum.GetValues(typeof(TapDataEnumeration)))
		{
			int index = ((int)tapDataEnumeration);
			tapData[tapDataEnumeration] = (tapBitmask & (1 << index)) != 0;
			if (tapData[tapDataEnumeration])
			{
				StatusMessage = String.Format("tapped {0}", tapDataEnumeration.ToString());
			}
		}
		UInt16 timeInterval = ToUInt16(bytes, 1);
		// StatusMessage = String.Format("interval: {0}", timeInterval);
		byteOffset += 2;
		tapEvents.tapData.Invoke();
	}

	public Vector2 mouseData;
	public bool mouseProximation = false;
	public void ProcessMouseData(byte[] bytes)
	{
		StatusMessage = String.Format("ProcessMouseData {0}", bytes.Length);
		int byteOffset = 0;
		Int16 x = ToInt16(bytes, byteOffset);
		byteOffset += 2;
		Int16 y = ToInt16(bytes, byteOffset);
		byteOffset += 2;
		mouseData.Set(x, -y);

		mouseProximation = bytes[9] == 1;

		StatusMessage = String.Format("mouseData {0}", mouseData.ToString());
		StatusMessage = String.Format("mouseProximation {0}", mouseProximation);
		tapEvents.mouse.Invoke();
	}

	public AirGestureEnumeration airGesture;
	public MouseModeEnumeration mouseMode;
	public void ProcessAirGestureData(byte[] bytes)
	{
		StatusMessage = String.Format("ProcessAirGestureData {0}", bytes.Length);
		if (bytes[0] == 0x14)
		{
			mouseMode = (MouseModeEnumeration)bytes[1];
			StatusMessage = String.Format("Mouse Mode: {0}", mouseMode.ToString());
			tapEvents.mouseMode.Invoke();
		}
		else
		{
			airGesture = (AirGestureEnumeration)bytes[0];
			StatusMessage = String.Format("Air Gesture: {0}", airGesture.ToString());
			tapEvents.airGesture.Invoke();
		}
	}

	public Vector3[] accelerometerData = new Vector3[5];
	public UInt32 accelerometerDataTimestamp;
	public Vector3[] imuData = new Vector3[2];
	public UInt32 imuDataTimestamp;
	private readonly static UInt32 timeStampSubtractor = (UInt32)Math.Pow(2, 31);
	public void ProcessRawData(byte[] bytes)
	{
		StatusMessage = String.Format("ProcessRawData {0}", bytes.Length);
		int byteOffset = 0;
		while (byteOffset < bytes.Length)
		{
			UInt32 timestamp = ToUInt32(bytes, byteOffset);
			if (timestamp == 0)
			{
				break;
			}
			else
			{
				byteOffset += 4;
				string type;
				int numberOfSamples;
				if (timestamp > timeStampSubtractor)
				{
					type = "accelerometer";
					timestamp -= timeStampSubtractor;
					numberOfSamples = 15;
					accelerometerDataTimestamp = timestamp;
				}
				else
				{
					type = "imu";
					numberOfSamples = 6;
					imuDataTimestamp = timestamp;
				}

				for (int payloadIndex = 0, sensorIndex = 0, vectorIndex = 0; payloadIndex < numberOfSamples; payloadIndex++, vectorIndex = (vectorIndex + 1) % 3)
				{
					Int16 value = ToInt16(bytes, byteOffset);
					byteOffset += 2;

					Int16 _value = value;
					// _value *= -1;
					int _vectorIndex = vectorIndex;

					switch (vectorIndex)
					{
						case 0:
							_value *= -1;
							_vectorIndex = 2;
							break;
						case 1:
							_vectorIndex = 0;
							break;
						case 2:
							_vectorIndex = 1;
							break;
					}

					switch (type)
					{
						case "imu":
							imuData[sensorIndex][_vectorIndex] = _value;
							break;
						case "accelerometer":
							accelerometerData[sensorIndex][_vectorIndex] = _value;
							break;
					}

					if (vectorIndex == 2)
					{
						sensorIndex++;
					}
				}

				switch (type)
				{
					case "imu":
						// StatusMessage = String.Format("imu data {0} [{1}]", imuData[0].ToString(), imuDataTimestamp);
						tapEvents.imu.Invoke();
						break;
					case "accelerometer":
						// StatusMessage = String.Format("accelerometer data {0} [{1}]", accelerometerData[0].ToString(), accelerometerDataTimestamp);
						tapEvents.accelerometer.Invoke();
						break;
				}
			}
		}
		tapEvents.raw.Invoke();
	}

	public virtual uint[] sensitivity { get; set; }
	public virtual void Vibrate(uint[] sequence) { }
}

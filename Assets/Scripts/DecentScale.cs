using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Collections;
using Unity.VisualScripting;

/*
  TODO
    *
*/

[Serializable]
public class DecentScale
{
	private UInt16 ToUInt16(byte[] bytes, int byteOffset, bool littleEndian = false)
	{
		UInt16 value = BitConverter.ToUInt16(bytes, byteOffset);
		if (littleEndian != BitConverter.IsLittleEndian)
		{
			value = BinaryPrimitives.ReverseEndianness(value);
		}
		return value;
	}
	private Int16 ToInt16(byte[] bytes, int byteOffset, bool littleEndian = false)
	{
		Int16 value = BitConverter.ToInt16(bytes, byteOffset);
		if (littleEndian != BitConverter.IsLittleEndian)
		{
			value = BinaryPrimitives.ReverseEndianness(value);
		}
		return value;
	}
	private UInt32 ToUInt32(byte[] bytes, int byteOffset, bool littleEndian = false)
	{
		UInt32 value = BitConverter.ToUInt32(bytes, byteOffset);
		if (littleEndian != !BitConverter.IsLittleEndian)
		{
			value = BinaryPrimitives.ReverseEndianness(value);
		}
		return value;
	}
	private double ToDouble(byte[] bytes, int byteOffset, bool littleEndian = false)
	{
		if (littleEndian != BitConverter.IsLittleEndian)
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
	private float ToSingle(byte[] bytes, int byteOffset, bool littleEndian = false)
	{
		if (littleEndian != BitConverter.IsLittleEndian)
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
	private byte BoolToByte(bool _bool)
	{
		return (byte)(_bool ? 1 : 0);
	}

	public TMPro.TMP_Text loggerText;

	[HideInInspector]
	public bool IsConnected = false;
	[HideInInspector]
	public bool IsConnecting = false;

	[SerializeField]
	public bool autoConnect = true;

	private void _SetLED()
	{
		SetLED(showWeight, showTimer, showGrams);
	}

	private bool _showWeight = false;
	[SerializeField]
	public bool showWeight = false;
	private void checkShowWeight()
	{
		if (_showWeight != showWeight)
		{
			_showWeight = showWeight;
			_SetLED();
		}
	}
	private bool _showTimer = false;
	[SerializeField]
	public bool showTimer = false;
	private void checkShowTimer()
	{
		if (_showTimer != showTimer)
		{
			_showTimer = showTimer;
			_SetLED();
		}
	}
	private bool _showGrams = true;
	[SerializeField]
	public bool showGrams = true;
	private void checkShowGrams()
	{
		if (_showGrams != showGrams)
		{
			_showGrams = showGrams;
			_SetLED();
		}
	}

	[SerializeField]
	public bool startTimer = false;
	private void checkStartTimer()
	{
		if (startTimer)
		{
			startTimer = false;
			StartTimer();
		}
	}
	[SerializeField]
	public bool resetTimer = false;
	private void checkResetTimer()
	{
		if (resetTimer)
		{
			resetTimer = false;
			ResetTimer();
		}
	}
	[SerializeField]
	public bool stopTimer = false;
	private void checkStopTimer()
	{
		if (stopTimer)
		{
			stopTimer = false;
			StopTimer();
		}
	}

	[SerializeField]
	public bool tare = false;
	private void checkTare()
	{
		if (tare)
		{
			tare = false;
			Tare();
		}
	}

	[SerializeField]
	public bool powerOff = false;
	private void checkPowerOff()
	{
		if (powerOff)
		{
			powerOff = false;
			PowerOff();
		}
	}

	[SerializeField]
	public int tareCounter = 0;

	public Logger logger = new Logger(Debug.unityLogger.logHandler);
	[SerializeField]
	public bool enableLogging = true;
	public void UpdateLogging()
	{
		logger.logEnabled = enableLogging;
	}

	public readonly Dictionary<int, string> FIRMWARE_ENUM = new Dictionary<int, string>
	{
		{0xfe,"1.0"},
		{0x02,"1.1"},
		{0x03,"1.2"}
	};
	public readonly int USB_BATTERY_ENUM = 255;

	[SerializeField]
	public string firmwareVersion;
	[HideInInspector]
	private bool checkedFirmwareVersion = false;
	[SerializeField]
	public int battery = 0;
	[SerializeField]
	public bool isUSB = false;
	[HideInInspector]
	private bool checkedIsUSB = false;

	[SerializeField]
	public float weight = 0;
	[SerializeField]
	public bool isStable = true;

	[Serializable]
	public class WeightTimestamp
	{
		public int minutes = 0;
		public int seconds = 0;
		public int milliseconds = 0;
		public override string ToString()
		{
			string minutesString = minutes.ToString().PadLeft(2, '0');
			string secondsString = seconds.ToString().PadLeft(2, '0');
			string millisecondsString = milliseconds.ToString().PadLeft(3, '0');
			return String.Format("{0}:{1}:{2}", minutesString, secondsString, millisecondsString);
		}
	}
	public WeightTimestamp weightTimestamp = new();

	public enum DisplayWeightType { grams, ounces };
	[SerializeField]
	public DisplayWeightType displayWeightType = DisplayWeightType.grams;
	[HideInInspector]
	private bool checkedDisplayWeightType = false;

	public enum ButtonType { left = 1, right };
	public enum ButtonTapType { shortPress = 1, longPress };

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
	public class DecentScaleEvents
	{
		public UnityEvent<string> firmwareVersion = new();
		public UnityEvent<int> battery = new();
		public UnityEvent<bool> isUSB = new();
		public UnityEvent<float, bool, WeightTimestamp> weight = new();
		public UnityEvent<int> tare = new();
		public UnityEvent<DisplayWeightType> displayWeightType = new();
		public UnityEvent<ButtonType, ButtonTapType> buttonTap = new();
	}
	[SerializeField]
	public DecentScaleEvents decentScaleEvents = new();

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
		UpdateLogging();
		connectionEvents.onConnect.AddListener(OnConnect);
	}

	private bool shouldSetLED = false;
	private float shouldSetLEDTime = 0;
	public virtual void Update()
	{
		checkShowWeight();
		checkShowTimer();
		checkShowGrams();

		checkStartTimer();
		checkStopTimer();
		checkResetTimer();

		checkTare();

		checkPowerOff();

		if (shouldSetLED && Time.time > shouldSetLEDTime)
		{
			_SetLED();
			shouldSetLED = false;
		}
	}
	public virtual void Connect() { }
	public virtual void Disconnect() { }

	public void OnConnect()
	{
		shouldSetLEDTime = Time.time + 2;
		shouldSetLED = true;
		//SetLED();
	}

	public enum Command
	{
		prefix = 0x03,
		led = 0x0a,
		timer = 0x0b,
		tare = 0x0f,
		stableWeight = 0xce,
		unstableWeight = 0xca,
		buttonTap = 0xaa
	}
	public byte XORNumbers(List<byte> numbers)
	{
		byte XORNumber = numbers[0];
		for (int i = 1; i < numbers.Count; i++)
		{
			XORNumber ^= numbers[i];
		}
		return XORNumber;
	}
	public void formatCommandData(List<byte> commandData)
	{
		commandData.Insert(0, (byte)Command.prefix);
		commandData.Add(XORNumbers(commandData));
	}

	[HideInInspector]
	protected List<List<byte>> commands = new();
	public virtual void sendCommand(List<byte> commandData, bool forceSend = false)
	{

	}

	public void Tare(int incrementedInteger = 0)
	{
		var commandData = new List<byte> {
			(byte)Command.tare,
			(byte)incrementedInteger,
			0, 0, 0
		};
		sendCommand(commandData);
	}
	public void SetLED(bool showWeight = false, bool showTimer = false, bool showGrams = true)
	{
		var commandData = new List<byte> {
			(byte)Command.led,
			BoolToByte(showWeight),
			BoolToByte(showTimer),
			BoolToByte(!showGrams),
			0
		};
		sendCommand(commandData);
	}

	public enum TimerCommand
	{
		stop = 0,
		reset = 2,
		start = 3
	}
	public void SetTimerCommand(TimerCommand timerCommand)
	{
		var commandData = new List<byte> {
			(byte)Command.timer,
			(byte)timerCommand,
			0, 0, 0
		};
		sendCommand(commandData);
	}
	public void StartTimer()
	{
		SetTimerCommand(TimerCommand.start);
	}
	public void StopTimer()
	{
		SetTimerCommand(TimerCommand.stop);
	}
	public void ResetTimer()
	{
		SetTimerCommand(TimerCommand.reset);
	}

	public void PowerOff()
	{
		var commandData = new List<byte> {
			(byte)Command.led, 2,
			0, 0, 0
		};
		sendCommand(commandData);
	}

	public void ProcessLEDData(byte[] bytes, int byteOffset = 0)
	{
		//StatusMessage = String.Format("ProcessLEDData {0}", bytes.Length);

		var _battery = bytes[byteOffset + 4];
		var _isUSB = _battery == USB_BATTERY_ENUM;
		if (_isUSB)
		{
			battery = 100;
		}
		else
		{
			battery = _battery;
		}
		if (!checkedIsUSB || isUSB != _isUSB)
		{
			checkedIsUSB = true;
			isUSB = _isUSB;
			StatusMessage = String.Format("isUSB: {0}", isUSB);
			decentScaleEvents.isUSB.Invoke(isUSB);
			if (isUSB)
			{
				StatusMessage = String.Format("usb battery: {0}", battery);
				decentScaleEvents.battery.Invoke(battery);
			}
		}
		if (!isUSB)
		{
			StatusMessage = String.Format("battery: {0}", battery);
			decentScaleEvents.battery.Invoke(battery);
		}

		var _displayWeightType = (DisplayWeightType)bytes[byteOffset + 3];
		if (!checkedDisplayWeightType || displayWeightType != _displayWeightType)
		{
			checkedDisplayWeightType = true;
			displayWeightType = _displayWeightType;
			StatusMessage = String.Format("display weight type: {0}", displayWeightType.ToString());
			decentScaleEvents.displayWeightType.Invoke(displayWeightType);
		}

		var _firmwareVersion = FIRMWARE_ENUM[bytes[byteOffset + 5]];
		if (!checkedFirmwareVersion || firmwareVersion != _firmwareVersion)
		{
			checkedFirmwareVersion = true;
			firmwareVersion = _firmwareVersion;
			StatusMessage = String.Format("firmware version: {0}", firmwareVersion);
			decentScaleEvents.firmwareVersion.Invoke(firmwareVersion);
		}
	}
	public void ProcessWeightData(byte[] bytes, int byteOffset = 0)
	{
		//StatusMessage = String.Format("ProcessWeightData {0}", bytes.Length);
		isStable = bytes[byteOffset + 1] == (int)Command.stableWeight;
		weight = (float)ToInt16(bytes, byteOffset + 2, false);
		var _byteLength = bytes.Length - byteOffset;
		if (_byteLength == 7)
		{
			// FILL - add "change"
		}
		else if (_byteLength == 10)
		{
			weightTimestamp.minutes = bytes[byteOffset + 4];
			weightTimestamp.seconds = bytes[byteOffset + 5];
			weightTimestamp.milliseconds = bytes[byteOffset + 6] * 100;
		}
		//StatusMessage = String.Format("weight {0} grams, {1}, {2}", weight, isStable ? "stable" : "unstable", weightTimestamp.ToString());
		decentScaleEvents.weight.Invoke(weight, isStable, weightTimestamp);
	}
	public void ProcessButtonTapData(byte[] bytes, int byteOffset = 0)
	{
		//StatusMessage = String.Format("ProcessButtonTapData {0}", bytes.Length);
		var button = (ButtonType)bytes[byteOffset + 2];
		var buttonTapType = (ButtonTapType)bytes[byteOffset + 3];
		StatusMessage = String.Format("tap {0}: {1}", button.ToString(), buttonTapType.ToString());
		decentScaleEvents.buttonTap.Invoke(button, buttonTapType);
	}
	public void ProcessTareData(byte[] bytes, int byteOffset = 0)
	{
		//StatusMessage = String.Format("ProcessTareData {0}", bytes.Length);
		tareCounter = bytes[byteOffset + 2];
		StatusMessage = String.Format("tare {0}", tareCounter);
		decentScaleEvents.tare.Invoke(tareCounter);
	}
}

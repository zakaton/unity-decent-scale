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
	[SerializeField]
	public int battery = 0;
	[SerializeField]
	public bool isUSB = false;

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
			string millisecondsString = milliseconds.ToString().PadLeft(4, '0');
			return String.Format("{0}:{1}:{2}", minutesString, secondsString, millisecondsString);
		}
	}
	public WeightTimestamp weightTimestamp = new();

	public enum DisplayWeightType { grams, ounces };
	[SerializeField]
	public DisplayWeightType displayWeightType = DisplayWeightType.grams;

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
		public UnityEvent<DisplayWeightType> weightType = new();
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

	public virtual void Update()
	{
		checkShowWeight();
		checkShowTimer();
		checkShowGrams();

		checkStartTimer();
		checkStopTimer();
		checkResetTimer();

		checkTare();
	}
	public virtual void Connect() { }
	public virtual void Disconnect() { }

	public void OnConnect()
	{
		SetLED();
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

	public void ProcessWeightData(byte[] bytes, int byteOffset = 0)
	{
		isStable = bytes[byteOffset++] == (int)Command.stableWeight;
		StatusMessage = String.Format("ProcessWeightData {0}", bytes.Length);
		// FILL
		decentScaleEvents.weight.Invoke(weight, isStable, weightTimestamp);
	}
	public void ProcessLEDData(byte[] bytes, int byteOffset = 0)
	{
		StatusMessage = String.Format("ProcessLEDData {0}", bytes.Length);
		// FILL
	}
	public void ProcessButtonTapData(byte[] bytes, int byteOffset = 0)
	{
		StatusMessage = String.Format("ProcessButtonTapData {0}", bytes.Length);
		// FILL
		//decentScaleEvents.buttonTap.Invoke();
	}
	public void ProcessTareData(byte[] bytes, int byteOffset = 0)
	{
		StatusMessage = String.Format("ProcessTareData {0}", bytes.Length);
		// FILL
		//decentScaleEvents.tare.Invoke();
	}
}

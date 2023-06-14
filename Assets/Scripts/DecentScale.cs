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
public class DecentScale
{
	public TMPro.TMP_Text loggerText;

	[HideInInspector]
	public bool IsConnected = false;
	[HideInInspector]
	public bool IsConnecting = false;


	[SerializeField]
	public bool autoConnect = true;

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
	public class DecentScaleEvents
	{
		public UnityEvent weightData = new();
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
	}

	public virtual void Update()
	{

	}
	public virtual void Connect() { }
	public virtual void Disconnect() { }

	public void ProcessWeightData(byte[] bytes)
	{
		StatusMessage = String.Format("ProcessWeightData {0}", bytes.Length);
		uint byteOffset = 0;

		decentScaleEvents.weightData.Invoke();
	}

}

using System;
using UnityEngine;
using System.Collections.Generic;

/*
    TODO
        *
*/

[Serializable]
public class TapStrapBLE : TapStrap
{
	static private string GENERATE_DATA_UUID(string value)
	{
		return String.Format("c3ff000{0}-1d8b-40fd-a56f-c7bd5d0f3370", value);
	}
	static private string GENERATE_SUPPORT_UUID(string value)
	{
		return String.Format("6e40000{0}-b5a3-f393-e0a9-e50e24dcca9e", value);
	}
	static bool IsEqual(string uuid1, string uuid2)
	{
		return (uuid1.ToUpper().Equals(uuid2.ToUpper()));
	}

	private static readonly string dataServiceUUID = GENERATE_DATA_UUID("1");
	private static readonly string tapDataCharacteristicUUID = GENERATE_DATA_UUID("5");
	private static readonly string mouseDataCharacteristicUUID = GENERATE_DATA_UUID("6");
	private static readonly string uiCommandCharacteristicUUID = GENERATE_DATA_UUID("9");
	private static readonly string airGestureDataCharacteristicUUID = GENERATE_DATA_UUID("a");

	private static readonly string supportServiceUUID = GENERATE_SUPPORT_UUID("1");
	private static readonly string tapModeCharacteristicUUID = GENERATE_SUPPORT_UUID("2");
	private static readonly string rawSensorsCharacteristicUUID = GENERATE_SUPPORT_UUID("3");

	private static string[] subscriptionCharacteristicUUIDs = { tapDataCharacteristicUUID, mouseDataCharacteristicUUID, airGestureDataCharacteristicUUID, rawSensorsCharacteristicUUID };

	private static Dictionary<string, string> characteristicUUIDToServiceUUID = new Dictionary<string, string> {
		{tapDataCharacteristicUUID, dataServiceUUID},
		{mouseDataCharacteristicUUID, dataServiceUUID},
		{uiCommandCharacteristicUUID, dataServiceUUID},
		{airGestureDataCharacteristicUUID, dataServiceUUID},

		{tapModeCharacteristicUUID, supportServiceUUID},
		{rawSensorsCharacteristicUUID, supportServiceUUID},
	};

	private float _timeout = 0f;
	private States _state = States.None;
	private string _deviceAddress;
	private bool _foundTapDataCharacteristicUUID = false;
	private bool _foundMouseDataCharacteristicUUID = false;
	private bool _foundUICommandCharacteristicUUID = false;
	private bool _foundAirGestureDataCharacteristicUUID = false;
	private bool _foundTapModeCharacteristicUUID = false;
	private bool _foundRawSensorsCharacteristicUUID = false;
	private bool _rssiOnly = false;
	private int _rssi = 0;
	void Reset()
	{
		IsConnected = false;
		_timeout = 0f;
		_state = States.None;
		_deviceAddress = null;
		_ClearCharacteristicUUIDFlags();
		_rssi = 0;
	}

	private void _ClearCharacteristicUUIDFlags()
	{
		_foundTapDataCharacteristicUUID = false;
		_foundMouseDataCharacteristicUUID = false;
		_foundUICommandCharacteristicUUID = false;
		_foundAirGestureDataCharacteristicUUID = false;
		_foundTapModeCharacteristicUUID = false;
		_foundRawSensorsCharacteristicUUID = false;
	}

	private bool _DidFindAllCharacteristics()
	{
		return _foundTapDataCharacteristicUUID && _foundMouseDataCharacteristicUUID && _foundUICommandCharacteristicUUID && _foundAirGestureDataCharacteristicUUID && _foundTapModeCharacteristicUUID && _foundTapModeCharacteristicUUID && _foundRawSensorsCharacteristicUUID;
	}

	enum States
	{
		None,
		Scan,
		ScanRSSI,
		ReadRSSI,
		Connect,
		RequestMTU,
		Subscribe,
		Unsubscribe,
		Disconnect,
		StopScan
	}
	void SetState(States newState, float timeout)
	{
		_state = newState;
		_timeout = timeout;
	}

	// Start is called before the first frame update
	public override void Start()
	{
		base.Start();
		if (autoConnect)
		{
			Connect();
		}
	}

	public override void _UpdateInputMode()
	{
		if (!IsConnected)
		{
			return;
		}

		byte[] data = inputModeCode;
		logger.Log(string.Join(", ", data));

		BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, supportServiceUUID, tapModeCharacteristicUUID, data, data.Length, true, (characteristicUUID) =>
		{
			BluetoothLEHardwareInterface.Log("Write Succeeded");
		});
	}

	public override void Connect()
	{
		if (IsConnected)
		{
			return;
		}

		IsConnecting = true;
		connectionEvents.onConnecting.Invoke();

		Reset();
		try
		{

			BluetoothLEHardwareInterface.Initialize(true, false, () =>
			{
				SetState(States.Scan, 0.1f);
			}, (error) =>
			{
				StatusMessage = "Error during initialize: " + error;
			});
		}
		catch (Exception e)
		{
			StatusMessage = e.Message;
		}
	}
	public override void Disconnect()
	{
		if (IsConnected)
		{
			SetState(States.Disconnect, 0.1f);
		}
		else
		{
			Debug.Log(String.Format("state: {0}", _state));
			if (_state == States.Scan)
			{
				SetState(States.StopScan, 0.1f);
			}
		}
	}

	private void OnData(string characteristicUUID, byte[] bytes)
	{
		switch (characteristicUUID)
		{
			case var value when IsEqual(value, tapDataCharacteristicUUID):
				ProcessTapData(bytes);
				break;
			case var value when IsEqual(value, mouseDataCharacteristicUUID):
				ProcessMouseData(bytes);
				break;
			case var value when IsEqual(value, airGestureDataCharacteristicUUID):
				ProcessAirGestureData(bytes);
				break;
			case var value when IsEqual(value, rawSensorsCharacteristicUUID):
				ProcessRawData(bytes);
				break;
			default:
				StatusMessage = String.Format("uncaught characteristicUUID: {0}", characteristicUUID);
				break;
		}
	}

	// Update is called once per frame
	public void Update()
	{
		base.Update();

		CheckUpdateInputMode();

		if (_timeout > 0f)
		{
			_timeout -= Time.deltaTime;
			if (_timeout <= 0f)
			{
				_timeout = 0f;

				switch (_state)
				{
					case States.None:
						break;

					case States.Scan:
						StatusMessage = "Scanning for " + deviceName;

						BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
						{
							// if your device does not advertise the rssi and manufacturer specific data
							// then you must use this callback because the next callback only gets called
							// if you have manufacturer specific data

							if (!_rssiOnly)
							{
								if (name.Contains(deviceName))
								{
									StatusMessage = "Found " + name;

									// found a device with the name we want
									// this example does not deal with finding more than one
									_deviceAddress = address;
									SetState(States.Connect, 0.5f);
								}
							}

						}, (address, name, rssi, bytes) =>
						{

							// use this one if the device responses with manufacturer specific data and the rssi

							if (name.Contains(deviceName))
							{
								StatusMessage = "Found " + name;

								if (_rssiOnly)
								{
									_rssi = rssi;
								}
								else
								{
									// found a device with the name we want
									// this example does not deal with finding more than one
									_deviceAddress = address;
									SetState(States.Connect, 0.5f);
								}
							}

						}, _rssiOnly); // this last setting allows RFduino to send RSSI without having manufacturer data

						if (_rssiOnly)
							SetState(States.ScanRSSI, 0.5f);
						break;

					case States.ScanRSSI:
						break;

					case States.ReadRSSI:
						//StatusMessage = $"Call Read RSSI";
						BluetoothLEHardwareInterface.ReadRSSI(_deviceAddress, (address, rssi) =>
						{
							//StatusMessage = $"Read RSSI: {rssi}";
						});
						SetState(States.ReadRSSI, 2f);
						break;

					case States.Connect:
						StatusMessage = "Connecting...";

						_ClearCharacteristicUUIDFlags();
						// note that the first parameter is the address, not the name. I have not fixed this because
						// of backwards compatiblity.
						// also note that I am note using the first 2 callbacks. If you are not looking for specific characteristics you can use one of
						// the first 2, but keep in mind that the device will enumerate everything and so you will want to have a timeout
						// large enough that it will be finished enumerating before you try to subscribe or do any other operations.
						BluetoothLEHardwareInterface.ConnectToPeripheral(_deviceAddress, null, null, (address, serviceUUID, characteristicUUID) =>
						{
							BluetoothLEHardwareInterface.StopScan();

							if (IsEqual(serviceUUID, dataServiceUUID))
							{
								StatusMessage = "Found Data Service UUID";

								_foundTapDataCharacteristicUUID = _foundTapDataCharacteristicUUID || IsEqual(characteristicUUID, tapDataCharacteristicUUID);
								_foundMouseDataCharacteristicUUID = _foundMouseDataCharacteristicUUID || IsEqual(characteristicUUID, mouseDataCharacteristicUUID);
								_foundUICommandCharacteristicUUID = _foundUICommandCharacteristicUUID || IsEqual(characteristicUUID, uiCommandCharacteristicUUID);
								_foundAirGestureDataCharacteristicUUID = _foundAirGestureDataCharacteristicUUID || IsEqual(characteristicUUID, airGestureDataCharacteristicUUID);
							}

							if (IsEqual(serviceUUID, supportServiceUUID))
							{
								StatusMessage = "Found Support Service UUID";

								_foundTapModeCharacteristicUUID = _foundTapModeCharacteristicUUID || IsEqual(characteristicUUID, tapModeCharacteristicUUID);
								_foundRawSensorsCharacteristicUUID = _foundRawSensorsCharacteristicUUID || IsEqual(characteristicUUID, rawSensorsCharacteristicUUID);
							}

							// if we have found the characteristics that we are waiting for
							// set the state. make sure there is enough timeout that if the
							// device is still enumerating other characteristics it finishes
							// before we try to subscribe
							if (!IsConnected && _DidFindAllCharacteristics())
							{
								StatusMessage = "Found all Characteristics!";
								IsConnected = true;
								IsConnecting = false;
								connectionEvents.onConnect.Invoke();
								SetState(States.RequestMTU, 2f);
							}
						});
						break;

					case States.RequestMTU:
						StatusMessage = "Requesting MTU";

						BluetoothLEHardwareInterface.RequestMtu(_deviceAddress, 185, (address, newMTU) =>
						{
							StatusMessage = "MTU set to " + newMTU.ToString();

							SetState(States.Subscribe, 0.1f);
						});
						break;

					case States.Subscribe:
						StatusMessage = "Subscribing to characteristics...";

						for (int index = 0; index < subscriptionCharacteristicUUIDs.Length; index++)
						{
							string characteristicUUID = subscriptionCharacteristicUUIDs[index];
							string serviceUUID = characteristicUUIDToServiceUUID[characteristicUUID];

							StatusMessage = String.Format("suscribing to #{0}{1}", index, characteristicUUID);
							BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, serviceUUID, characteristicUUID, (notifyAddress, notifyCharacteristic) =>
					{
						_state = States.None;

						SetState(States.ReadRSSI, 1f);

					}, (address, characteristicUUID, bytes) =>
					{
						if (_state != States.None)
						{
							// some devices do not properly send the notification state change which calls
							// the lambda just above this one so in those cases we don't have a great way to
							// set the state other than waiting until we actually got some data back.
							// The esp32 sends the notification above, but if yuor device doesn't you would have
							// to send data like pressing the button on the esp32 as the sketch for this demo
							// would then send data to trigger this.
							SetState(States.ReadRSSI, 1f);
						}

						// we received some data from the device
						OnData(characteristicUUID, bytes);
					});
						}

						StatusMessage = "Finished Subscribing to characteristics!";
						break;

					case States.Unsubscribe:
						for (int index = 0; index < subscriptionCharacteristicUUIDs.Length; index++)
						{
							string characteristicUUID = subscriptionCharacteristicUUIDs[index];
							string serviceUUID = characteristicUUIDToServiceUUID[characteristicUUID];

							BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, serviceUUID, characteristicUUID, null);
						}

						SetState(States.Disconnect, 4f);
						break;

					case States.Disconnect:
						StatusMessage = "Commanded disconnect.";

						if (IsConnected)
						{
							BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
							{
								StatusMessage = "Device disconnected";
								BluetoothLEHardwareInterface.DeInitialize(() =>
															{
																IsConnected = false;
																_state = States.None;
																connectionEvents.onDisconnect.Invoke();
															});
							});
						}
						else
						{
							BluetoothLEHardwareInterface.DeInitialize(() =>
							{
								_state = States.None;
							});
						}
						break;
				}
			}
		}
	}
}

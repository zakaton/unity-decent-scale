using System;
using UnityEngine;
using System.Collections.Generic;

/*
    TODO
			*
*/

[Serializable]
public class DecentScaleBLE : DecentScale
{
	[HideInInspector]
	private string deviceName = "Decent Scale";

	// https://github.com/zakaton/decent-scale-web-sdk/blob/main/script/DecentScale.js#L95
	static private string GENERATE_MAIN_UUID(string value)
	{
		return value;
	}
	static private string GENERATE_FULL_MAIN_UUID(string value)
	{
		return String.Format("0000{0}-0000-1000-8000-00805f9b34fb", value);
	}
	static bool IsEqual(string uuid1, string uuid2)
	{
		if (uuid1.Length > 4)
		{
			uuid1 = uuid1.Substring(4, 4);
		}
		if (uuid2.Length > 4)
		{
			uuid2 = uuid2.Substring(4, 4);
		}
		return (uuid1.ToUpper().Equals(uuid2.ToUpper()));
	}

	private static readonly string mainServiceUUID = GENERATE_MAIN_UUID("fff0");
	private static readonly string commandCharacteristicUUID = GENERATE_MAIN_UUID("36f5");
	private static readonly string dataCharacteristicUUID = GENERATE_MAIN_UUID("fff4");

	private static string[] subscriptionCharacteristicUUIDs = { dataCharacteristicUUID };

	private static Dictionary<string, string> characteristicUUIDToServiceUUID = new Dictionary<string, string> {
		{dataCharacteristicUUID, mainServiceUUID}
	};

	private float _timeout = 0f;
	private States _state = States.None;
	private string _deviceAddress;
	private bool _foundCommandCharacteristicUUID = false;
	private bool _foundDataCharacteristicUUID = false;
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
		_foundDataCharacteristicUUID = false;
		_foundCommandCharacteristicUUID = false;
	}

	private bool _DidFindAllCharacteristics()
	{
		return _foundDataCharacteristicUUID && _foundCommandCharacteristicUUID;
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
		if (IsEqual(characteristicUUID, dataCharacteristicUUID))
		{
			int byteOffset = 0;
			if (bytes[0] == 0)
			{
				byteOffset += 2;
			}
			var command = (Command)bytes[byteOffset + 1];
			switch (command)
			{
				case Command.led:
					ProcessLEDData(bytes, byteOffset);
					break;
				case Command.stableWeight:
				case Command.unstableWeight:
					ProcessWeightData(bytes, byteOffset);
					break;
				case Command.tare:
					ProcessTareData(bytes, byteOffset);
					break;
				case Command.buttonTap:
					ProcessButtonTapData(bytes, byteOffset);
					break;
				default:
					StatusMessage = String.Format("uncaught characteristicUUID: {0}", characteristicUUID);
					break;
			}
		}
	}

	private bool isSendingCommand = false;
	public override void sendCommand(List<byte> commandData, bool overrideSend = false)
	{
		if (IsConnected)
		{
			if (!overrideSend && (isSendingCommand || commands.Count > 0))
			{
				StatusMessage = "Pushing command to buffer...";
				commands.Add(commandData);
			}
			else
			{
				isSendingCommand = true;
				formatCommandData(commandData);
				var data = commandData.ToArray();
				StatusMessage = "sending command...";
				var serviceUUID = mainServiceUUID;
				if (serviceUUID.Length == 4)
				{
					serviceUUID = GENERATE_FULL_MAIN_UUID(serviceUUID);
				}
				var characreristicUUID = commandCharacteristicUUID;
				if (characreristicUUID.Length == 4)
				{
					characreristicUUID = GENERATE_FULL_MAIN_UUID(characreristicUUID);
				}
				BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, serviceUUID, characreristicUUID, data, data.Length, true, (characteristicUUID) =>
				{
					BluetoothLEHardwareInterface.Log("Write Succeeded");
					isSendingCommand = false;
					if (commands.Count > 0)
					{
						var nextCommand = commands[0];
						commands.Remove(nextCommand);
						StatusMessage = "sending next command...";
						sendCommand(nextCommand, true);
					}
				});
			}
		}
		else
		{
			StatusMessage = "unable to send command...";
		}
	}

	// Update is called once per frame
	public override void Update()
	{
		base.Update();

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
							StatusMessage = String.Format("serviceUUID: {0}", serviceUUID);
							BluetoothLEHardwareInterface.StopScan();


							if (IsEqual(serviceUUID, mainServiceUUID))
							{
								StatusMessage = "Found main Service UUID";

								_foundCommandCharacteristicUUID = _foundCommandCharacteristicUUID || IsEqual(characteristicUUID, commandCharacteristicUUID);
								_foundDataCharacteristicUUID = _foundDataCharacteristicUUID || IsEqual(characteristicUUID, dataCharacteristicUUID);
							}

							// if we have found the characteristics that we are waiting for
							// set the state. make sure there is enough timeout that if the
							// device is still enumerating other characteristics it finishes
							// before we try to subscribe
							if (!IsConnected && _DidFindAllCharacteristics())
							{
								StatusMessage = "Found all Characteristics!";
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
							StatusMessage = String.Format("suscribing to #{0}: {1}", index, characteristicUUID);
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
						IsConnected = true;
						IsConnecting = false;
						connectionEvents.onConnect.Invoke();
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

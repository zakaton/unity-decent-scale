using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    TODO
        trigger haptics
        set sensitivity
*/

public class TapStrapBLEMonoBehavior : MonoBehaviour
{
  void Awake()
  {
    Debug.Log("Initialized");
  }

  [SerializeField]
  public TapStrapBLE tapStrap;

  public void Connect()
  {
    tapStrap.Connect();
  }

  private void _StartUpdatingInputMode()
  {
    StartCoroutine(tapStrap.updateInputModeCoroutine);
  }
  private void _StopUpdatingInputMode()
  {
    StopCoroutine(tapStrap.updateInputModeCoroutine);
  }
  private void ResetCoroutine()
  {
    StopCoroutine(tapStrap.updateInputModeCoroutine);
    StartCoroutine(tapStrap.updateInputModeCoroutine);
  }

  // Start is called before the first frame update
  void Start()
  {
    tapStrap.Start();
    tapStrap.connectionEvents.onConnect.AddListener(_StartUpdatingInputMode);
    tapStrap.connectionEvents.onDisconnect.AddListener(_StopUpdatingInputMode);
  }

  // Update is called once per frame
  void Update()
  {
    tapStrap.Update();
    if (tapStrap.resetCoroutine && tapStrap.IsConnected)
    {
      ResetCoroutine();
      tapStrap.resetCoroutine = false;
    }
  }
}

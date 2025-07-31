using UnityEngine;

public class ActiveDebug : MonoBehaviour
{
    private void OnEnable() { Debug.Log("[ActiveDebug] ResourcePanelUI enabled", this); }
    private void OnDisable() { Debug.Log("[ActiveDebug] ResourcePanelUI disabled", this); }
}
using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight always-active runner for coroutines when original object is inactive.
/// </summary>
public class CoroutineRelay : MonoBehaviour
{
    private static CoroutineRelay _instance;
    public static CoroutineRelay Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("~CoroutineRelay");
                go.hideFlags = HideFlags.HideAndDontSave;
                _instance = go.AddComponent<CoroutineRelay>();
            }
            return _instance;
        }
    }

    public void Run(IEnumerator routine)
    {
        StartCoroutine(routine);
    }
}

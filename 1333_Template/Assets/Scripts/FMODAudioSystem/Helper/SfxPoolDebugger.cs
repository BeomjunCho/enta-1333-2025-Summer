#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Editor-only overlay that inspects a <see cref="SfxPlayerPool"/> at runtime
/// and prints key statistics: active/idle player counts, cached EventInstances,
/// FMOD CPU & memory usage, release-routine progress, etc.
/// </summary>
[DefaultExecutionOrder(9999)] // draw after most UI
public class SfxPoolDebugger : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("Target Pool (auto-filled if null)")]
    [SerializeField] private SfxPlayerPool _pool = null;

    [Header("Sampling")]
    [Tooltip("Seconds between stat refreshes.")]
    [SerializeField, Min(0.1f)] private float _sampleInterval = 1f;

    [Header("GUI")]
    [SerializeField] private bool _showOverlay = true;
    [SerializeField] private Vector2 _overlayPos = new(10f, 10f);

    /* ------------------------------------------------------------------ */
    /*  Internal State                                                    */
    /* ------------------------------------------------------------------ */

    private float _timer;
    private int _active;
    private int _idle;
    private int _totalPlayers;
    private int _cachedInstances;
    private float _fmodCpuDsp;
    private float _fmodCpuStudio;
    private uint _fmodMemCurrent;
    private uint _fmodMemPeak;

    /* ------------------------------------------------------------------ */
    /*  Unity                                                              */
    /* ------------------------------------------------------------------ */

    private void Awake()
    {
        if (_pool == null)
            _pool = this.gameObject.GetComponent<SfxPlayerPool>();
        if (_pool == null)
        {
            Debug.LogWarning($"{nameof(SfxPoolDebugger)}: No SfxPlayerPool found.");
            enabled = false;
        }
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer >= _sampleInterval)
        {
            _timer = 0f;
            SampleStats();
        }
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;

        var r = new Rect(_overlayPos.x, _overlayPos.y, 340f, 140f);
        GUI.Box(r, GUIContent.none);

        GUILayout.BeginArea(r);
        GUILayout.Label("<b>SFX Pool Debug</b>", GetStyle());
        GUILayout.Label($"Players  : Active {_active}  | Idle {_idle}  | Total {_totalPlayers}");
        GUILayout.Label($"Instances: Cached {_cachedInstances}");
        GUILayout.EndArea();
    }

    /* ------------------------------------------------------------------ */
    /*  Helpers                                                            */
    /* ------------------------------------------------------------------ */

    private void SampleStats()
    {
        // 1) Pool stats (reflection-safe)
        _active = GetPrivateField<List<SfxPlayer>>(_pool, "_active")?.Count ?? 0;
        _idle = GetPrivateField<Queue<SfxPlayer>>(_pool, "_idle")?.Count ?? 0;
        _totalPlayers = GetPrivateField<List<SfxPlayer>>(_pool, "_players")?.Count ?? 0;

        var map = GetPrivateField<Dictionary<EventReference, Stack<EventInstance>>>(_pool, "_instancePool");
        _cachedInstances = 0;
        if (map != null)
            foreach (var s in map.Values) _cachedInstances += s.Count;
    }

    private static T GetPrivateField<T>(object obj, string field) where T : class
    {
        var fi = obj.GetType().GetField(field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return fi?.GetValue(obj) as T;
    }

    private static GUIStyle GetStyle()
    {
        var st = new GUIStyle(GUI.skin.label) { richText = true };
        return st;
    }
}
#endif

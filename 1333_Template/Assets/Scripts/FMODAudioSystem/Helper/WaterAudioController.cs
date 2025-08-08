using UnityEngine;

/// <summary>
/// Attached to the player camera.  
/// ▸ Detects entry into Water-layer colliders.  
/// ▸ Notifies <see cref="LakeAudioManager"/> to start playback for that lake.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class WaterAudioController : MonoBehaviour
{
    /* ------------------------------------------------------------------ */
    /*  Inspector                                                         */
    /* ------------------------------------------------------------------ */

    [Header("Water layer")]
    [SerializeField] private LayerMask _waterLayer = 0;

    private LakeAudioManager _lakeAudioManager = null;
    /* ------------------------------------------------------------------ */
    /*  Internal                                                          */
    /* ------------------------------------------------------------------ */

    private void Awake()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (!col.isTrigger)
            col.isTrigger = true;
    }

    private void Start()
    {
        _lakeAudioManager = FindAnyObjectByType<LakeAudioManager>();
        if (_lakeAudioManager == null) Debug.LogError("WaterAudioController: _lakeAudioManager is null!");
    }

    /// <summary>
    /// Starts the lake’s audio when entering any Water collider.
    /// </summary>
    /// <param name="other">Collider entered.</param>
    private void OnTriggerEnter(Collider other)
    {
        if (IsWater(other.gameObject.layer))
        {
            int lakeId = _lakeAudioManager.GetLakeId(other.transform.position);
            _lakeAudioManager.EnsureLakePlaying(lakeId);
        }
    }

    /* ------------------------------------------------------------------ */
    /*  Helpers                                                           */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Checks if a layer is inside <see cref="_waterLayer"/>.
    /// </summary>
    /// <param name="layer">Layer index.</param>
    /// <returns>True when Water.</returns>
    private bool IsWater(int layer)
    {
        return (_waterLayer & (1 << layer)) != 0;
    }
}

using UnityEngine;

/// <summary>
/// Castle building that triggers game over when destroyed.
/// Inherits core building functionality from BuildingBase.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class BuildingCastle : BuildingBase
{
    [Header("Game Over")]
    [Tooltip("If true, destruction of this building triggers game over.")]
    public bool isCastle = true;

    /// <summary>
    /// Cache references and initialize health.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        // Apply team material on spawn
        ApplyTeamMaterial();
    }

    /// <summary>
    /// Called when this building is selected by the player.
    /// Shows the health bar.
    /// </summary>
    public override void OnSelected()
    {
        ShowHpBar();
    }

    /// <summary>
    /// Called when this building is deselected by the player.
    /// Hides the health bar.
    /// </summary>
    public override void OnDeselected()
    {
        HideHpBar();
    }

    /// <summary>
    /// Destroys this building.
    /// Triggers game over if this is the castle.
    /// </summary>
    public override void DestroySelf()
    {
        // If this is the castle, fire game over event before destruction
        if (isCastle)
        {
            BuildingEvents.RaiseCastleDestroyed(this);
        }

        base.DestroySelf();
    }
}

/// <summary>
/// Centralized events for building-related global notifications.
/// </summary>
public static class BuildingEvents
{
    /// <summary>
    /// Invoked when the castle building is destroyed.
    /// </summary>
    public static event System.Action<BuildingCastle> OnCastleDestroyed;

    /// <summary>
    /// Helper to invoke the OnCastleDestroyed event.
    /// </summary>
    public static void RaiseCastleDestroyed(BuildingCastle castle)
    {
        OnCastleDestroyed?.Invoke(castle);
    }
}

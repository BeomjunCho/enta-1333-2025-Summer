using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel for a selected resource-producing building.
/// Shows building name / description + resource icon / production rate
/// and offers a destroy button.
/// </summary>
public class ResourceBuildingSelectedUI : MonoBehaviour
{
    [Header("Building UI")]
    [Tooltip("TMP text that shows the building name.")]
    [SerializeField] private TMP_Text _buildingTitleText = null;

    [Tooltip("TMP text that shows the building description.")]
    [SerializeField] private TMP_Text _buildingDescText = null;

    [Header("Resource UI")]
    [Tooltip("Image that shows the produced resource icon.")]
    [SerializeField] private Image _resourceIconImage = null;

    [Tooltip("TMP text that shows resource display name.")]
    [SerializeField] private TMP_Text _resourceNameText = null;

    [Tooltip("TMP text that shows production rate.")]
    [SerializeField] private TMP_Text _productionText = null;

    [Header("Other")]
    [Tooltip("Button that destroys the building.")]
    [SerializeField] private Button _destroyButton = null;

    // currently selected building
    private BuildingResource _building;

    /* ------------------------------------------------------------------ */
    /*  Public API                                                         */
    /* ------------------------------------------------------------------ */

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
    /// <summary>
    /// Binds the resource building and fills every UI field.
    /// </summary>
    public void Bind(BuildingResource building, BuildingDataSO buildingData)
    {
        _building = building;

        if (_building == null ||
            buildingData == null ||
            _building.ResourceType == null)
        {
            // it is castle and it is fine.
            if (buildingData.BuildingName == "Castle")
            {
                Debug.Log("Castle");
                return;
            }
            // Missing reference
            Debug.LogError("ResourceBuildingSelectedUI: Missing references.");
            return;
        }

        /* ---------- building texts ---------- */
        _buildingTitleText.text = buildingData.BuildingName;
        _buildingDescText.text = buildingData.Description;

        /* ---------- resource section ---------- */
        ResourceDataSO resourceType = _building.ResourceType;

        _resourceIconImage.sprite = resourceType.Icon;
        _resourceNameText.text = resourceType.DisplayName;

        int amt = _building.ProductionAmount;
        float interval = _building.ProductionInterval;
        string resourceName = resourceType.DisplayName;

        _productionText.text = $"Produces {amt} {resourceName} every {interval:F1}s";

        /* ---------- destroy button ---------- */
        _destroyButton.onClick.RemoveAllListeners();
        _destroyButton.onClick.AddListener(() =>
        {
            _building.DestroySelf();
            Hide();
        });

        Show();
    }

    /// <summary>Clears stored reference and hides the panel.</summary>
    public void Clear()
    {
        _building = null;
        _destroyButton.onClick.RemoveAllListeners();
        Hide();
    }
}

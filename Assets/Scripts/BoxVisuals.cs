using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(DeliveryBox))]
public class BoxVisuals : MonoBehaviour
{
    [Tooltip("Drag empty GameObjects here to represent where each item should visually sit inside the box")]
    public List<Transform> itemSlots;

    [Tooltip("Scale multiplier for items in the box. Use e.g. 0.5 to make them half size so they fit.")]
    public float itemScale = 0.5f;

    private DeliveryBox box;
    private List<GameObject> spawnedVisuals = new List<GameObject>();

    private void Awake()
    {
        box = GetComponent<DeliveryBox>();
    }

    private void OnEnable()
    {
        box.OnItemCountChanged += UpdateVisuals;
        box.OnBoxStateChanged += HandleStateChange;
    }

    private void OnDisable()
    {
        box.OnItemCountChanged -= UpdateVisuals;
        box.OnBoxStateChanged -= HandleStateChange;
    }

    private void HandleStateChange(bool isOpen)
    {
        UpdateVisuals(box.itemCount.Value);
    }

    private void UpdateVisuals(int itemCount)
    {
        // Only show items if the box is actually open!
        if (ProductManager.Instance == null || !box.isOpen.Value) return;

        GameObject prefab = ProductManager.Instance.GetPrefab(box.productType.Value);
        if (prefab == null) return;

        // Add visual items up to the current count
        while (spawnedVisuals.Count < itemCount && spawnedVisuals.Count < itemSlots.Count)
        {
            Transform slot = itemSlots[spawnedVisuals.Count];
            GameObject visual = Instantiate(prefab, slot.position, slot.rotation, slot);
            
            // Apply our scale multiplier to the prefab's original scale
            visual.transform.localScale = prefab.transform.localScale * itemScale;
            
            spawnedVisuals.Add(visual);
        }

        // Remove visual items if they were taken out
        while (spawnedVisuals.Count > itemCount)
        {
            GameObject visual = spawnedVisuals[spawnedVisuals.Count - 1];
            spawnedVisuals.RemoveAt(spawnedVisuals.Count - 1);
            Destroy(visual);
        }
    }
}

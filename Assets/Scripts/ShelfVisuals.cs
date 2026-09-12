using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Shelf))]
public class ShelfVisuals : MonoBehaviour
{
    [Tooltip("Drag empty GameObjects here to represent where each item should visually sit on the shelf")]
    public List<Transform> itemSlots; 

    private Shelf shelf;
    private List<GameObject> spawnedVisuals = new List<GameObject>();

    private void Awake()
    {
        shelf = GetComponent<Shelf>();
    }

    private void OnEnable()
    {
        shelf.OnStockChanged += UpdateVisuals;
    }

    private void OnDisable()
    {
        shelf.OnStockChanged -= UpdateVisuals;
    }

    private void UpdateVisuals(int currentStock)
    {
        if (ProductManager.Instance == null) return;

        GameObject prefab = ProductManager.Instance.GetPrefab(shelf.acceptedProductType);
        if (prefab == null) return;

        // Add items if stock increased
        while (spawnedVisuals.Count < currentStock && spawnedVisuals.Count < itemSlots.Count)
        {
            Transform slot = itemSlots[spawnedVisuals.Count];
            GameObject visual = Instantiate(prefab, slot.position, slot.rotation, slot);
            spawnedVisuals.Add(visual);
        }

        // Remove items if stock decreased
        while (spawnedVisuals.Count > currentStock)
        {
            GameObject visual = spawnedVisuals[spawnedVisuals.Count - 1];
            spawnedVisuals.RemoveAt(spawnedVisuals.Count - 1);
            Destroy(visual);
        }
    }
}

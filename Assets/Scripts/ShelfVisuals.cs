using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Shelf))]
public class ShelfVisuals : MonoBehaviour
{
    [Tooltip("Drag empty GameObjects here to represent where each item should visually sit on the shelf")]
    public List<Transform> itemSlots; 
    
    [Tooltip("Scale multiplier for items on the shelf to make them more noticeable")]
    public float itemScale = 1.0f;

    [Tooltip("Vertical offset to elevate the items so they don't clip into the shelf")]
    public float elevationOffset = 0.0f;

    [Header("Visual Polish")]
    public bool addRandomJitter = true;
    public bool animatePopIn = true;

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
            
            // Apply elevation offset along the slot's local UP direction
            Vector3 spawnPos = slot.position + (slot.up * elevationOffset);
            
            GameObject visual = Instantiate(prefab, spawnPos, slot.rotation, slot);
            
            // 1. Calculate Target Scale
            Vector3 targetScale = prefab.transform.localScale * itemScale;
            
            // 2. Add slight random jitter so shelves look organic and not perfectly robotic
            if (addRandomJitter)
            {
                visual.transform.Rotate(0, Random.Range(-5f, 5f), 0, Space.Self);
                visual.transform.position += visual.transform.right * Random.Range(-0.02f, 0.02f);
            }

            // 3. Apply Scale / Pop-in Animation
            if (animatePopIn)
            {
                visual.transform.localScale = Vector3.zero; // Start tiny
                StartCoroutine(PopInRoutine(visual.transform, targetScale));
            }
            else
            {
                visual.transform.localScale = targetScale;
            }

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
    private IEnumerator PopInRoutine(Transform visualTransform, Vector3 finalScale)
    {
        float timer = 0f;
        float duration = 0.25f;
        
        while (timer < duration && visualTransform != null)
        {
            timer += Time.deltaTime;
            float percent = timer / duration;
            
            // A simple overshoot "bounce" curve
            float bounce = Mathf.Sin(percent * Mathf.PI * 0.8f) * 1.2f;
            if (percent > 0.8f) bounce = Mathf.Lerp(bounce, 1f, (percent - 0.8f) * 5f);
            
            visualTransform.localScale = finalScale * bounce;
            yield return null;
        }

        if (visualTransform != null)
        {
            visualTransform.localScale = finalScale;
        }
    }
}

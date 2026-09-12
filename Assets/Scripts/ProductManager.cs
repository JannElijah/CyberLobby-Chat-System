using UnityEngine;
using System.Collections.Generic;

public class ProductManager : MonoBehaviour
{
    public static ProductManager Instance { get; private set; }

    [System.Serializable]
    public struct ProductEntry
    {
        public ProductType type;
        public GameObject visualPrefab; // Your grocery item prefab
    }

    [Tooltip("Map each Product Type to its visual 3D Prefab here")]
    public List<ProductEntry> products = new List<ProductEntry>();

    private void Awake()
    {
        // Simple Singleton pattern so any script can easily grab the prefabs
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public GameObject GetPrefab(ProductType type)
    {
        foreach (var p in products)
        {
            if (p.type == type) return p.visualPrefab;
        }
        return null;
    }
}

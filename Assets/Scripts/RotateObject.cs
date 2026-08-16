using UnityEngine;

public class RotateObject : MonoBehaviour
{
    void Update()
    {
        // Rotates the object constantly every frame
        transform.Rotate(0, 50 * Time.deltaTime, 0); 
    }
}
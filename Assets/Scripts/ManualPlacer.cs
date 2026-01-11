using UnityEngine;

public class ManualPlacer : MonoBehaviour
{
    [Tooltip("Distance in front of the user's head while placing")]
    public float distanceFromHead = 1.5f;

    // Start in locked mode; toggle to move
    private bool isPlacing = false;

    // Optional: see in Inspector if it's currently placing
    public bool IsPlacing => isPlacing;

    // Call this from a UI button or input event
    public void TogglePlacement()
    {
        isPlacing = !isPlacing;
    }

    private void Update()
    {
        if (!isPlacing) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Put this object in front of the user's head
        Vector3 targetPos = cam.transform.position + cam.transform.forward * distanceFromHead;
        transform.position = targetPos;

        // Make it face the user (but keep it upright)
        Vector3 lookDir = transform.position - cam.transform.position;
        lookDir.y = 0f; // keep level, optional
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }

        // ---- OPTIONAL: for testing in Editor ----
        // Press Space to toggle placement on PC
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePlacement();
        }
    }
}

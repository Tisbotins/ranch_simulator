using UnityEngine;

public class RanchBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        transform.LookAt(transform.position + camera.transform.rotation * Vector3.forward,
            camera.transform.rotation * Vector3.up);
    }
}

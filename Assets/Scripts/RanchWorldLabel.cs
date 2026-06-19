using UnityEngine;

public class RanchWorldLabel : MonoBehaviour
{
    private Transform target;
    private Vector3 offset;
    private bool scaleHeight;

    public void Initialize(Transform followTarget, Vector3 worldOffset, bool useTargetScaleForHeight)
    {
        target = followTarget;
        offset = worldOffset;
        scaleHeight = useTargetScaleForHeight;
        UpdateTransform();
    }

    private void LateUpdate() => UpdateTransform();

    private void UpdateTransform()
    {
        if (target == null) { Destroy(gameObject); return; }
        float heightMultiplier = scaleHeight ? target.lossyScale.y : 1f;
        transform.position = target.position + new Vector3(offset.x, offset.y * heightMultiplier, offset.z);
        transform.localScale = Vector3.one;
        Camera camera = Camera.main;
        if (camera != null)
            transform.LookAt(transform.position + camera.transform.rotation * Vector3.forward,
                camera.transform.rotation * Vector3.up);
    }
}

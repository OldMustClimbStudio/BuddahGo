using UnityEngine;

public class IntroSlot : MonoBehaviour
{
    [System.Serializable]
    public struct VisualStyle
    {
        public float yawScale;
        public float rollScale;
        public float swayAmplitude;
        public float swayFrequency;
    }

    [Header("Identity")]
    [SerializeField] private int slotIndex;

    [Header("Launch Pose")]
    [SerializeField] private Transform launchPoint;
    [SerializeField] private Transform forwardReference;

    [Header("Path")]
    [SerializeField] private IntroPath introPath;

    [Header("Visual Tuning")]
    [SerializeField] private VisualStyle visualStyle = new VisualStyle
    {
        yawScale = 0.75f,
        rollScale = 10f,
        swayAmplitude = 0.06f,
        swayFrequency = 3.5f
    };

    [Header("Optional Root Offsets")]
    [SerializeField] private Vector3 rootPositionOffset;
    [SerializeField] private Vector3 rootEulerOffset;

    public int SlotIndex => slotIndex;
    public Transform LaunchPoint => launchPoint;
    public Transform ForwardReference => forwardReference;
    public IntroPath IntroPath => introPath;
    public VisualStyle SlotVisualStyle => visualStyle;
    public Vector3 RootPositionOffset => rootPositionOffset;
    public Quaternion RootRotationOffset => Quaternion.Euler(rootEulerOffset);

    public Vector3 GetLaunchPosition()
    {
        return launchPoint != null ? launchPoint.position : transform.position;
    }

    public Vector3 GetLaunchForward()
    {
        if (forwardReference != null)
            return forwardReference.forward.normalized;

        if (launchPoint != null)
            return launchPoint.forward.normalized;

        return transform.forward.normalized;
    }

    public void BindPathToSlot()
    {
        if (introPath != null)
            introPath.BindTerminalPose(launchPoint, forwardReference);
    }

    private void OnValidate()
    {
        BindPathToSlot();
    }
}

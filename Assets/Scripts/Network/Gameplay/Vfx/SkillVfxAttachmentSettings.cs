using UnityEngine;

public class SkillVfxAttachmentSettings : MonoBehaviour
{
    [Header("Attachment")]
    [SerializeField] private bool followTargetPosition = true;
    [SerializeField] private bool inheritTargetRotation = true;
    [SerializeField] private bool inheritTargetScale = false;

    public bool FollowTargetPosition => followTargetPosition;
    public bool InheritTargetRotation => inheritTargetRotation;
    public bool InheritTargetScale => inheritTargetScale;
}

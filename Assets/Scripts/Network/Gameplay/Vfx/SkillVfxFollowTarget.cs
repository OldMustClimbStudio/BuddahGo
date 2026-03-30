using UnityEngine;

public class SkillVfxFollowTarget : MonoBehaviour
{
    private Transform _target;
    private Vector3 _localOffset;
    private Quaternion _worldRotation;
    private bool _inheritRotation;
    private bool _inheritScale;
    private Vector3 _baseLocalScale = Vector3.one;

    public void Initialize(Transform target, Vector3 localOffset, Vector3 localEuler, bool inheritRotation, bool inheritScale)
    {
        _target = target;
        _localOffset = localOffset;
        _worldRotation = Quaternion.Euler(localEuler);
        _inheritRotation = inheritRotation;
        _inheritScale = inheritScale;
        _baseLocalScale = transform.localScale;
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (_target == null)
            return;

        transform.position = _target.TransformPoint(_localOffset);
        transform.rotation = _inheritRotation
            ? _target.rotation * _worldRotation
            : _worldRotation;

        if (_inheritScale)
            transform.localScale = Vector3.Scale(_baseLocalScale, _target.lossyScale);
    }
}

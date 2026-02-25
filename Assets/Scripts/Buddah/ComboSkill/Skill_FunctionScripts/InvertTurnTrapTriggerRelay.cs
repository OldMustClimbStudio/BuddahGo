using UnityEngine;

/// <summary>
/// Forwards trigger callbacks to the owning invert-turn trap zone effect.
/// </summary>
public class InvertTurnTrapTriggerRelay : MonoBehaviour
{
    private MovementInvertTurnTrapZoneEffect _owner;

    public void Initialize(MovementInvertTurnTrapZoneEffect owner)
    {
        _owner = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        _owner?.HandleTriggerTouch(other);
    }

    private void OnTriggerStay(Collider other)
    {
        _owner?.HandleTriggerTouch(other);
    }
}

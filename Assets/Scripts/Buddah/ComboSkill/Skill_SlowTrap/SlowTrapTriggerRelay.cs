using UnityEngine;

/// <summary>
/// Attach this to the trigger collider object (or let MovementSlowTrapZoneEffect add it at runtime).
/// Forwards trigger callbacks to the owning slow trap zone effect.
/// </summary>
public class SlowTrapTriggerRelay : MonoBehaviour
{
    private MovementSlowTrapZoneEffect _owner;

    public void Initialize(MovementSlowTrapZoneEffect owner)
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

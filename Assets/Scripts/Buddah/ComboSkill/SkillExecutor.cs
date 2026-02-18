using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class SkillExecutor : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private ComboSkillInput comboInput;
    [SerializeField] private SkillLoadout loadout;
    [SerializeField] private SkillDatabase database;

    private float[] _nextReadyTime; // server cooldown tracking

    private void Awake()
    {
        _nextReadyTime = new float[SkillLoadout.SlotCount];
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner) return;

        if (comboInput == null) comboInput = GetComponent<ComboSkillInput>();
        if (loadout == null) loadout = GetComponent<SkillLoadout>();

        if (comboInput != null)
        {
            comboInput.OnSkillSlotTriggered += OnSlotTriggeredByCombo;
        }
        else
        {
            Debug.LogWarning("[SkillExecutor] Missing ComboSkillInput reference.");
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (comboInput != null)
            comboInput.OnSkillSlotTriggered -= OnSlotTriggeredByCombo;
    }

    private void OnSlotTriggeredByCombo(int slotIndex, string comboName)
    {
        // Owner side request
        Debug.Log($"[SkillExecutor][Owner] Combo '{comboName}' triggered slot {slotIndex}, requesting cast...");
        RequestCast(slotIndex);
    }

    public void RequestCast(int slotIndex)
    {
        if (!IsOwner) return;
        CastSlotServerRpc(slotIndex);
    }

    [ServerRpc(RequireOwnership = true)]
    private void CastSlotServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SkillLoadout.SlotCount) return;
        if (loadout == null || database == null)
        {
            Debug.LogWarning("[SkillExecutor][Server] Missing loadout/database.");
            return;
        }

        string skillId = loadout.GetSkillId(slotIndex);
        if (string.IsNullOrWhiteSpace(skillId))
        {
            Debug.Log($"[SkillExecutor][Server] Slot {slotIndex} is empty, cast ignored.");
            return;
        }

        if (!database.TryGet(skillId, out SkillAction skill) || skill == null)
        {
            Debug.LogWarning($"[SkillExecutor][Server] Unknown skillId '{skillId}' (slot {slotIndex}).");
            return;
        }

        float now = (float)Time.time;
        if (now < _nextReadyTime[slotIndex])
        {
            Debug.Log($"[SkillExecutor][Server] Skill '{skillId}' on cooldown. Ready in {(_nextReadyTime[slotIndex] - now):0.00}s");
            return;
        }

        _nextReadyTime[slotIndex] = now + skill.cooldownSeconds;

        Debug.Log($"[SkillExecutor][Server] CAST '{skillId}' (slot {slotIndex})");
        skill.ExecuteServer(this, slotIndex);

        CastObserversRpc(slotIndex, skillId);
    }

    [ObserversRpc]
    private void CastObserversRpc(int slotIndex, string skillId)
    {
        if (database == null) return;

        if (database.TryGet(skillId, out SkillAction skill) && skill != null)
        {
            Debug.Log($"[SkillExecutor][Observers] '{skillId}' played (slot {slotIndex})");
            skill.ExecuteObservers(this, slotIndex);
        }
    }

    public void ApplyAccelerationToOwner(float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        if (!IsServerInitialized) return;

        NetworkConnection conn = Owner;
        if (conn == null) return;

        ApplyAccelerationTargetRpc(conn, extraForwardForce, extraMaxSpeed, durationSeconds);
    }

    [TargetRpc]
    private void ApplyAccelerationTargetRpc(NetworkConnection conn, float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        // 只在该玩家的 Owner 客户端执行
        var move = GetComponent<BuddahMovement>();
        if (move == null)
        {
            Debug.LogWarning("[SkillExecutor][Target] Missing BuddahMovement.");
            return;
        }

        var effect = GetComponent<MovementAccelerationEffect>();
        if (effect == null)
            effect = gameObject.AddComponent<MovementAccelerationEffect>();

        effect.ApplyOrRefresh(move, extraForwardForce, extraMaxSpeed, durationSeconds);

        Debug.Log($"[SkillExecutor][Target] Accel: +{extraForwardForce} forwardForce, +{extraMaxSpeed} maxSpeed for {durationSeconds}s");
    }



}

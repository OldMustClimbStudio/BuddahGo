using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class SkillLoadout : NetworkBehaviour
{
    public const int SlotCount = 3;

    // slotIndex -> skillId
    public readonly SyncList<string> SlotSkillIds = new SyncList<string>();

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Ensure 3 slots exist
        if (SlotSkillIds.Count != SlotCount)
        {
            SlotSkillIds.Clear();
            for (int i = 0; i < SlotCount; i++)
                SlotSkillIds.Add(string.Empty);
        }

        // 这里先默认给个技能，你可以在游戏里将以下改成实际的使用技能ID
        //注意技能ID就是玩家装备的技能
        SlotSkillIds[0] = "acceleration"; // For Debug给一个加速技能
        SlotSkillIds[1] = "debug";
        SlotSkillIds[2] = "debug";
    }

    public string GetSkillId(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return string.Empty;
        if (SlotSkillIds.Count < SlotCount) return string.Empty;
        return SlotSkillIds[slotIndex] ?? string.Empty;
    }

    /// <summary>
    /// Client calls this to request changing loadout from menu/UI.
    /// </summary>
    public void RequestSetSlot(int slotIndex, string skillId)
    {
        if (!IsOwner) return;
        SetSlotServerRpc(slotIndex, skillId);
    }

    [ServerRpc(RequireOwnership = true)]
    private void SetSlotServerRpc(int slotIndex, string skillId)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;

        // TODO: 这里可以做白名单校验、等级限制、解锁校验等
        SlotSkillIds[slotIndex] = skillId ?? string.Empty;

        Debug.Log($"[SkillLoadout][Server] Set slot {slotIndex} -> '{SlotSkillIds[slotIndex]}'");
    }
}

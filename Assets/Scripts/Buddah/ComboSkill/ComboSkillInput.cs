using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ComboSkillInput : NetworkBehaviour
{
    public enum Token { W, Up }

    [Serializable]
    public class ComboBinding
    {
        public string name = "Skill";
        public int slotIndex = 0;                 // 匹配后触发的技能槽位
        public Token[] sequence;                  // 例如：W Up W
    }

    [Header("Input")]
    [Tooltip("Use InputSystem_Actions -> Player -> HandPush (W / UpArrow).")]
    [SerializeField] private bool useGeneratedInputActions = true;

    [Header("Combo Window")]
    [Tooltip("Max allowed time between consecutive inputs. If exceeded, buffer clears.")]
    [SerializeField] private float stepWindowSeconds = 0.35f;

    [Tooltip("Max number of inputs kept in buffer.")]
    [SerializeField] private int maxBuffer = 6;

    [Header("Bindings")]
    [SerializeField] private List<ComboBinding> bindings = new();

    // 你后面可以用这个事件把“槽位 → 技能执行”接起来
    public event Action<int, string> OnSkillSlotTriggered; // (slotIndex, comboName)

    private InputSystem_Actions _actions;
    private InputAction _handPushAction;

    private readonly List<Token> _buffer = new();
    private float _lastInputTime = -999f;

    private void Awake()
    {
        if (useGeneratedInputActions)
        {
            _actions = new InputSystem_Actions();
            _handPushAction = _actions.Player.HandPush;
        }

        // 给你默认示例（你也可以在Inspector里配）
        if (bindings.Count == 0)
        {
            bindings.Add(new ComboBinding { name = "Skill1", slotIndex = 0, sequence = new[] { Token.W, Token.Up, Token.W } });
            bindings.Add(new ComboBinding { name = "Skill2", slotIndex = 1, sequence = new[] { Token.Up, Token.W, Token.Up } });
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
            return;

        _actions?.Enable();

        if (_handPushAction != null)
            _handPushAction.performed += OnHandPushPerformed;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        if (_handPushAction != null)
            _handPushAction.performed -= OnHandPushPerformed;

        _actions?.Disable();
    }

    private void OnDisable()
    {
        if (_handPushAction != null)
            _handPushAction.performed -= OnHandPushPerformed;

        _actions?.Disable();
    }

    private void OnHandPushPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsOwner)
            return;

        if (ctx.control is not KeyControl key)
            return;

        Token? t = key.keyCode switch
        {
            Key.W => Token.W,
            Key.UpArrow => Token.Up,
            _ => null
        };

        if (t == null)
            return;

        PushToken(t.Value);
    }

    private void PushToken(Token t)
    {
        float now = Time.time;

        if (now - _lastInputTime > stepWindowSeconds)
        {
            Debug.Log($"[Combo] window expired ({now - _lastInputTime:0.00}s), clearing buffer");
            _buffer.Clear();
        }

        _lastInputTime = now;
        _buffer.Add(t);
        if (_buffer.Count > maxBuffer) _buffer.RemoveAt(0);

        Debug.Log($"[Combo] +{t} | buffer = {string.Join(",", _buffer)}");

        ComboBinding matched = FindExactMatchOnSuffix(_buffer);
        if (matched != null)
        {
            TriggerSlot(matched.slotIndex, matched.name);
            _buffer.Clear();
            return;
        }

        if (!CouldBePrefixOfAnyCombo(_buffer))
        {
            Debug.Log($"[Combo] dead-end buffer, clearing: {string.Join(",", _buffer)}");
            _buffer.Clear();
        }
    }


    private ComboBinding FindExactMatchOnSuffix(List<Token> buffer)
    {
        foreach (var b in bindings)
        {
            if (b.sequence == null || b.sequence.Length == 0)
                continue;

            if (EndsWith(buffer, b.sequence))
                return b;
        }
        return null;
    }

    private bool CouldBePrefixOfAnyCombo(List<Token> buffer)
    {
        // 我们允许“继续搓下去”的条件：buffer 的末尾能匹配任意 combo 的前缀
        foreach (var b in bindings)
        {
            if (b.sequence == null || b.sequence.Length == 0)
                continue;

            if (IsSuffixAValidPrefix(buffer, b.sequence))
                return true;
        }
        return false;
    }

    private static bool EndsWith(List<Token> buffer, Token[] seq)
    {
        if (buffer.Count < seq.Length)
            return false;

        int start = buffer.Count - seq.Length;
        for (int i = 0; i < seq.Length; i++)
        {
            if (buffer[start + i] != seq[i])
                return false;
        }
        return true;
    }

    private static bool IsSuffixAValidPrefix(List<Token> buffer, Token[] seq)
    {
        // 取 buffer 的全部，要求它能匹配 seq 的前 buffer.Count 个
        if (buffer.Count > seq.Length)
            return false;

        // 让 buffer 对齐 seq 的开头：我们判断的是“当前输入序列”是否等于某个 combo 的前缀
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] != seq[i])
                return false;
        }
        return true;
    }

    private void TriggerSlot(int slotIndex, string comboName)
    {
        // 本地立即触发回调（你可以用来播本地 UI / 音效）
        OnSkillSlotTriggered?.Invoke(slotIndex, comboName);

        // 联机：请求服务器执行（最终由服务器广播）
        RequestCastServerRpc(slotIndex, comboName);
    }

    [ServerRpc]
    private void RequestCastServerRpc(int slotIndex, string comboName)
    {
        // TODO: 这里未来做冷却/CD/资源/状态校验
        CastObserversRpc(slotIndex, comboName);
    }

    [ObserversRpc]
    private void CastObserversRpc(int slotIndex, string comboName)
    {
        // 所有人都会收到。你可以在这里统一触发“释放技能槽位”的视觉表现。
        // 如果你不想 owner 重复触发，可以在这里加 if (IsOwner) return; 但一般让它走一遍也OK。
        OnSkillSlotTriggered?.Invoke(slotIndex, comboName);

        Debug.Log($"[ComboSkillInput] Cast Slot={slotIndex}, Combo={comboName}, Owner={IsOwner}");
    }

    // 在这里调试用：生成GUI来监测目前输入
    [SerializeField] private bool debugHud = true;

    private void OnGUI()
    {
        if (!debugHud || !IsOwner) return;
        GUI.Label(new Rect(10, 10, 800, 30), $"Combo Buffer: {string.Join(",", _buffer)}");
        GUI.Label(new Rect(10, 30, 800, 30), $"Last Input Δt: {(Time.time - _lastInputTime):0.00}s / Window {stepWindowSeconds:0.00}s");
    }

}

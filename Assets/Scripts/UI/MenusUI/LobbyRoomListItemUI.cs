using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

public class LobbyRoomListItemUI : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image selectionHighlight;
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI roomDetailsText;

    private LobbyListItemData _data;
    private Action<LobbyListItemData, LobbyRoomListItemUI> _onSelected;

    public LobbyListItemData Data => _data;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(NotifySelected);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(NotifySelected);
    }

    public void Bind(LobbyListItemData data, Action<LobbyListItemData, LobbyRoomListItemUI> onSelected, bool selected)
    {
        _data = data;
        _onSelected = onSelected;

        if (roomNameText != null)
            roomNameText.text = string.IsNullOrWhiteSpace(data.LobbyName) ? "Unnamed Room" : data.LobbyName;

        if (roomDetailsText != null)
            roomDetailsText.text = $"#{data.LobbyId}  {data.CurrentPlayers}/{data.MaxPlayers}";

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.enabled = selected;
    }

    private void NotifySelected()
    {
        _onSelected?.Invoke(_data, this);
    }
}

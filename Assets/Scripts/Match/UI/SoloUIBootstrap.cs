using TMPro;
using UnityEngine;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Preplace once on an always-active root in the menu and race scenes. Registers the bundled CJK font as a TMP
    /// fallback for the application's lifetime (HUD text relies on it) and, in the race, adds the quit dialog and
    /// the Solo results view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloUIBootstrap : MonoBehaviour
    {
        [Tooltip("Assign a bundled TMP font with Chinese glyphs for the Solo UI.")]
        [SerializeField] private TMP_FontAsset font;

        private void Start()
        {
            if (font == null) font = TMP_Settings.defaultFontAsset;
            // Leaderboard and HUD text also render the localized offline player name.
            // Keep one shared fallback for the application lifetime, without replacing their styling.
            if (font != null && !TMP_Settings.fallbackFontAssets.Contains(font))
                TMP_Settings.fallbackFontAssets.Add(font);

            if (gameObject.scene.name != SceneNames.RaceMap) return;
            gameObject.AddComponent<QuitConfirmDialog>().Initialize(font);
            gameObject.AddComponent<SoloResultsView>().Initialize(font);
        }
    }
}

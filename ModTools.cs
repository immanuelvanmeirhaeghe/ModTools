using Enums;
using ModManager.Data.Interfaces;
using ModTools.Enums;
using ModTools.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace ModTools
{
    /// <summary>
    /// ModTools is a mod for Green Hell that allows a player to unlock all tool- armor- weapon- and trap blueprints.
    /// Press Keypad9 (default) or the key configurable in ModAPI to open the main mod screen.
    /// </summary>
    public class ModTools : MonoBehaviour
    {
        private const string LocalizedTextKey = "HUD_InfoLog_NewEntry";
        private static ModTools Instance;

        private static readonly string ModName = nameof(ModTools);

        private static float ModToolsScreenTotalWidth { get; set; } = 700f;
        private static float ModToolsScreenTotalHeight { get; set; } = 350f;
        private static float ModToolsScreenMinWidth { get; set; } = 700f;
        private static float ModToolsScreenMaxWidth { get; set; } = Screen.width;
        private static float ModToolsScreenMinHeight { get; set; } = 50f;
        private static float ModToolsScreenMaxHeight { get; set; } = Screen.height;
        private static float ModToolsScreenStartPositionX { get; set; } = Screen.width / 2f;
        private static float ModToolsScreenStartPositionY { get; set; } = Screen.height / 2f;
        private static bool IsModToolsScreenMinimized { get; set; } = false;
        private Color DefaultGuiColor = GUI.color;
        private bool ShowModTools { get; set; } = false;
        private bool ShowModToolsInfo { get; set; } = false;

        public static Rect ModToolsScreen = new Rect(ModToolsScreenStartPositionX, ModToolsScreenStartPositionY, ModToolsScreenTotalWidth, ModToolsScreenTotalHeight);

        private static CursorManager LocalCursorManager;
        private static ItemsManager LocalItemsManager;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static StylingManager LocalStylingManager;

        private static List<ItemInfo> UnlockedToolsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedTools { get; private set; }

        private static List<ItemInfo> UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedWeapons { get; private set; }

        private static List<ItemInfo> UnlockedArmorItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedArmor { get; private set; }

        public static List<ItemID> WaterToolIDs = new List<ItemID>();
        public static List<ItemID> FireToolIDs = new List<ItemID>();
        public static List<ItemID> FishingToolIDs = new List<ItemID>();


        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public Vector2 ModInfoScrollViewPosition { get; set; } = Vector2.zero;
        public IConfigurableMod SelectedMod { get; set; } = default;

        public static string AlreadyUnlockedInfo(string info)
            => $"All {info} were already unlocked!";
        public static string AddedToInventoryMessage(int count, ItemInfo itemInfo)
            => $"Added {count} x {itemInfo.GetNameToDisplayLocalized()} to inventory.";
        public static string OnlyForSinglePlayerOrHostMessage()
                    => $"Only available for single player or when host. Host can activate using ModManager.";
        public static string PermissionChangedMessage(string permission, string reason)
            => $"Permission to use mods and cheats in multiplayer was {permission} because {reason}.";
        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";
        protected virtual void OnlyForSingleplayerOrWhenHostBox()
        {
            using (var infoScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), LocalStylingManager.ColoredCommentLabel(LocalStylingManager.DefaultAttentionColor));
            }
        }
        private void ShowHUDBigInfo(string text)
        {
            string header = $"{ModName} Info";
            string textureName = HUDInfoLogTextureType.Count.ToString();

            HUDBigInfo bigInfo = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = 2f;
            HUDBigInfoData bigInfoData = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            bigInfo.AddInfo(bigInfoData);
            bigInfo.Show(true);
        }

        protected virtual void Awake()
        {
            Instance = this;
        }

        protected virtual void OnDestroy()
        {
            Instance = null;
        }

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
            InitData();
            ShortcutKey = GetConfigurableKey(nameof(ShortcutKey));
        }

        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");
        public KeyCode ShortcutKey { get; set; } = KeyCode.Keypad9;
        private KeyCode GetConfigurableKey(string buttonId)
        {
            KeyCode configuredKeyCode = default;
            string configuredKeybinding = string.Empty;

            try
            {
                if (File.Exists(RuntimeConfigurationFile))
                {
                    using (var xmlReader = XmlReader.Create(new StreamReader(RuntimeConfigurationFile)))
                    {
                        while (xmlReader.Read())
                        {
                            if (xmlReader["ID"] == ModName)
                            {
                                if (xmlReader.ReadToFollowing(nameof(Button)) && xmlReader["ID"] == buttonId)
                                {
                                    configuredKeybinding = xmlReader.ReadElementContentAsString();
                                }
                            }
                        }
                    }
                }

                configuredKeybinding = configuredKeybinding?.Replace("NumPad", "Keypad").Replace("Oem", "");

                configuredKeyCode = (KeyCode)(!string.IsNullOrEmpty(configuredKeybinding)
                                                            ? Enum.Parse(typeof(KeyCode), configuredKeybinding)
                                                            : GetType().GetProperty(buttonId)?.GetValue(this));
                return configuredKeyCode;
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(GetConfigurableKey));
                configuredKeyCode = (KeyCode)(GetType().GetProperty(buttonId)?.GetValue(this));
                return configuredKeyCode;
            }
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            string reason = optionValue ? "the game host allowed usage" : "the game host did not allow usage";
            IsModActiveForMultiplayer = optionValue;

            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted", $"{reason}"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked", $"{reason}"), MessageType.Info, Color.yellow))
                            );
        }

        private void HandleException(Exception exc, string methodName)
        {
            string info = $"[{ModName}:{methodName}] throws exception:\n{exc}";
            ModAPI.Log.Write(info);
            ShowHUDBigInfo(HUDBigInfoMessage(exc.Message, MessageType.Error, Color.red));
        }

        public ModTools()
        {
            useGUILayout = true;
            Instance = this;
        }

        public static ModTools Get()
        {
            return Instance;
        }

        private void ShowHUDInfoLog(string ItemInfo, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            ((HUDMessages)LocalHUDManager.GetHUD(typeof(HUDMessages))).AddMessage(localization.Get(localizedTextKey) + "  " + localization.Get(ItemInfo));
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            LocalCursorManager.ShowCursor(blockPlayer);

            if (blockPlayer)
            {
                LocalPlayer.BlockMoves();
                LocalPlayer.BlockRotation();
                LocalPlayer.BlockInspection();
            }
            else
            {
                LocalPlayer.UnblockMoves();
                LocalPlayer.UnblockRotation();
                LocalPlayer.UnblockInspection();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(ShortcutKey))
            {
                if (!ShowModTools)
                {
                    InitData();
                    EnableCursor(blockPlayer: true);
                }
                ToggleShowUI(0);
                if (!ShowModTools)
                {
                    EnableCursor(blockPlayer: false);
                }
            }
        }

        private void ToggleShowUI(int controlId)
        {
            switch (controlId)
            {
                case 0:
                    ShowModTools = !ShowModTools;
                    return;                
                case 3:
                    ShowModToolsInfo = !ShowModToolsInfo;
                    return;                
                default:
                    ShowModTools = !ShowModTools;
                    ShowModToolsInfo = !ShowModToolsInfo;
                    return;
            }
        }

        private void OnGUI()
        {
            if (ShowModTools)
            {
                InitData();
                InitSkinUI();
                ShowModToolsWindow();
            }
        }

        private void ShowModToolsWindow()
        {
            int ModToolsScreenId = GetHashCode();
            string ModToolsScreenTitle = $"{ModName} created by [Dragon Legion] Immaanuel#4300";
            ModToolsScreen = GUILayout.Window(
                                                                                    ModToolsScreenId,
                                                                                    ModToolsScreen,
                                                                                    InitModToolsScreen,
                                                                                    ModToolsScreenTitle,
                                                                                    GUI.skin.window,
                                                                                    GUILayout.ExpandWidth(true),
                                                                                    GUILayout.MinWidth(ModToolsScreenMinWidth),
                                                                                    GUILayout.MaxWidth(ModToolsScreenMaxWidth),
                                                                                    GUILayout.ExpandHeight(true),
                                                                                    GUILayout.MinHeight(ModToolsScreenMinHeight),
                                                                                    GUILayout.MaxHeight(ModToolsScreenMaxHeight));
        }

        private void InitData()
        {
            LocalCursorManager = CursorManager.Get();
            LocalItemsManager = ItemsManager.Get();
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
            LocalStylingManager = StylingManager.Get();
        }

        private void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private void ModToolsScreenMenuBox()
        {
            string CollapseButtonText = IsModToolsScreenMinimized ? "O" : "-";

            if (GUI.Button(new Rect(ModToolsScreen.width - 40f, 0f, 20f, 20f), CollapseButtonText, GUI.skin.button))
            {
                CollapseModToolsWindow();
            }

            if (GUI.Button(new Rect(ModToolsScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
        }

        private void CollapseModToolsWindow()
        {
            if (!IsModToolsScreenMinimized)
            {
                ModToolsScreen = new Rect(ModToolsScreen.x, ModToolsScreen.y, ModToolsScreenTotalWidth, ModToolsScreenMinHeight);
                IsModToolsScreenMinimized = true;
            }
            else
            {
                ModToolsScreen = new Rect(ModToolsScreen.x, ModToolsScreen.y, ModToolsScreenTotalWidth, ModToolsScreenTotalHeight);
                IsModToolsScreenMinimized = false;
            }
            ShowModToolsWindow();
        }

        private void InitModToolsScreen(int windowID)
        {
            ModToolsScreenStartPositionX = ModToolsScreen.x;
            ModToolsScreenStartPositionY = ModToolsScreen.y;
            ModToolsScreenTotalWidth = ModToolsScreen.width;

            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ModToolsScreenMenuBox();
                if (!IsModToolsScreenMinimized)
                {
                    ModToolsManagerBox();
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ModToolsManagerBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{ModName} Manager", LocalStylingManager.ColoredHeaderLabel(LocalStylingManager.DefaultAttentionColor));
                    GUILayout.Label($"{ModName} Options", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultAttentionColor));

                    using (new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        //if (GUILayout.Button($"Mod Info", GUI.skin.button))
                        //{
                        //    ToggleShowUI(3);
                        //}
                        //if (ShowModToolsInfo)
                        //{
                        //    ModToolsInfoBox();
                        //}
                        ModToolsOptionsBox();
                        MultiplayerOptionBox();
                        UnlockToolsBox();
                        UnlockWeaponsTrapsBox();
                        UnlockArmorBox();
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void ModToolsInfoBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ModInfoScrollViewPosition = GUILayout.BeginScrollView(ModInfoScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(150f));

                GUILayout.Label("Mod Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));

                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.GameID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.GameID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.ID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.ID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.UniqueID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.UniqueID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.Version)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.Version}", LocalStylingManager.FormFieldValueLabel);
                }

                GUILayout.Label("Buttons Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));

                foreach (var configurableModButton in SelectedMod.ConfigurableModButtons)
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.ID)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.ID}", LocalStylingManager.FormFieldValueLabel);
                    }
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.KeyBinding)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.KeyBinding}", LocalStylingManager.FormFieldValueLabel);
                    }
                }

                GUILayout.EndScrollView();
            }
        }

        private void ModToolsOptionsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"To toggle the mod main UI, press [{ShortcutKey}]", LocalStylingManager.TextLabel);                    
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void MultiplayerOptionBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    string multiplayerOptionMessage = string.Empty;
                    GUILayout.Label("Multiplayer Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));
                    if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                    {
                        if (IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are the game host";
                        }
                        if (IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host allowed usage";
                        }
                        GUILayout.Label($"{PermissionChangedMessage($"granted", multiplayerOptionMessage)}", LocalStylingManager.ColoredToggleValueTextLabel(true, Color.green, LocalStylingManager.DefaultAttentionColor));
                    }
                    else
                    {
                        if (!IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are not the game host";
                        }
                        if (!IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host did not allow usage";
                        }
                        GUILayout.Label($"{PermissionChangedMessage($"revoked", multiplayerOptionMessage)}", LocalStylingManager.ColoredToggleValueTextLabel(false, Color.green, LocalStylingManager.DefaultAttentionColor));
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(MultiplayerOptionBox));
            }
        }

        private void UnlockArmorBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"Leaves, wood, bone, metal and armadillo armor: ", LocalStylingManager.TextLabel);
                        if (GUILayout.Button("Unlock armor", GUI.skin.button, GUILayout.Width(150f)))
                        {
                            OnClickUnlockArmorButton();
                        }
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void UnlockWeaponsTrapsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"Weapons and traps: ", LocalStylingManager.TextLabel);
                        if (GUILayout.Button("Unlock weapons/traps", GUI.skin.button, GUILayout.Width(150f)))
                        {
                            OnClickUnlockWeaponsButton();
                        }
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void UnlockToolsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"Fire - water - and fishing tools: ", LocalStylingManager.TextLabel);
                        if (GUILayout.Button("Unlock tools", GUI.skin.button, GUILayout.Width(150f)))
                        {
                            OnClickUnlockToolsButton();
                        }
                    }
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void CloseWindow()
        {
            ShowModTools = false;
            EnableCursor(false);
        }

        private void OnClickUnlockToolsButton()
        {
            try
            {
                UnlockAllTools();
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickUnlockToolsButton));
            }
        }

        private void OnClickUnlockWeaponsButton()
        {
            try
            {
                UnlockAllWeapons();
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickUnlockWeaponsButton));
            }
        }

        private void OnClickUnlockArmorButton()
        {
            try
            {
                UnlockAllArmor();
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickUnlockArmorButton));
            }
        }

        public void UnlockAllArmor()
        {
            try
            {
                if (!HasUnlockedArmor)
                {
                    foreach (var armorItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Armor))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(armorItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(armorItemInfo.m_ID.ToString());
                    }
                    HasUnlockedArmor = true;                    
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(AlreadyUnlockedInfo("armor blueprints"),
                            MessageType.Warning,
                            LocalStylingManager.DefaultAttentionColor));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedArmor = false;
                HandleException(exc, nameof(UnlockAllArmor));
            }
        }

        public void UnlockAllTools()
        {
            try
            {
                if (!HasUnlockedTools)
                {
                    foreach (var toolItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.ItemTool))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(toolItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(toolItemInfo.m_ID.ToString());
                    }
                    foreach (var torchItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Torch))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(torchItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(torchItemInfo.m_ID.ToString());
                    }
                    HasUnlockedTools = true;
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(AlreadyUnlockedInfo("tool blueprints"),
                        MessageType.Warning,
                        LocalStylingManager.DefaultAttentionColor));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedTools = false;
                HandleException(exc, nameof(UnlockAllTools));
            }
        }

        public void UnlockAllWeapons()
        {
            try
            {
                if (!HasUnlockedWeapons) 
                {
                    foreach (var weaponItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Weapon))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(weaponItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(weaponItemInfo.m_ID.ToString());
                    }
                    foreach (var spearItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Spear))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(spearItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(spearItemInfo.m_ID.ToString());
                    }
                    foreach (var bowItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Bow))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(bowItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(bowItemInfo.m_ID.ToString());
                    }
                    foreach (var arrowItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Arrow))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(arrowItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(arrowItemInfo.m_ID.ToString());
                    }
                    foreach (var blowpipeItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Blowpipe))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(blowpipeItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(blowpipeItemInfo.m_ID.ToString());
                    }
                    foreach (var blowpipeArrowItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.BlowpipeArrow))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(blowpipeArrowItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(blowpipeArrowItemInfo.m_ID.ToString());
                    }
                    foreach (var trapItemInfo in ItemsManager.Get().GetAllInfosOfType(ItemType.Trap))
                    {
                        ItemsManager.Get().UnlockItemInNotepad(trapItemInfo.m_ID);
                        ItemsManager.Get().UnlockItemInfo(trapItemInfo.m_ID.ToString());
                    }
                    HasUnlockedWeapons = true;              
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(AlreadyUnlockedInfo("weapon - and trap blueprints"),
                        MessageType.Warning,
                        LocalStylingManager.DefaultAttentionColor));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons |= false;
                HandleException(exc, nameof(UnlockAllWeapons));
            }
        }
    }
}

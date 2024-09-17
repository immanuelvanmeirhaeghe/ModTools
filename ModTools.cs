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
                        if (GUILayout.Button($"Mod Info", GUI.skin.button))
                        {
                            ToggleShowUI(3);
                        }
                        if (ShowModToolsInfo)
                        {
                            ModToolsInfoBox();
                        }
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

        protected virtual void ModToolsInfoBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ModInfoScrollViewPosition = GUILayout.BeginScrollView(ModInfoScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(150f));

                GUILayout.Label("Mod Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));

                using (var gidScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.GameID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.GameID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (var midScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.ID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.ID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (var uidScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.UniqueID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.UniqueID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (var versionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.Version)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.Version}", LocalStylingManager.FormFieldValueLabel);
                }

                GUILayout.Label("Buttons Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));

                foreach (var configurableModButton in SelectedMod.ConfigurableModButtons)
                {
                    using (var btnidScope = new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.ID)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.ID}", LocalStylingManager.FormFieldValueLabel);
                    }
                    using (var btnbindScope = new GUILayout.HorizontalScope(GUI.skin.box))
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
                if (UnlockedArmorItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedArmorItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedArmorItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!HasUnlockedArmor)
                {
                    UnlockArmors();
                    UnlockArmorStands();
                    UnlockArmorForms();
                    if (UnlockedArmorItemInfos != null && UnlockedArmorItemInfos.Count == 0)
                    {
                        ModAPI.Log.Write("UnlockedArmorItemInfos is empty!");
                        ShowHUDBigInfo(HUDBigInfoMessage("Fatal problem: Could not retrieve any armor blueprints. See logfile in game log folder for more info.",
                            MessageType.Error,
                            LocalStylingManager.DefaultErrorColor));
                        HasUnlockedArmor = false;
                    }
                    else
                    {
                        foreach (ItemInfo unlockedArmorItemInfo in UnlockedArmorItemInfos)
                        {
                            LocalItemsManager.UnlockItemInfo(unlockedArmorItemInfo.m_ID.ToString());
                            LocalItemsManager.UnlockItemInNotepad(unlockedArmorItemInfo.m_ID);
                            ShowHUDInfoLog(unlockedArmorItemInfo.m_ID.ToString(), LocalizedTextKey);
                        }
                        HasUnlockedArmor = true;
                    }
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
                if (UnlockedToolsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedToolsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedToolsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!HasUnlockedTools)
                {
                    UnlockFireTools();
                    UnlockFishingTools();
                    UnlockWaterTools();
                    if (UnlockedToolsItemInfos != null && UnlockedToolsItemInfos.Count == 0)
                    {
                        ModAPI.Log.Write("UnlockedToolsItemInfos is empty!");
                        ShowHUDBigInfo(HUDBigInfoMessage("Fatal problem: Could not retrieve any tool blueprints. See logfile in game log folder for more info.",
                            MessageType.Error,
                            LocalStylingManager.DefaultErrorColor));
                        HasUnlockedTools = false;
                    }
                    else
                    {
                        foreach (ItemInfo unlockedToolsItemInfo in UnlockedToolsItemInfos)
                        {
                            LocalItemsManager.UnlockItemInfo(unlockedToolsItemInfo.m_ID.ToString());
                            LocalItemsManager.UnlockItemInNotepad(unlockedToolsItemInfo.m_ID);
                            ShowHUDInfoLog(unlockedToolsItemInfo.m_ID.ToString(), LocalizedTextKey);
                        }
                        HasUnlockedTools = true;
                    }
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
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!HasUnlockedWeapons)
                {
                    UnlockTorches();
                    UnlockSpears();
                    UnlockAxes();
                    UnlockBladesAndKnives();
                    UnlockBowsAndArrows();
                    UnlockTraps();
                    if (UnlockedWeaponsTrapsItemInfos != null && UnlockedWeaponsTrapsItemInfos.Count == 0)
                    {
                        ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is empty!");
                        ShowHUDBigInfo(HUDBigInfoMessage("Fatal problem: Could not retrieve any weapon - or trap blueprints. See logfile in game log folder for more info.",
                            MessageType.Error,
                            LocalStylingManager.DefaultErrorColor));
                        HasUnlockedWeapons = false;
                    }
                    else
                    {
                        foreach (ItemInfo unlockedWeaponTrapItemInfo in UnlockedWeaponsTrapsItemInfos)
                        {
                            LocalItemsManager.UnlockItemInNotepad(unlockedWeaponTrapItemInfo.m_ID);
                            LocalItemsManager.UnlockItemInfo(unlockedWeaponTrapItemInfo.m_ID.ToString());
                            ShowHUDInfoLog(unlockedWeaponTrapItemInfo.m_ID.ToString(), LocalizedTextKey);
                        }
                        HasUnlockedWeapons = true;
                    }
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

        private void UnlockArmors()
        {
            try
            {
                if (UnlockedArmorItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedArmorItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedArmorItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.armadillo_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.armadillo_armor));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.bamboo_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.bamboo_armor));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.bone_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.bone_armor));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.leaf_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.leaf_armor));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_armor));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_armor_part)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_armor_part));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.stick_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.stick_armor));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedArmor = false;
                HandleException(exc, nameof(UnlockArmors));
            }
        }

        private void UnlockArmorStands()
        {
            try
            {
                if (UnlockedArmorItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedArmorItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedArmorItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.wooden_armor_stand)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.wooden_armor_stand));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.bamboo_armor_stand)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.bamboo_armor_stand));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedArmor = false;
                HandleException(exc, nameof(UnlockArmorStands));
            }
        }

        private void UnlockArmorForms()
        {
            try
            {
                if (UnlockedArmorItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedArmorItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedArmorItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_baked)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_baked));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_bamboo)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_bamboo));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_bamboo_baked)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_bamboo_baked));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_bone)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_bone));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_bone_baked)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_bone_baked));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_metal)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_metal));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_metal_baked)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_metal_baked));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_metal_part)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_metal_part));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_metal_part_baked)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_metal_part_baked));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_stick)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_stick));
                }

                if (!UnlockedArmorItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_armor_stick_baked)))
                {
                    UnlockedArmorItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_armor_stick_baked));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedArmor = false;
                HandleException(exc, nameof(UnlockArmorForms));
            }
        }

        private void UnlockBowsAndArrows()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Bow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Bow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tribe_Bow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tribe_Bow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Blowpipe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Blowpipe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Arrow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Arrow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Metal_arrow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Metal_arrow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_arrowhead)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_arrowhead));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Blowpipe_Arrow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Blowpipe_Arrow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tribe_Arrow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tribe_Arrow));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockBowsAndArrows));
            }
        }

        private void UnlockTorches()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Weak_Torch)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Weak_Torch));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Torch)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Torch));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tobacco_Torch)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tobacco_Torch));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockTorches));
            }
        }

        private void UnlockBladesAndKnives()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Obsidian_Blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Obsidian_Blade));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stone_Blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stone_Blade));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bone_Knife)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bone_Knife));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stick_Blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stick_Blade));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_blade));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Obsidian_Bone_Blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Obsidian_Bone_Blade));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockBladesAndKnives));
            }
        }

        private void UnlockAxes()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Axe_professional)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Axe_professional));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stone_Axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stone_Axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Blade_Axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Blade_Axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_axe_blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_axe_blade));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bone_Axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bone_Axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stone_Axe_2H)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stone_Axe_2H));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tribe_Axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tribe_Axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_pickaxe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_pickaxe));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockAxes));
            }
        }

        private void UnlockSpears()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.metal_spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.metal_spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bone_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bone_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Four_Pronged_Bamboo_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Four_Pronged_Bamboo_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Four_Pronged_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Four_Pronged_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Obsidian_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Obsidian_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stone_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stone_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tribe_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tribe_Spear));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Weak_Spear)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Weak_Spear));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockSpears));
            }
        }

        private void UnlockTraps()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.shrimp_trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.shrimp_trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.tribe_spike_trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.tribe_spike_trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Big_Stick_Fish_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Big_Stick_Fish_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Cage_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Cage_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fish_Rod_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fish_Rod_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Human_Killer_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Human_Killer_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Killer_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Killer_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Snare_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Snare_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stick_Fish_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stick_Fish_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stone_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stone_Trap));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tribe_Bow_Trap)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tribe_Bow_Trap));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockTraps));
            }
        }

        private void UnlockWeaponStands()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.bamboo_arrow_stand)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.bamboo_arrow_stand));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.wooden_arrow_stand)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.wooden_arrow_stand));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockWeaponStands));
            }
        }

        private void UnlockWeaponForms()
        {
            try
            {
                if (UnlockedWeaponsTrapsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedWeaponsTrapsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_arrow)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_arrow));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_arrow_baked)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_arrow_baked));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_axe)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_axe));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_axe_baked)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_axe_baked));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_blade)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_blade));
                }

                if (!UnlockedWeaponsTrapsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.form_blade_baked)))
                {
                    UnlockedWeaponsTrapsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.form_blade_baked));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedWeapons = false;
                HandleException(exc, nameof(UnlockWeaponForms));
            }
        }

        private void UnlockWaterTools()
        {
            try
            {
                if (UnlockedToolsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedToolsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedToolsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Coconut_Bidon)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Coconut_Bidon));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Water_Filter)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Water_Filter));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Water_Filter)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Water_Filter));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Water_Collector)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Water_Collector));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Water_Collector)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Water_Collector));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Water_Container)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Water_Container));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.WaterSource)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.WaterSource));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.mud_mixer)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.mud_mixer));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.mud_water_collector)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.mud_water_collector));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.mud_shower)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.mud_shower));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Bowl)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Bowl));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Brazil_nut_Bowl)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Brazil_nut_Bowl));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedTools = false;
                HandleException(exc, nameof(UnlockWaterTools));
            }
        }

        private void UnlockFishingTools()
        {
            try
            {
                if (UnlockedToolsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedToolsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedToolsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Fishing_Rod_Bone)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Fishing_Rod_Bone));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fish_Hook)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fish_Hook));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fishing_Rod)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fishing_Rod));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fishing_Rod_Bone)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fishing_Rod_Bone));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fish_Bone)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fish_Bone));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fish_Rod_Trap)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fish_Rod_Trap));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedTools = false;
                HandleException(exc, nameof(UnlockFishingTools));
            }
        }

        private void UnlockFireTools()
        {
            try
            {
                if (UnlockedToolsItemInfos == null)
                {
                    ModAPI.Log.Write("UnlockedToolsItemInfos is null! Setting value to new List ItemInfo");
                    UnlockedToolsItemInfos = new List<ItemInfo>();
                }
                if (LocalItemsManager == null)
                {
                    ModAPI.Log.Write("LocalItemsManager is null! Setting value to ItemsManager.Get");
                    LocalItemsManager = ItemsManager.Get();
                }
                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Campfire_fireside)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Campfire_fireside));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Village_campfire_burned)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Village_campfire_burned));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Small_Fire)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Small_Fire));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fire)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fire));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stone_Ring)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stone_Ring));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Campfire)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Campfire));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Hand_Drill_Board)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Hand_Drill_Board));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Hand_Drill_Stick)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Hand_Drill_Stick));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fire_Bow)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fire_Bow));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fire_Board)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fire_Board));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Rubing_Wood)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Rubing_Wood));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Tobacco_Torch)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Tobacco_Torch));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Dryer)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Dryer));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Dryer)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Dryer));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Smoker)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Smoker));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Smoker)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Smoker));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.mud_metal_furnace)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.mud_metal_furnace));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.mud_charcoal_furnace)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.mud_charcoal_furnace));
                }

                if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Cremation_fire)))
                {
                    UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Cremation_fire));
                }
            }
            catch (Exception exc)
            {
                HasUnlockedTools = false;
                HandleException(exc, nameof(UnlockFireTools));
            }
        }
    }
}

using Enums;
using ModTools.Enums;
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
        private static ModTools Instance;

        private static readonly string ModName = nameof(ModTools);
        private static readonly float ModScreenTotalWidth = 500f;
        private static readonly float ModScreenTotalHeight = 150f;
        private static readonly float ModScreenMinWidth = 450f;
        private static readonly float ModScreenMaxWidth = 550f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 200f;
        private static float ModScreenStartPositionX { get; set; } = (Screen.width - ModScreenMaxWidth) % ModScreenTotalWidth;
        private static float ModScreenStartPositionY { get; set; } = (Screen.height - ModScreenMaxHeight) % ModScreenTotalHeight;
        private static bool IsMinimized { get; set; } = false;
        private Color DefaultGuiColor = GUI.color;
        private bool ShowUI = false;

        public static Rect ModToolsScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);

        private static CursorManager LocalCursorManager;
        private static ItemsManager LocalItemsManager;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;

        private static List<ItemInfo> UnlockedToolsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedTools { get; private set; }

        private static List<ItemInfo> UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedWeapons { get; private set; }

        private static List<ItemInfo> UnlockedArmorItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedArmor { get; private set; }

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

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

        public void ShowHUDBigInfo(string text)
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

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
            ModKeybindingId = GetConfigurableKey(nameof(ModKeybindingId));
        }

        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");
        private static KeyCode ModKeybindingId { get; set; } = KeyCode.Keypad8;
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
            string info = $"[{ModName}:{methodName}] throws exception:\n{exc.Message}";
            ModAPI.Log.Write(info);
            ShowHUDBigInfo(HUDBigInfoMessage(info, MessageType.Error, Color.red));
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

        public void ShowHUDInfoLog(string ItemInfo, string localizedTextKey)
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
            if (Input.GetKeyDown(ModKeybindingId))
            {
                if (!ShowUI)
                {
                    InitData();
                    EnableCursor(blockPlayer: true);
                }
                ToggleShowUI();
                if (!ShowUI)
                {
                    EnableCursor();
                }
            }
        }

        private void ToggleShowUI()
        {
            ShowUI = !ShowUI;
        }

        private void OnGUI()
        {
            if (ShowUI)
            {
                InitData();
                InitSkinUI();
                InitWindow();
            }
        }

        private void InitWindow()
        {
            int wid = GetHashCode();
            ModToolsScreen = GUILayout.Window(
                                                                                    wid,
                                                                                    ModToolsScreen,
                                                                                    InitModToolsScreen,
                                                                                    ModName,
                                                                                    GUI.skin.window,
                                                                                    GUILayout.ExpandWidth(true),
                                                                                    GUILayout.MinWidth(ModScreenMinWidth),
                                                                                    GUILayout.MaxWidth(ModScreenMaxWidth),
                                                                                    GUILayout.ExpandHeight(true),
                                                                                    GUILayout.MinHeight(ModScreenMinHeight),
                                                                                    GUILayout.MaxHeight(ModScreenMaxHeight));
        }

        private void InitData()
        {
            LocalCursorManager = CursorManager.Get();
            LocalItemsManager = ItemsManager.Get();
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
        }

        private void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(ModToolsScreen.width - 40f, 0f, 20f, 20f), "-", GUI.skin.button))
            {
                CollapseWindow();
            }

            if (GUI.Button(new Rect(ModToolsScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
        }

        private void CollapseWindow()
        {
            if (!IsMinimized)
            {
                ModToolsScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                ModToolsScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
                IsMinimized = false;
            }
            InitWindow();
        }

        private void InitModToolsScreen(int windowID)
        {
            ModScreenStartPositionX = ModToolsScreen.x;
            ModScreenStartPositionY = ModToolsScreen.y;

            using (var modContentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();
                if (!IsMinimized)
                {
                    ModOptionsBox();
                    UnlockToolsBox();
                    UnlockWeaponsTrapsBox();
                    UnlockArmorBox();
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ModOptionsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var optionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"To toggle the mod main UI, press [{ModKeybindingId}]", GUI.skin.label);
                    MultiplayerOptionBox();
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
                using (var multiplayeroptionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Multiplayer options: ", GUI.skin.label);
                    string multiplayerOptionMessage = string.Empty;
                    if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                    {
                        GUI.color = Color.green;
                        if (IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are the game host";
                        }
                        if (IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host allowed usage";
                        }
                        _ = GUILayout.Toggle(true, PermissionChangedMessage($"granted", multiplayerOptionMessage), GUI.skin.toggle);
                    }
                    else
                    {
                        GUI.color = Color.yellow;
                        if (!IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are not the game host";
                        }
                        if (!IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host did not allow usage";
                        }
                        _ = GUILayout.Toggle(false, PermissionChangedMessage($"revoked", $"{multiplayerOptionMessage}"), GUI.skin.toggle);
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
                using (var toolsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label($"Leave, wood, bone, metal and armadillo armor: ", GUI.skin.label);
                    using (var unlockarmorScope = new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        if (GUILayout.Button("Unlock armor", GUI.skin.button))
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
                using (var toolsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label($"Weapons and traps: ", GUI.skin.label);
                    using (var unlockweaponsScope = new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        if (GUILayout.Button("Unlock weapons/traps", GUI.skin.button))
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
                using (var toolsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label($"Fire - water - and fishing tools: ", GUI.skin.label);
                    using (var unlocktoolsScope = new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        if (GUILayout.Button("Unlock tools", GUI.skin.button))
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

        private void OnlyForSingleplayerOrWhenHostBox()
        {
            using (var infoScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUI.color = Color.yellow;
                GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), GUI.skin.label);
            }
        }

        private void CloseWindow()
        {
            ShowUI = false;
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
                    UnlockedArmorItemInfos = LocalItemsManager.GetAllInfos().Values.Where(info => info.IsArmor()).ToList();

                    foreach (ItemInfo unlockedArmorItemInfo in UnlockedArmorItemInfos)
                    {
                        LocalItemsManager.UnlockItemInNotepad(unlockedArmorItemInfo.m_ID);
                        LocalItemsManager.UnlockItemInfo(unlockedArmorItemInfo.m_ID.ToString());
                        ShowHUDInfoLog(unlockedArmorItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    HasUnlockedArmor = true;
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(AlreadyUnlockedInfo("armor blueprints"), MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(UnlockAllArmor));
            }
        }

        public void UnlockAllWeapons()
        {
            try
            {
                if (!HasUnlockedWeapons)
                {
                    UnlockedWeaponsTrapsItemInfos = LocalItemsManager.GetAllInfos().Values.Where(info => info.IsWeapon() || ItemInfo.IsTrap(info.m_ID)).ToList();

                    foreach (ItemInfo unlockedWeaponTrapItemInfo in UnlockedWeaponsTrapsItemInfos)
                    {
                        LocalItemsManager.UnlockItemInNotepad(unlockedWeaponTrapItemInfo.m_ID);
                        LocalItemsManager.UnlockItemInfo(unlockedWeaponTrapItemInfo.m_ID.ToString());
                        ShowHUDInfoLog(unlockedWeaponTrapItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    HasUnlockedWeapons = true;
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(AlreadyUnlockedInfo("weapon - and trap blueprints"), MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(UnlockAllWeapons));
            }
        }

        public void UnlockAllTools()
        {
            try
            {
                if (!HasUnlockedTools)
                {
                    UnlockedToolsItemInfos = LocalItemsManager.GetAllInfos().Values.Where(info => info.IsTool() || info.IsTorch() || info.IsFishingRod()).ToList();

                    UnlockFireTools();
                    UnlockFishingTools();
                    UnlockWaterTools();

                    foreach (ItemInfo unlockedToolsItemInfo in UnlockedToolsItemInfos)
                    {
                        LocalItemsManager.UnlockItemInfo(unlockedToolsItemInfo.m_ID.ToString());
                        LocalItemsManager.UnlockItemInNotepad(unlockedToolsItemInfo.m_ID);
                        ShowHUDInfoLog(unlockedToolsItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    HasUnlockedTools = true;
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(AlreadyUnlockedInfo("tool blueprints"), MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(UnlockAllTools));
            }
        }

        public void UnlockWaterTools()
        {
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

        public void UnlockFishingTools()
        {
            if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Stick_Fish_Trap)))
            {
                UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Stick_Fish_Trap));
            }

            if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Big_Stick_Fish_Trap)))
            {
                UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Big_Stick_Fish_Trap));
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

        public void UnlockFireTools()
        {
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
    }
}

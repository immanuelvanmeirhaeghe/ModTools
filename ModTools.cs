using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModTools
{
    public enum MessageType
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// ModTools is a mod for Green Hell
    /// that gives the player the option to unlock tools, weapons and armor.
    /// Enable the mod UI by pressing Home.
    /// </summary>
    public class ModTools : MonoBehaviour
    {
        private static ModTools Instance;

        private static readonly string ModName = nameof(ModTools);
        private static readonly float ModScreenTotalWidth = 500f;
        private static readonly float ModScreenTotalHeight = 150f;
        private static readonly float ModScreenMinWidth = 50f;
        private static readonly float ModScreenMaxWidth = 550f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 200f;
        private static float ModScreenStartPositionX { get; set; } = (Screen.width - ModScreenMaxWidth) % ModScreenTotalWidth;
        private static float ModScreenStartPositionY { get; set; } = (Screen.height - ModScreenMaxHeight) % ModScreenTotalHeight;
        private static bool IsMinimized { get; set; } = false;

        private bool ShowUI = false;

        public static Rect ModToolsScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);

        private static ItemsManager LocalItemsManager;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;

        private static List<ItemInfo> UnlockedToolsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedTools { get; private set; }

        private static List<ItemInfo> UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedWeapons { get; private set; }
        public static bool HasBlowgun { get; private set; }

        private static List<ItemInfo> UnlockedArmorItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedArmor { get; private set; }

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string AlreadyUnlockedInfo(string info) => $"All {info} were already unlocked!";
        public static string AddedToInventoryMessage(int count, ItemInfo itemInfo) => $"Added {count} x {itemInfo.GetNameToDisplayLocalized()} to inventory.";
        public static string PermissionChangedMessage(string permission) => $"Permission to use mods and cheats in multiplayer was {permission}";
        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            IsModActiveForMultiplayer = optionValue;
            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked"), MessageType.Info, Color.yellow))
                            );
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

        public void ShowHUDInfoLog(string ItemInfo, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            ((HUDMessages)LocalHUDManager.GetHUD(typeof(HUDMessages))).AddMessage(localization.Get(localizedTextKey) + "  " + localization.Get(ItemInfo));
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            CursorManager.Get().ShowCursor(blockPlayer);

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
            if (Input.GetKeyDown(KeyCode.Home))
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
            ModToolsScreen = GUILayout.Window(wid, ModToolsScreen, InitModToolsScreen, $"{ModName}", GUI.skin.window);
        }

        private void InitData()
        {
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
                ModScreenStartPositionX = ModToolsScreen.x;
                ModScreenStartPositionY = ModToolsScreen.y;
                ModToolsScreen.Set(ModToolsScreen.x, ModToolsScreen.y, ModScreenMinWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                ModToolsScreen.Set(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
                IsMinimized = false;
            }
            InitWindow();
        }

        private void InitModToolsScreen(int windowID)
        {
            using (var modContentScope = new GUILayout.VerticalScope(
                                                                                                        GUI.skin.box,
                                                                                                        GUILayout.ExpandWidth(true),
                                                                                                        GUILayout.MinWidth(ModScreenMinWidth),
                                                                                                        GUILayout.MaxWidth(ModScreenMaxWidth),
                                                                                                        GUILayout.ExpandHeight(true),
                                                                                                        GUILayout.MinHeight(ModScreenMinHeight),
                                                                                                        GUILayout.MaxHeight(ModScreenMaxHeight)))
            {
                ScreenMenuBox();
                if (!IsMinimized)
                {
                    UnlockToolsBox();
                    UnlockWeaponsTrapsBox();
                    UnlockArmorBox();
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void UnlockArmorBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label("Click to unlock all armor blueprints: ", GUI.skin.label);
                if (GUILayout.Button("Unlock armor", GUI.skin.button, GUILayout.MaxWidth(200f)))
                {
                    OnClickUnlockArmorButton();
                }
            }
        }

        private void UnlockWeaponsTrapsBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label("Click to unlock all weapon - and trap blueprints: ", GUI.skin.label);
                if (GUILayout.Button("Unlock weapons/traps", GUI.skin.button, GUILayout.MaxWidth(200f)))
                {
                    OnClickUnlockWeaponsButton();
                }
            }
        }

        private void UnlockToolsBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label("Click to unlock al fire-water-fishing tool blueprints: ", GUI.skin.label);
                if (GUILayout.Button("Unlock tools", GUI.skin.button,GUILayout.MaxWidth(200f)))
                {
                    OnClickUnlockToolsButton();
                }
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickUnlockToolsButton)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickUnlockWeaponsButton)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickUnlockArmorButton)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(UnlockAllArmor)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(UnlockAllWeapons)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(UnlockAllTools)}] throws exception:\n{exc.Message}");
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
            if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fishing_Rod_Bone)))
            {
                UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fishing_Rod_Bone));
            }

            if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Bamboo_Fishing_Rod_Bone)))
            {
                UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Bamboo_Fishing_Rod_Bone));
            }

            if (!UnlockedToolsItemInfos.Contains(LocalItemsManager.GetInfo(ItemID.Fish_Hook)))
            {
                UnlockedToolsItemInfos.Add(LocalItemsManager.GetInfo(ItemID.Fish_Hook));
            }
        }

        public void UnlockFireTools()
        {
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
        }
    }
}

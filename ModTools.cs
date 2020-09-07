using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModTools
{
    /// <summary>
    /// ModTools is a mod for Green Hell
    /// that gives the player the option to unlock tools, weapons and armor.
    /// Enable the mod UI by pressing Home.
    /// </summary>
    public class ModTools : MonoBehaviour
    {
        private static ModTools s_Instance;

        private static readonly string ModName = nameof(ModTools);

        private bool showUI;

        public Rect ModToolsScreen = new Rect(10f, 10f, 450f, 150f);

        private static ItemsManager itemsManager;

        private static HUDManager hUDManager;

        private static Player player;

        private static List<ItemInfo> m_UnlockedToolsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedTools { get; private set; }

        private static List<ItemInfo> m_UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedWeapons { get; private set; }
        public static bool HasBlowgun { get; private set; }

        private static List<ItemInfo> m_UnlockedArmorItemInfos = new List<ItemInfo>();
        public static bool HasUnlockedArmor { get; private set; }
        public bool UseOptionF8 { get; private set; }

        public bool IsModActiveForMultiplayer => FindObjectOfType(typeof(ModManager.ModManager)) != null && ModManager.ModManager.AllowModsForMultiplayer;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public ModTools()
        {
            useGUILayout = true;
            s_Instance = this;
        }

        public static ModTools Get()
        {
            return s_Instance;
        }

        public void ShowHUDBigInfo(string text, string header, string textureName)
        {
            HUDBigInfo obj = (HUDBigInfo)hUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData data = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            obj.AddInfo(data);
            obj.Show(show: true);
        }

        public void ShowHUDInfoLog(string ItemInfo, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            ((HUDMessages)hUDManager.GetHUD(typeof(HUDMessages))).AddMessage(localization.Get(localizedTextKey) + "  " + localization.Get(ItemInfo));
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            CursorManager.Get().ShowCursor(blockPlayer);

            if (blockPlayer)
            {
                player.BlockMoves();
                player.BlockRotation();
                player.BlockInspection();
            }
            else
            {
                player.UnblockMoves();
                player.UnblockRotation();
                player.UnblockInspection();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (!showUI)
                {
                    InitData();
                    EnableCursor(blockPlayer: true);
                }
                showUI = !showUI;
                if (!showUI)
                {
                    EnableCursor();
                }
            }
        }

        private void OnGUI()
        {
            if (showUI)
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
            itemsManager = ItemsManager.Get();
            hUDManager = HUDManager.Get();
            player = Player.Get();
        }

        private void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private void InitModToolsScreen(int windowID)
        {
            using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                if (GUI.Button(new Rect(430f, 0f, 20f, 20f), "X", GUI.skin.button))
                {
                    CloseWindow();
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("All fire-water-fishing tools", GUI.skin.label);
                    if (GUILayout.Button("Unlock tools", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickUnlockToolsButton();
                        CloseWindow();
                    }
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("All weapons and traps", GUI.skin.label);
                    if (GUILayout.Button("Unlock weapons/traps", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickUnlockWeaponsButton();
                        CloseWindow();
                    }
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("All types of armor", GUI.skin.label);
                    if (GUILayout.Button("Unlock armor", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickUnlockArmorButton();
                        CloseWindow();
                    }
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void CloseWindow()
        {
            showUI = false;
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickUnlockToolsButton)}] throws exception: {exc.Message}");
            }
        }

        private void OnClickUnlockWeaponsButton()
        {
            try
            {
                UnlockAllWeapons();
                //GetBlowgun();
                //GetBlowpipeArrows(5);
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickUnlockWeaponsButton)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickUnlockArmorButton)}] throws exception: {exc.Message}");
            }
        }

        public void GetBlowgun()
        {
            try
            {
                itemsManager.UnlockItemInfo(ItemID.Bamboo_Blowpipe.ToString());
                ItemInfo blowPipeItemInfo = itemsManager.GetInfo(ItemID.Bamboo_Blowpipe);
                player.AddItemToInventory(blowPipeItemInfo.m_ID.ToString());
                ShowHUDBigInfo($"Added 1 x {blowPipeItemInfo.GetNameToDisplayLocalized()} to inventory", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                HasBlowgun = true;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(GetBlowgun)}] throws exception: {exc.Message}");
            }
        }

        public void GetMaxThreeBlowpipeArrow(int count = 1)
        {
            try
            {
                if (count > 3)
                {
                    count = 3;
                }
                itemsManager.UnlockItemInfo(ItemID.Blowpipe_Arrow.ToString());
                ItemInfo blowPipeArrowItemInfo = itemsManager.GetInfo(ItemID.Blowpipe_Arrow);
                for (int i = 0; i < count; i++)
                {
                    player.AddItemToInventory(blowPipeArrowItemInfo.m_ID.ToString());
                }
                ShowHUDBigInfo($"Added {count} x {blowPipeArrowItemInfo.GetNameToDisplayLocalized()} to inventory", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(GetMaxThreeBlowpipeArrow)}] throws exception: {exc.Message}");
            }
        }

        public void UnlockAllArmor()
        {
            try
            {
                if (!HasUnlockedArmor)
                {
                    m_UnlockedArmorItemInfos = itemsManager.GetAllInfos().Values.Where(info => info.IsArmor()).ToList();

                    foreach (ItemInfo unlockedArmorItemInfo in m_UnlockedArmorItemInfos)
                    {
                        itemsManager.UnlockItemInNotepad(unlockedArmorItemInfo.m_ID);
                        itemsManager.UnlockItemInfo(unlockedArmorItemInfo.m_ID.ToString());
                        ShowHUDInfoLog(unlockedArmorItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    HasUnlockedArmor = true;
                }
                else
                {
                    ShowHUDBigInfo("All armor were already unlocked!", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(UnlockAllArmor)}] throws exception: {exc.Message}");
            }
        }

        public void UnlockAllWeapons()
        {
            try
            {
                if (!HasUnlockedWeapons)
                {
                    m_UnlockedWeaponsTrapsItemInfos = itemsManager.GetAllInfos().Values.Where(info => info.IsWeapon() || ItemInfo.IsTrap(info.m_ID)).ToList();

                    foreach (ItemInfo unlockedWeaponTrapItemInfo in m_UnlockedWeaponsTrapsItemInfos)
                    {
                        itemsManager.UnlockItemInNotepad(unlockedWeaponTrapItemInfo.m_ID);
                        itemsManager.UnlockItemInfo(unlockedWeaponTrapItemInfo.m_ID.ToString());
                        ShowHUDInfoLog(unlockedWeaponTrapItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    HasUnlockedWeapons = true;
                }
                else
                {
                    ShowHUDBigInfo("All weapons and traps were already unlocked!", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(UnlockAllWeapons)}] throws exception: {exc.Message}");
            }
        }

        public void UnlockAllTools()
        {
            try
            {
                if (!HasUnlockedTools)
                {
                    m_UnlockedToolsItemInfos = itemsManager.GetAllInfos().Values.Where(info => info.IsTool() || info.IsTorch() || info.IsFishingRod()).ToList();

                    UnlockFireTools();
                    UnlockFishingTools();
                    UnlockWaterTools();

                    foreach (ItemInfo unlockedToolsItemInfo in m_UnlockedToolsItemInfos)
                    {
                        itemsManager.UnlockItemInfo(unlockedToolsItemInfo.m_ID.ToString());
                        itemsManager.UnlockItemInNotepad(unlockedToolsItemInfo.m_ID);
                        ShowHUDInfoLog(unlockedToolsItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    HasUnlockedTools = true;
                }
                else
                {
                    ShowHUDBigInfo("All tools were already unlocked", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(UnlockAllTools)}] throws exception: {exc.Message}");
            }
        }

        public void UnlockWaterTools()
        {
            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Coconut_Bidon)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Coconut_Bidon));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Water_Filter)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Water_Filter));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Bamboo_Water_Filter)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Bamboo_Water_Filter));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Bamboo_Water_Collector)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Bamboo_Water_Collector));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Water_Collector)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Water_Collector));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.mud_mixer)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.mud_mixer));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.mud_water_collector)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.mud_water_collector));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.mud_shower)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.mud_shower));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Bamboo_Bowl)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Bamboo_Bowl));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Brazil_nut_Bowl)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Brazil_nut_Bowl));
            }
        }

        public void UnlockFishingTools()
        {
            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Fishing_Rod_Bone)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Fishing_Rod_Bone));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Bamboo_Fishing_Rod_Bone)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Bamboo_Fishing_Rod_Bone));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Fish_Hook)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Fish_Hook));
            }
        }

        public void UnlockFireTools()
        {
            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Small_Fire)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Small_Fire));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Fire)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Fire));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Stone_Ring)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Stone_Ring));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Campfire)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Campfire));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Hand_Drill_Board)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Hand_Drill_Board));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Hand_Drill_Stick)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Hand_Drill_Stick));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Fire_Bow)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Fire_Bow));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Fire_Board)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Fire_Board));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Tobacco_Torch)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Tobacco_Torch));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Dryer)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Dryer));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Bamboo_Dryer)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Bamboo_Dryer));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Smoker)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Smoker));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.Bamboo_Smoker)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.Bamboo_Smoker));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.mud_metal_furnace)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.mud_metal_furnace));
            }

            if (!m_UnlockedToolsItemInfos.Contains(itemsManager.GetInfo(ItemID.mud_charcoal_furnace)))
            {
                m_UnlockedToolsItemInfos.Add(itemsManager.GetInfo(ItemID.mud_charcoal_furnace));
            }
        }

    }
}

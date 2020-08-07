﻿using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModTools
{
    class ModTools : MonoBehaviour
    {
        private static ModTools s_Instance;

        private bool showUI;

        private static ItemsManager itemsManager;

        private static HUDManager hUDManager;

        private static Player player;

        public static List<ItemID> m_UnlockedItemInfos = new List<ItemID>();

        public bool IsModToolsActive;

        public bool IsOptionInstantFinishConstructionsActive;

        public bool IsLocalOrHost
        {
            get
            {
                if (!ReplTools.AmIMaster())
                {
                    return !ReplTools.IsCoopEnabled();
                }
                return true;
            }
        }

        public bool UseOptionF8
        {
            get
            {
                return IsOptionInstantFinishConstructionsActive;
            }
        }

        public ModTools()
        {
            IsModToolsActive = true;
            s_Instance = this;
        }

        public static ModTools Get()
        {
            return s_Instance;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Pause))
            {
                if (!showUI)
                {
                    itemsManager = ItemsManager.Get();
                    hUDManager = HUDManager.Get();
                    player = Player.Get();
                    EnableCursor(enabled: true);
                }
                showUI = !showUI;
                if (!showUI)
                {
                    EnableCursor();
                }
            }
            if ( !IsOptionInstantFinishConstructionsActive && !IsLocalOrHost && IsModToolsActive && Input.GetKeyDown(KeyCode.F8))
            {
                ShowHUDBigInfo("Feature disabled in multiplayer!", "Mod Tools Info", HUDInfoLogTextureType.Count.ToString());
            }
        }

        private void OnGUI()
        {
            if (showUI)
            {
                InitData();
                InitModUI();
            }
        }

        public static void OnClickUnlockToolsButton()
        {
            try
            {
                InitToolsDataToUnlock();

                UnlockAllToolsInNotepad();
                UnlockAllToolsItemInfosInNotepad();
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[ModTools.ModTools:OnClickUnlockToolsButton] throws exception: " + ex.Message);
            }
        }

        public static void OnClickUnlockWeaponsButton()
        {
            try
            {
                InitWeaponsDataToUnlock();

                UnlockAllWeaponsInNotepad();
                UnlockAllWeaponsItemInfosInNotepad();
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[ModTools.ModTools:OnClickUnlockWeaponsButton] throws exception: " + ex.Message);
            }
        }

        public static void OnClickUnlockArmorButton()
        {
            try
            {
                InitArmorsDataToUnlock();

                UnlockAllArmorInNotepad();
                UnlockAllArmorItemInfosInNotepad();
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[ModTools.ModTools:OnClickUnlockArmorButton] throws exception: " + ex.Message);
            }
        }

        private static void UnlockAllArmorInNotepad()
        {
            foreach (ItemID unlockedArmorItemInfo in m_UnlockedItemInfos)
            {
                if (!itemsManager.m_UnlockedItemInfos.Contains(unlockedArmorItemInfo))
                {
                    itemsManager.UnlockItemInfo(unlockedArmorItemInfo.ToString());
                }
            }
        }

        private static void UnlockAllArmorItemInfosInNotepad()
        {
            foreach (ItemID unlockedArmorItemInfo in m_UnlockedItemInfos)
            {
                if (!itemsManager.m_UnlockedInNotepadItems.Contains(unlockedArmorItemInfo))
                {
                    itemsManager.UnlockItemInNotepad(unlockedArmorItemInfo);
                }
            }
        }

        private static void UnlockAllWeaponsInNotepad()
        {
            foreach (ItemID unlockedWeaponsItemInfo in m_UnlockedItemInfos)
            {
                if (!itemsManager.m_UnlockedItemInfos.Contains(unlockedWeaponsItemInfo))
                {
                    itemsManager.UnlockItemInfo(unlockedWeaponsItemInfo.ToString());
                }
            }
        }

        private static void UnlockAllWeaponsItemInfosInNotepad()
        {
            foreach (ItemID unlockedWeaponsItemInfo in m_UnlockedItemInfos)
            {
                if (!itemsManager.m_UnlockedInNotepadItems.Contains(unlockedWeaponsItemInfo))
                {
                    itemsManager.UnlockItemInNotepad(unlockedWeaponsItemInfo);
                }
            }
        }

        private static void UnlockAllToolsItemInfosInNotepad()
        {
            foreach (ItemID unlockedToolsItemInfo in m_UnlockedItemInfos)
            {
                if (!itemsManager.m_UnlockedItemInfos.Contains(unlockedToolsItemInfo))
                {
                    itemsManager.UnlockItemInfo(unlockedToolsItemInfo.ToString());
                }
            }
        }

        private static void UnlockAllToolsInNotepad()
        {
            foreach (ItemID unlockedToolsItemInfo in m_UnlockedItemInfos)
            {
                if (!itemsManager.m_UnlockedInNotepadItems.Contains(unlockedToolsItemInfo))
                {
                    itemsManager.UnlockItemInNotepad(unlockedToolsItemInfo);
                }
            }
        }

        public static void ShowHUDBigInfo(string text, string header, string textureName)
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

        public static void ShowHUDInfoLog(string itemID, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            ((HUDMessages)hUDManager.GetHUD(typeof(HUDMessages))).AddMessage(localization.Get(localizedTextKey) + "  " + localization.Get(itemID));
        }

        private static void InitData()
        {
            itemsManager = ItemsManager.Get();
            hUDManager = HUDManager.Get();
            player = Player.Get();

            InitSkinUI();
        }

        private void InitModUI()
        {
            GUI.Box(new Rect(10f, 10f, 500f, 150f), "Mod for tools", GUI.skin.window);

            GUI.Label(new Rect(30f, 25f, 300f, 20f), "Click to unlock all crafting tools", GUI.skin.label);
            if (GUI.Button(new Rect(325f, 25f, 200f, 20f), "Unlock tools", GUI.skin.button))
            {
                OnClickUnlockToolsButton();
                showUI = false;
                EnableCursor();
            }

            GUI.Label(new Rect(30f, 50f, 300f, 20f), "Click to unlock all weapons and traps", GUI.skin.label);
            if (GUI.Button(new Rect(325f, 50f, 200f, 20f), "Unlock weapons/traps", GUI.skin.button))
            {
                OnClickUnlockWeaponsButton();
                showUI = false;
                EnableCursor();
            }

            GUI.Label(new Rect(30f, 75f, 300f, 20f), "Click to unlock all armor", GUI.skin.label);
            if (GUI.Button(new Rect(325f, 75f, 200f, 20f), "Unlock armor", GUI.skin.button))
            {
                OnClickUnlockArmorButton();
                showUI = false;
                EnableCursor();
            }

            GUI.Label(new Rect(30f, 100f, 300f, 20f), "Use F8 to instantly finish constructions", GUI.skin.label);
            IsOptionInstantFinishConstructionsActive = GUI.Toggle(new Rect(325, 100f, 20f, 20f), IsOptionInstantFinishConstructionsActive, "");
        }

        private static void InitWeaponsDataToUnlock()
        {
            if (m_UnlockedItemInfos == null)
            {
                m_UnlockedItemInfos = new List<ItemID>();
            }

            foreach (ItemInfo weaponItemInfo in itemsManager.GetAllInfos().Values.Where (
                                                                                                            info =>
                                                                                                                info.IsWeapon()
                                                                                                                || info.IsKnife()
                                                                                                                || info.IsSpear()
                                                                                                                || info.IsAxe()
                                                                                                                || info.IsBow()
                                                                                                                || info.IsArrow()
                                                                                                                || info.IsBlowpipeArrow()
                                                                                                                || info.m_ID == ItemID.Stone_Blade
                                                                                                                || info.m_ID == ItemID.Stick_Blade
                                                                                                                || info.m_ID == ItemID.Obsidian_Blade
                                                                                                                || info.m_ID == ItemID.Obsidian_Bone_Blade
                                                                                                                || info.m_ID == ItemID.Blowpipe_Arrow
                                                                                                                || info.m_ID == ItemID.Bamboo_Blowpipe
                                                                                                                || ItemInfo.IsTrap(info.m_ID)
                                                                                                                ))
            {
                ItemID weaponItemID = weaponItemInfo.m_ID;
                if (!m_UnlockedItemInfos.Contains(weaponItemID))
                {
                    m_UnlockedItemInfos.Add(weaponItemID);
                }
            }
        }

        private static void InitArmorsDataToUnlock()
        {
            if (m_UnlockedItemInfos == null)
            {
                m_UnlockedItemInfos = new List<ItemID>();
            }

            foreach (ItemInfo armorItemInfo in itemsManager.GetAllInfos().Values.Where(
                                                                                                            info =>
                                                                                                                info.IsArmor()
                                                                                                                ))
            {
                ItemID armorItemID = armorItemInfo.m_ID;
                if (!m_UnlockedItemInfos.Contains(armorItemID))
                {
                    m_UnlockedItemInfos.Add(armorItemID);
                }
            }
        }

        private static void InitToolsDataToUnlock()
        {
            if (m_UnlockedItemInfos == null)
            {
                m_UnlockedItemInfos = new List<ItemID>();
            }

            foreach (ItemInfo toolItemInfo in itemsManager.GetAllInfos().Values.Where(
                                                                                                info =>
                                                                                                    info.IsFishingRod()
                                                                                                    || info.IsTorch()
                                                                                                    || info.m_ID == ItemID.Tobacco_Torch
                                                                                                    || info.m_ID == ItemID.Coconut_Bidon
                                                                                                    || info.m_ID == ItemID.Bamboo_Bowl
                                                                                                ))
            {
                ItemID itemToolItemID = toolItemInfo.m_ID;
                if (!m_UnlockedItemInfos.Contains(itemToolItemID))
                {
                    m_UnlockedItemInfos.Add(itemToolItemID);
                }
            }


        }

        private static void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private static void EnableCursor(bool enabled = false)
        {
            CursorManager.Get().ShowCursor(enabled);
            player = Player.Get();
            if (enabled)
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
    }
}
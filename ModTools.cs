using Enums;
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

        public static List<ItemInfo> m_UnlockedToolsItemInfos = new List<ItemInfo>();
        private static bool m_UnlockedTools;
        public static bool HasUnlockedTools => m_UnlockedTools;

        public static List<ItemInfo> m_UnlockedWeaponsTrapsItemInfos = new List<ItemInfo>();
        private static bool m_UnlockedWeaponsTraps;
        public static bool HasUnlockedWeapons => m_UnlockedWeaponsTraps;

        private static bool m_HasBlowgun;
        public static bool HasBlowgun => m_HasBlowgun;

        public static List<ItemInfo> m_UnlockedArmorItemInfos = new List<ItemInfo>();
        private static bool m_UnlockedArmor;
        public static bool HasUnlockedArmor => m_UnlockedArmor;

        private bool m_IsOptionInstantFinishConstructionsActive;
        public bool UseOptionF8 => m_IsOptionInstantFinishConstructionsActive;

        /// <summary>
        /// ModAPI required security check to enable this mod feature for multiplayer.
        /// See <see cref="ModManager"/> for implementation.
        /// Based on request in chat: use  !requestMods in chat as client to request the host to activate mods for them.
        /// </summary>
        /// <returns>true if enabled, else false</returns>
        public bool IsModActiveForMultiplayer => FindObjectOfType(typeof(ModManager.ModManager)) != null ? ModManager.ModManager.AllowModsForMultiplayer : false;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public ModTools()
        {
            s_Instance = this;
        }

        public static ModTools Get()
        {
            return s_Instance;
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

        public static void ShowHUDInfoLog(string ItemInfo, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            ((HUDMessages)hUDManager.GetHUD(typeof(HUDMessages))).AddMessage(localization.Get(localizedTextKey) + "  " + localization.Get(ItemInfo));
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (!showUI)
                {
                    InitData();
                    EnableCursor(enabled: true);
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
                InitModUI();
            }
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
            GUI.Box(new Rect(500f, 10f, 450f, 150f), "ModTools UI - Press END to open/close", GUI.skin.window);

            GUI.Label(new Rect(520f, 30f, 200f, 20f), "Click to unlock fire-water-fishing tools", GUI.skin.label);
            if (GUI.Button(new Rect(770f, 30f, 150f, 20f), "Unlock tools", GUI.skin.button))
            {
                OnClickUnlockToolsButton();
                showUI = false;
                EnableCursor();
            }

            GUI.Label(new Rect(520f, 50f, 300f, 20f), "Click to unlock weapons-traps", GUI.skin.label);
            if (GUI.Button(new Rect(770f, 50f, 150f, 20f), "Unlock weapons/traps", GUI.skin.button))
            {
                OnClickUnlockWeaponsButton();
                showUI = false;
                EnableCursor();
            }

            GUI.Label(new Rect(520f, 70f, 300f, 20f), "Click to unlock all armor", GUI.skin.label);
            if (GUI.Button(new Rect(770f, 70f, 150f, 20f), "Unlock armor", GUI.skin.button))
            {
                OnClickUnlockArmorButton();
                showUI = false;
                EnableCursor();
            }

            CreateF8Option();
        }

        private void CreateF8Option()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                GUI.Label(new Rect(520f, 90f, 300f, 20f), "Use F8 to instantly finish", GUI.skin.label);
                m_IsOptionInstantFinishConstructionsActive = GUI.Toggle(new Rect(770f, 90f, 20f, 20f), m_IsOptionInstantFinishConstructionsActive, "");
            }
            else
            {
                GUI.Label(new Rect(520f, 90f, 330f, 20f), "Use F8 to instantly to finish any constructions", GUI.skin.label);
                GUI.Label(new Rect(520f, 110f, 330f, 20f), "is only for single player or when host", GUI.skin.label);
                GUI.Label(new Rect(520f, 130f, 330f, 20f), "Host can activate using ModManager.", GUI.skin.label);
            }
        }

        private static void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private static void OnClickUnlockToolsButton()
        {
            try
            {
                UnlockAllTools();
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[ModTools.ModTools:OnClickUnlockToolsButton] throws exception: " + ex.Message);
            }
        }

        private static void OnClickUnlockWeaponsButton()
        {
            try
            {
                UnlockAllWeapons();
                //GetBlowgun();
                //GetBlowpipeArrows(5);
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[ModTools.ModTools:OnClickUnlockWeaponsButton] throws exception: " + ex.Message);
            }
        }

        public static void GetBlowgun(int count = 1)
        {
            try
            {
                itemsManager.UnlockItemInfo(ItemID.Bamboo_Blowpipe.ToString());
                ItemInfo blowPipeItemInfo = itemsManager.GetInfo(ItemID.Bamboo_Blowpipe);
                player.AddItemToInventory(blowPipeItemInfo.m_ID.ToString());
                ShowHUDBigInfo($"Added {count} x {itemsManager.GetInfo(ItemID.Bamboo_Blowpipe).GetNameToDisplayLocalized()} to inventory", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                m_HasBlowgun = true;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModTools)}.{nameof(ModTools)}:{nameof(GetBlowgun)}] throws exception: {exc.Message}");
            }
        }

        public static void GetBlowpipeArrows(int count = 1)
        {
            try
            {
                itemsManager.UnlockItemInfo(ItemID.Blowpipe_Arrow.ToString());
                ItemInfo blowPipeArrowItemInfo = itemsManager.GetInfo(ItemID.Blowpipe_Arrow);
                for (int i = 0; i < count; i++)
                {
                    player.AddItemToInventory(blowPipeArrowItemInfo.m_ID.ToString());
                }
                ShowHUDBigInfo($"Added {count} x {itemsManager.GetInfo(ItemID.Blowpipe_Arrow).GetNameToDisplayLocalized()} to inventory", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModTools)}.{nameof(ModTools)}:{nameof(GetBlowpipeArrows)}] throws exception: {exc.Message}");
            }
        }

        private static void OnClickUnlockArmorButton()
        {
            try
            {
                UnlockAllArmor();
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[ModTools.ModTools:OnClickUnlockArmorButton] throws exception: " + ex.Message);
            }
        }

        public static void UnlockAllArmor()
        {
            try
            {
                if (!m_UnlockedArmor)
                {
                    m_UnlockedArmorItemInfos = itemsManager.GetAllInfos().Values.Where(info => info.IsArmor()).ToList();

                    foreach (ItemInfo unlockedArmorItemInfo in m_UnlockedArmorItemInfos)
                    {
                        itemsManager.UnlockItemInNotepad(unlockedArmorItemInfo.m_ID);
                        itemsManager.UnlockItemInfo(unlockedArmorItemInfo.m_ID.ToString());
                        ShowHUDInfoLog(unlockedArmorItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    m_UnlockedArmor = true;
                }
                else
                {
                    ShowHUDBigInfo("All armor were already unlocked!", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModTools)}.{nameof(ModTools)}:{nameof(UnlockAllArmor)}] throws exception: {exc.Message}");
            }
        }

        public static void UnlockAllWeapons()
        {
            try
            {
                if (!m_UnlockedWeaponsTraps)
                {
                    m_UnlockedWeaponsTrapsItemInfos = itemsManager.GetAllInfos().Values.Where(info => info.IsWeapon() || ItemInfo.IsTrap(info.m_ID)).ToList();

                    foreach (ItemInfo unlockedWeaponTrapItemInfo in m_UnlockedWeaponsTrapsItemInfos)
                    {
                        itemsManager.UnlockItemInNotepad(unlockedWeaponTrapItemInfo.m_ID);
                        itemsManager.UnlockItemInfo(unlockedWeaponTrapItemInfo.m_ID.ToString());
                        ShowHUDInfoLog(unlockedWeaponTrapItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    m_UnlockedWeaponsTraps = true;
                }
                else
                {
                    ShowHUDBigInfo("All weapons and traps were already unlocked", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModTools)}.{nameof(ModTools)}:{nameof(UnlockAllWeapons)}] throws exception: {exc.Message}");
            }
        }

        public static void UnlockAllTools()
        {
            try
            {
                if (!m_UnlockedTools)
                {
                    m_UnlockedToolsItemInfos = itemsManager.GetAllInfos().Values.Where(info =>
                                                                                                                                                                info.IsTool() || info.IsTorch() || info.IsFishingRod()
                                                                                                                                                                || ItemInfo.IsSmoker(info.m_ID) || ItemInfo.IsStoneRing(info.m_ID) || ItemInfo.IsFirecamp(info.m_ID)).ToList();

                    UnlockFireTools();
                    UnlockFishingTools();
                    UnlockWaterTools();

                    foreach (ItemInfo unlockedToolsItemInfo in m_UnlockedToolsItemInfos)
                    {
                        itemsManager.UnlockItemInfo(unlockedToolsItemInfo.m_ID.ToString());
                        itemsManager.UnlockItemInNotepad(unlockedToolsItemInfo.m_ID);
                        ShowHUDInfoLog(unlockedToolsItemInfo.m_ID.ToString(), "HUD_InfoLog_NewEntry");
                    }
                    m_UnlockedTools = true;
                }
                else
                {
                    ShowHUDBigInfo("All tools were already unlocked", "ModTools Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModTools)}.{nameof(ModTools)}:{nameof(UnlockAllTools)}] throws exception: {exc.Message}");
            }
        }

        public static void UnlockWaterTools()
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

        public static void UnlockFishingTools()
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

        public static void UnlockFireTools()
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

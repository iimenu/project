using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using iiMenu.Classes.Menu;
using iiMenu.Menu;
using iiMenu.Utilities;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace iiMenu.Managers
{
    [HarmonyPatch(typeof(GorillaTagger), "LateUpdate")]
    public class AdminIdentifierManager
    {
        public static bool adminBypass;
        public static bool showOtherAdminCosmetics = true;
        public static bool grabAdminStick;
        public static bool grabForestGuide;
        public static readonly HashSet<string> activeCosmetics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<VRRig, GameObject> adminTags = new Dictionary<VRRig, GameObject>();
        private static readonly List<KeyValuePair<VRRig, GameObject>> adminTagsSnapshot = new List<KeyValuePair<VRRig, GameObject>>();
        private static bool greetedAdmin;

        public static TelemetryClient.AdminEntry FindAdmin(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            foreach (TelemetryClient.AdminEntry entry in TelemetryClient.VerifiedAdmins)
                if (string.Equals(entry.Id, userId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            return null;
        }

        public static bool IsAdminCosmetic(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return false;
            string id = itemName.TrimEnd('.');
            return string.Equals(id, "LBAAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LBANI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LBAAK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LBADE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LBAGS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LMAPY", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LHAAG", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "LBAAE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "ADMINISTRATOR BADGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "AdministratorBadge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "AA CREATOR BADGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "AaCreatorBadge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "FINGER PAINTER BADGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "FingerPainterBadge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "ILLUSTRATOR BADGE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "IllustratorBadge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "MOD STICK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "GOLDEN HEAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "EARLY ACCESS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "earlyaccess", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "FOREST GUIDE MOD STICK", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetLocalUserId()
        {
            try
            {
                if (PlayFabAuthenticator.instance != null)
                {
                    string id = PlayFabAuthenticator.instance.GetPlayFabPlayerId();
                    if (!string.IsNullOrEmpty(id)) return id;
                }
            }
            catch { }
            try
            {
                if (PhotonNetwork.LocalPlayer != null && !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
                    return PhotonNetwork.LocalPlayer.UserId;
            }
            catch { }
            try
            {
                if (NetworkSystem.Instance != null && NetworkSystem.Instance.LocalPlayer != null && !string.IsNullOrEmpty(NetworkSystem.Instance.LocalPlayer.UserId))
                    return NetworkSystem.Instance.LocalPlayer.UserId;
            }
            catch { }
            try
            {
                if (VRRig.LocalRig != null)
                {
                    NetPlayer player = RigUtilities.GetPlayerFromVRRig(VRRig.LocalRig);
                    if (player != null && !string.IsNullOrEmpty(player.UserId))
                        return player.UserId;
                }
            }
            catch { }
            return null;
        }

        public static string GetAdminName(TelemetryClient.AdminEntry entry)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Role))
            {
                int start = entry.Role.IndexOf('(');
                int end = entry.Role.IndexOf(')');
                if (start >= 0 && end > start)
                    return entry.Role.Substring(start + 1, end - start - 1).Trim();
                return entry.Role.Trim();
            }
            return "Admin";
        }

        public static void CheckAdminGreeting()
        {
            if (greetedAdmin) return;
            string userId = GetLocalUserId();
            if (string.IsNullOrEmpty(userId)) return;
            TelemetryClient.AdminEntry entry = FindAdmin(userId);
            if (entry != null)
            {
                greetedAdmin = true;
                NotificationManager.SendNotification($"hello, {GetAdminName(entry)}.");
            }
        }

        public static bool IsAdmin()
        {
            if (adminBypass) return true;
            string userId = GetLocalUserId();
            return !string.IsNullOrEmpty(userId) && FindAdmin(userId) != null;
        }

        public static int GetSlotForId(string cleanId)
        {
            switch (cleanId.ToUpperInvariant())
            {
                case "LHAAG":
                case "LBAAE":
                    return (int)CosmeticsController.CosmeticSlots.Hat;
                case "LBAAD":
                case "LBANI":
                case "LBADE":
                case "LBAGS":
                    return (int)CosmeticsController.CosmeticSlots.Badge;
                case "LBAAK":
                    return (int)CosmeticsController.CosmeticSlots.ArmLeft;
                case "LMAPY":
                    return (int)CosmeticsController.CosmeticSlots.ArmRight;
                default:
                    return -1;
            }
        }

        public static CosmeticsController.CosmeticItem GetCosmeticItem(string id)
        {
            if (CosmeticsController.instance == null) return default;
            string cleanId = id.TrimEnd('.');

            var item = CosmeticsController.instance.GetItemFromDict(cleanId + ".");
            if (!item.isNullItem) return item;
            item = CosmeticsController.instance.GetItemFromDict(cleanId);
            if (!item.isNullItem) return item;

            if (CosmeticsController.instance.allCosmetics != null)
            {
                foreach (var c in CosmeticsController.instance.allCosmetics)
                {
                    if (string.Equals(c.itemName, cleanId + ".", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.itemName, cleanId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.displayName, cleanId + ".", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.displayName, cleanId, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
            }

            CosmeticsController.CosmeticCategory category = CosmeticsController.CosmeticCategory.Badge;
            bool isHoldable = false;
            if (cleanId == "LBAAK" || cleanId == "LMAPY") { category = CosmeticsController.CosmeticCategory.Paw; isHoldable = true; }
            else if (cleanId == "LHAAG" || cleanId == "LBAAE") category = CosmeticsController.CosmeticCategory.Hat;

            item = new CosmeticsController.CosmeticItem
            {
                itemName = cleanId + ".",
                displayName = cleanId == "LBAAK" ? "MOD STICK" : (cleanId == "LMAPY" ? "FOREST GUIDE MOD STICK" : (cleanId == "LBAAD" ? "ADMINISTRATOR BADGE" : cleanId + ".")),
                overrideDisplayName = cleanId == "LBAAD" ? "ADMINISTRATOR BADGE" : (cleanId == "LBAAK" ? "MOD STICK" : (cleanId == "LMAPY" ? "FOREST GUIDE MOD STICK" : cleanId)),
                itemCategory = category,
                isHoldable = isHoldable,
                isNullItem = false,
                bundledItems = new string[0]
            };

            if (CosmeticsController.instance.allCosmetics != null && !CosmeticsController.instance.allCosmetics.Any(c => c.itemName == item.itemName))
                CosmeticsController.instance.allCosmetics.Add(item);
            if (CosmeticsController.instance.allCosmeticsDict != null)
            {
                CosmeticsController.instance.allCosmeticsDict[item.itemName] = item;
                CosmeticsController.instance.allCosmeticsDict[cleanId] = item;
                if (cleanId == "LBAAD")
                {
                    CosmeticsController.instance.allCosmeticsDict["ADMINISTRATOR BADGE"] = item;
                    CosmeticsController.instance.allCosmeticsDict["AdministratorBadge"] = item;
                }
                else if (cleanId == "LBAAK")
                {
                    CosmeticsController.instance.allCosmeticsDict["MOD STICK"] = item;
                    CosmeticsController.instance.allCosmeticsDict["Admin Stick"] = item;
                }
                else if (cleanId == "LMAPY")
                {
                    CosmeticsController.instance.allCosmeticsDict["FOREST GUIDE MOD STICK"] = item;
                    CosmeticsController.instance.allCosmeticsDict["Forest Guide"] = item;
                }
            }
            if (CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict != null)
            {
                CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict[item.displayName] = item.itemName;
                CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict[cleanId] = item.itemName;
                CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict[cleanId + "."] = item.itemName;
                if (cleanId == "LBAAD")
                {
                    CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict["ADMINISTRATOR BADGE"] = "LBAAD.";
                    CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict["AdministratorBadge"] = "LBAAD.";
                }
                else if (cleanId == "LBAAK")
                {
                    CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict["MOD STICK"] = "LBAAK.";
                    CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict["Admin Stick"] = "LBAAK.";
                }
                else if (cleanId == "LMAPY")
                {
                    CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict["FOREST GUIDE MOD STICK"] = "LMAPY.";
                    CosmeticsController.instance.allCosmeticsItemIDsfromDisplayNamesDict["Forest Guide"] = "LMAPY.";
                }
            }
            return item;
        }

        public static void ToggleCosmetic(string cosmeticId, bool enable)
        {
            string clean = cosmeticId.TrimEnd('.');
            if (enable) activeCosmetics.Add(cosmeticId);
            else
            {
                activeCosmetics.Remove(cosmeticId);
                activeCosmetics.Remove(clean);
                if (clean == "LBAAK")
                {
                    grabAdminStick = false;
                    ButtonInfo g = Buttons.GetIndex("Grab Admin Stick");
                    if (g != null) g.enabled = false;
                }
                else if (clean == "LMAPY")
                {
                    grabForestGuide = false;
                    ButtonInfo g = Buttons.GetIndex("Grab Forest Guide");
                    if (g != null) g.enabled = false;
                }
            }
            SyncLocalProperties();
            ApplyLocalCosmetics();
        }

        public static void GrabHoldable(string cleanId, bool grab, bool leftHand = false)
        {
            string clean = cleanId.TrimEnd('.');
            if (clean == "LBAAK") grabAdminStick = grab;
            else if (clean == "LMAPY") grabForestGuide = grab;

            if (grab)
            {
                if (!activeCosmetics.Contains(clean + "."))
                {
                    activeCosmetics.Add(clean + ".");
                    ButtonInfo btn = Buttons.GetIndex(GetButtonNameForId(clean));
                    if (btn != null) btn.enabled = true;
                    SyncLocalProperties();
                }
            }
            ApplyLocalCosmetics();
        }

        public static void EnableAll()
        {
            string[] all = { "LBAAD.", "LBANI.", "LBAAK.", "LBADE.", "LBAGS.", "LMAPY." };
            foreach (string id in all)
            {
                activeCosmetics.Add(id);
                ButtonInfo btn = Buttons.GetIndex(GetButtonNameForId(id));
                if (btn != null) btn.enabled = true;
            }
            SyncLocalProperties();
            ApplyLocalCosmetics();
        }

        public static void DisableAll()
        {
            string[] all = { "LBAAD.", "LBANI.", "LBAAK.", "LBADE.", "LBAGS.", "LMAPY." };
            activeCosmetics.Clear();
            grabAdminStick = false;
            grabForestGuide = false;
            foreach (string id in all)
            {
                ButtonInfo btn = Buttons.GetIndex(GetButtonNameForId(id));
                if (btn != null) btn.enabled = false;
            }
            ButtonInfo g1 = Buttons.GetIndex("Grab Admin Stick");
            if (g1 != null) g1.enabled = false;
            ButtonInfo g2 = Buttons.GetIndex("Grab Forest Guide");
            if (g2 != null) g2.enabled = false;
            SyncLocalProperties();
            ApplyLocalCosmetics();
        }

        private static string GetButtonNameForId(string id)
        {
            switch (id.TrimEnd('.'))
            {
                case "LBAAD": return "Admin Badge";
                case "LBANI": return "AA Creator";
                case "LBAAK": return "Admin Stick";
                case "LBADE": return "Finger Painter";
                case "LBAGS": return "Illustrator";
                case "LMAPY": return "Forest Guide";
                default: return null;
            }
        }

        public static void SyncLocalProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
            string value = string.Join(",", activeCosmetics);
            Hashtable props = new Hashtable { { "iiAdminCosmetic", value } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public static TransferrableObject GetHoldable(VRRig rig, string cleanId)
        {
            if (rig == null) return null;
            BodyDockPositions bDock = rig.GetComponent<BodyDockPositions>();
            if (cleanId == "LBAAK")
            {
                if (bDock?.allObjects != null && bDock.allObjects.Length > 28 && bDock.allObjects[28] != null)
                    return bDock.allObjects[28];
            }
            else if (cleanId == "LMAPY")
            {
                if (bDock?.allObjects != null && bDock.allObjects.Length > 638 && bDock.allObjects[638] != null)
                    return bDock.allObjects[638];
            }
            if (rig.cosmeticsObjectRegistry != null)
            {
                var inst = rig.cosmeticsObjectRegistry.Cosmetic(cleanId + ".");
                if (inst?.holdableObjects != null)
                {
                    foreach (GameObject go in inst.holdableObjects)
                    {
                        if (go == null) continue;
                        var to = go.GetComponentInChildren<TransferrableObject>(true);
                        if (to != null) return to;
                    }
                }
            }
            if (bDock?.allObjects != null)
            {
                foreach (var to in bDock.allObjects)
                {
                    if (to == null) continue;
                    string name = to.gameObject.name.ToUpperInvariant();
                    if (cleanId == "LBAAK" && (name.Contains("MOD STICK") || name.Contains("STICK") || name.Contains("LBAAK")))
                        return to;
                    if (cleanId == "LMAPY" && (name.Contains("FOREST") || name.Contains("GUIDE") || name.Contains("LMAPY")))
                        return to;
                }
            }
            return null;
        }

        public static void EnableHoldable(string cleanId, BodyDockPositions.DropPositions dropPos, TransferrableObject.PositionState posState)
        {
            VRRig offline = GorillaTagger.Instance?.offlineVRRig;
            VRRig local = VRRig.LocalRig ?? offline;
            if (offline == null) return;

            BodyDockPositions offDock = offline.GetComponent<BodyDockPositions>();
            if (offDock == null) return;

            TransferrableObject to = GetHoldable(offline, cleanId);
            if (to == null && offline.cosmeticsObjectRegistry != null)
            {
                try { CosmeticsV2Spawner_Dirty.ProcessLoadOpInfos(offline, cleanId + ".", offline.cosmeticsObjectRegistry); } catch { }
                to = GetHoldable(offline, cleanId);
            }
            if (to == null) return;

            int itemIdx = to.myIndex >= 0 ? to.myIndex : (cleanId == "LBAAK" ? 28 : 638);
            to.myIndex = itemIdx;

            if (offDock._allObjects != null && itemIdx >= 0 && itemIdx < offDock._allObjects.Length)
                offDock._allObjects[itemIdx] = to;

            int slot = -1;
            for (int i = 0; i < offline.ActiveTransferrableObjectIndexLength(); i++)
            {
                if (offline.ActiveTransferrableObjectIndex(i) == itemIdx)
                {
                    slot = i;
                    break;
                }
                if (slot == -1 && offline.ActiveTransferrableObjectIndex(i) == -1)
                    slot = i;
            }

            if (slot != -1)
            {
                offline.SetActiveTransferrableObjectIndex(slot, itemIdx);
                offline.SetTransferrablePosStates(slot, posState);
                offline.SetTransferrableItemStates(slot, (TransferrableObject.ItemStates)0);
                offline.SetTransferrableDockPosition(slot, dropPos);
            }

            to.storedZone = dropPos;
            to.currentState = posState;
            to.SetTargetRig(offline);
            to.myRig = offline;
            to.canAutoGrabLeft = true;
            to.canAutoGrabRight = true;
            to.allowPlayerStealing = true;

            if (to.gripInteractor == null)
                to.gripInteractor = to.GetComponentInChildren<InteractionPoint>(true);

            if (to.gripInteractor != null)
            {
                to.gripInteractor.parentHoldableObject = to.gameObject;
                to.gripInteractor.forLocalPlayer = true;
                to.gripInteractor.interactor = EquipmentInteractor.instance;
                to.gripInteractor.enabled = true;
                if (to.gripInteractor.myCollider == null)
                    to.gripInteractor.myCollider = to.gripInteractor.GetComponent<Collider>();
                if (to.gripInteractor.myCollider != null)
                    to.gripInteractor.myCollider.enabled = true;
            }

            to.gameObject.SetActive(true);

            if (posState == TransferrableObject.PositionState.InRightHand && EquipmentInteractor.instance != null)
                EquipmentInteractor.instance.UpdateHandEquipment(to, false);
            else if (posState == TransferrableObject.PositionState.InLeftHand && EquipmentInteractor.instance != null)
                EquipmentInteractor.instance.UpdateHandEquipment(to, true);

            if (offline.cosmeticsObjectRegistry != null)
            {
                var inst = offline.cosmeticsObjectRegistry.Cosmetic(cleanId + ".");
                if (inst?.holdableObjects != null)
                {
                    foreach (GameObject go in inst.holdableObjects)
                        if (go != null) go.SetActive(true);
                }
            }

            if (local != null && local != offline)
            {
                BodyDockPositions locDock = local.GetComponent<BodyDockPositions>();
                TransferrableObject locTo = GetHoldable(local, cleanId);
                if (locTo == null && local.cosmeticsObjectRegistry != null)
                {
                    try { CosmeticsV2Spawner_Dirty.ProcessLoadOpInfos(local, cleanId + ".", local.cosmeticsObjectRegistry); } catch { }
                    locTo = GetHoldable(local, cleanId);
                }
                if (locTo != null)
                {
                    locTo.myIndex = itemIdx;
                    if (locDock != null && locDock._allObjects != null && itemIdx >= 0 && itemIdx < locDock._allObjects.Length)
                        locDock._allObjects[itemIdx] = locTo;
                    locTo.storedZone = dropPos;
                    locTo.currentState = posState;
                    locTo.SetTargetRig(local);
                    locTo.gameObject.SetActive(true);
                }
                if (slot != -1)
                {
                    local.SetActiveTransferrableObjectIndex(slot, itemIdx);
                    local.SetTransferrablePosStates(slot, posState);
                    local.SetTransferrableItemStates(slot, (TransferrableObject.ItemStates)0);
                    local.SetTransferrableDockPosition(slot, dropPos);
                }
                if (local.cosmeticsObjectRegistry != null)
                {
                    var inst = local.cosmeticsObjectRegistry.Cosmetic(cleanId + ".");
                    if (inst?.holdableObjects != null)
                    {
                        foreach (GameObject go in inst.holdableObjects)
                            if (go != null) go.SetActive(true);
                    }
                }
                try { locDock?.RefreshTransferrableItems(); } catch { }
            }
        }

        public static void DisableHoldable(string cleanId)
        {
            VRRig offline = GorillaTagger.Instance?.offlineVRRig;
            VRRig local = VRRig.LocalRig ?? offline;
            int itemIdx = cleanId == "LBAAK" ? 28 : (cleanId == "LMAPY" ? 638 : -1);

            if (offline != null)
            {
                TransferrableObject to = GetHoldable(offline, cleanId);
                if (to != null)
                {
                    itemIdx = to.myIndex;
                    to.storedZone = BodyDockPositions.DropPositions.None;
                    to.currentState = TransferrableObject.PositionState.None;
                    if (EquipmentInteractor.instance != null)
                    {
                        if (EquipmentInteractor.instance.leftHandHeldEquipment == (IHoldableObject)to)
                            EquipmentInteractor.instance.leftHandHeldEquipment = null;
                        if (EquipmentInteractor.instance.rightHandHeldEquipment == (IHoldableObject)to)
                            EquipmentInteractor.instance.rightHandHeldEquipment = null;
                    }
                    to.gameObject.SetActive(false);
                }
                for (int i = 0; i < offline.ActiveTransferrableObjectIndexLength(); i++)
                {
                    if (offline.ActiveTransferrableObjectIndex(i) == itemIdx)
                        offline.SetActiveTransferrableObjectIndex(i, -1);
                }
                if (offline.cosmeticsObjectRegistry != null)
                {
                    var inst = offline.cosmeticsObjectRegistry.Cosmetic(cleanId + ".");
                    if (inst?.holdableObjects != null)
                    {
                        foreach (GameObject go in inst.holdableObjects)
                            if (go != null) go.SetActive(false);
                    }
                }
            }

            if (local != null && local != offline)
            {
                TransferrableObject locTo = GetHoldable(local, cleanId);
                if (locTo != null)
                {
                    locTo.storedZone = BodyDockPositions.DropPositions.None;
                    locTo.currentState = TransferrableObject.PositionState.None;
                    locTo.gameObject.SetActive(false);
                }
                for (int i = 0; i < local.ActiveTransferrableObjectIndexLength(); i++)
                {
                    if (local.ActiveTransferrableObjectIndex(i) == itemIdx)
                        local.SetActiveTransferrableObjectIndex(i, -1);
                }
                if (local.cosmeticsObjectRegistry != null)
                {
                    var inst = local.cosmeticsObjectRegistry.Cosmetic(cleanId + ".");
                    if (inst?.holdableObjects != null)
                    {
                        foreach (GameObject go in inst.holdableObjects)
                            if (go != null) go.SetActive(false);
                    }
                }
                try { local.GetComponent<BodyDockPositions>()?.RefreshTransferrableItems(); } catch { }
            }
        }

        public static void ApplyLocalCosmetics()
        {
            VRRig local = VRRig.LocalRig ?? GorillaTagger.Instance?.offlineVRRig;
            VRRig offline = GorillaTagger.Instance?.offlineVRRig;
            CosmeticsController controller = CosmeticsController.instance;
            if (controller == null) return;

            string[] all = { "LBAAD.", "LBANI.", "LBAAK.", "LBADE.", "LBAGS.", "LMAPY." };
            foreach (string id in all)
            {
                string clean = id.TrimEnd('.');
                int slot = GetSlotForId(clean);
                if (slot < 0) continue;

                bool shouldWear = activeCosmetics.Contains(id) || activeCosmetics.Contains(clean);
                var item = GetCosmeticItem(clean);
                CosmeticsController.CosmeticSlots cSlot = (CosmeticsController.CosmeticSlots)slot;

                if (local != null)
                {
                    local.AddCosmetic(id);
                    local.AddCosmetic(clean);
                    if (clean == "LBAAD")
                    {
                        local.AddCosmetic("AdministratorBadge");
                        local.AddCosmetic("ADMINISTRATOR BADGE");
                    }
                }
                if (offline != null && offline != local)
                {
                    offline.AddCosmetic(id);
                    offline.AddCosmetic(clean);
                    if (clean == "LBAAD")
                    {
                        offline.AddCosmetic("AdministratorBadge");
                        offline.AddCosmetic("ADMINISTRATOR BADGE");
                    }
                }

                if (!string.IsNullOrEmpty(controller.concatStringCosmeticsAllowed) && !controller.concatStringCosmeticsAllowed.Contains(clean))
                    controller.concatStringCosmeticsAllowed += clean + ".";

                if (shouldWear)
                {
                    if (clean == "LBAAK")
                    {
                        var posState = grabAdminStick ? TransferrableObject.PositionState.InRightHand : TransferrableObject.PositionState.OnLeftArm;
                        EnableHoldable("LBAAK", BodyDockPositions.DropPositions.LeftArm, posState);
                    }
                    else if (clean == "LMAPY")
                    {
                        var posState = grabForestGuide ? TransferrableObject.PositionState.InRightHand : TransferrableObject.PositionState.OnRightArm;
                        EnableHoldable("LMAPY", BodyDockPositions.DropPositions.RightArm, posState);
                    }
                    else
                    {
                        if (local != null && local.cosmeticsObjectRegistry != null)
                        {
                            try { CosmeticsV2Spawner_Dirty.ProcessLoadOpInfos(local, clean + ".", local.cosmeticsObjectRegistry); } catch { }
                            var inst = local.cosmeticsObjectRegistry.Cosmetic(clean + ".");
                            if (inst != null)
                            {
                                inst.EnableItem(cSlot, local);
                                foreach (GameObject o in inst.objects)
                                    if (o != null) o.SetActive(true);
                            }
                        }
                        if (offline != null && offline != local && offline.cosmeticsObjectRegistry != null)
                        {
                            try { CosmeticsV2Spawner_Dirty.ProcessLoadOpInfos(offline, clean + ".", offline.cosmeticsObjectRegistry); } catch { }
                            var inst = offline.cosmeticsObjectRegistry.Cosmetic(clean + ".");
                            if (inst != null)
                            {
                                inst.EnableItem(cSlot, offline);
                                foreach (GameObject o in inst.objects)
                                    if (o != null) o.SetActive(true);
                            }
                        }
                    }

                    if (controller.currentWornSet?.items != null && controller.currentWornSet.items.Length > slot)
                        controller.currentWornSet.items[slot] = item;
                    if (local?.cosmeticSet?.items != null && local.cosmeticSet.items.Length > slot)
                        local.cosmeticSet.items[slot] = item;
                    if (local?.tryOnSet?.items != null && local.tryOnSet.items.Length > slot)
                        local.tryOnSet.items[slot] = item;
                }
                else
                {
                    if (clean == "LBAAK" || clean == "LMAPY")
                        DisableHoldable(clean);
                    else
                    {
                        if (local != null && local.cosmeticsObjectRegistry != null)
                        {
                            var inst = local.cosmeticsObjectRegistry.Cosmetic(clean + ".");
                            if (inst != null) inst.DisableItem(cSlot);
                        }
                        if (offline != null && offline != local && offline.cosmeticsObjectRegistry != null)
                            offline.cosmeticsObjectRegistry.Cosmetic(clean + ".")?.DisableItem(cSlot);
                    }

                    if (controller.currentWornSet?.items != null && controller.currentWornSet.items.Length > slot)
                        controller.currentWornSet.items[slot] = controller.nullItem;
                    if (local?.cosmeticSet?.items != null && local.cosmeticSet.items.Length > slot)
                        local.cosmeticSet.items[slot] = controller.nullItem;
                    if (local?.tryOnSet?.items != null && local.tryOnSet.items.Length > slot)
                        local.tryOnSet.items[slot] = controller.nullItem;
                }
            }

            try { controller.UpdateWornCosmetics(PhotonNetwork.InRoom); } catch { }
            try { local?.RefreshCosmetics(); } catch { }
            try { if (offline != null && offline != local) offline.RefreshCosmetics(); } catch { }
        }

        private static IEnumerator LoadAvatarCoroutine(string url, Renderer renderer)
        {
            if (string.IsNullOrEmpty(url)) yield break;
            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success && renderer != null)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(req);
                    Material mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader"));
                    mat.mainTexture = texture;
                    renderer.material = mat;
                }
            }
        }

        public static void Postfix()
        {
            CheckAdminGreeting();

            if (!PhotonNetwork.InRoom)
            {
                if (adminTags.Count > 0)
                {
                    foreach (var tag in adminTags)
                        if (tag.Value != null) UnityEngine.Object.Destroy(tag.Value);
                    adminTags.Clear();
                }
                return;
            }

            adminTagsSnapshot.Clear();
            foreach (var tag in adminTags)
                adminTagsSnapshot.Add(tag);
            foreach (var tag in adminTagsSnapshot)
            {
                if (!VRRigCache.ActiveRigs.Contains(tag.Key))
                {
                    if (tag.Value != null) UnityEngine.Object.Destroy(tag.Value);
                    adminTags.Remove(tag.Key);
                }
            }

            Camera mainCam = Camera.main;

            foreach (var rig in VRRigCache.ActiveRigs)
            {
                if (rig == null || rig.isLocal || rig.isOfflineVRRig) continue;

                NetPlayer player = RigUtilities.GetPlayerFromVRRig(rig);
                string userId = player != null ? player.UserId : null;
                if (string.IsNullOrEmpty(userId)) continue;

                TelemetryClient.AdminEntry profile = FindAdmin(userId);
                if (profile != null)
                {
                    if (!adminTags.ContainsKey(rig))
                    {
                        NotificationManager.SendNotification($"<color=white>[</color><color=orange>iiMenu</color><color=white>]</color> {profile.Role} in lobby!");

                        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                        go.name = "iiMenu_AdminIdentifierTag";

                        if (!string.IsNullOrEmpty(profile.Avatar))
                        {
                            if (profile.Avatar.StartsWith("https://"))
                                GorillaTagger.Instance.StartCoroutine(LoadAvatarCoroutine(profile.Avatar, go.GetComponent<Renderer>()));
                            else
                            {
                                try
                                {
                                    Texture2D texture = AssetUtilities.LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.{profile.Avatar}.png");
                                    if (texture != null)
                                    {
                                        Material mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader"));
                                        mat.mainTexture = texture;
                                        go.GetComponent<Renderer>().material = mat;
                                    }
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            go.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader")) { color = Color.clear };
                        }

                        adminTags.Add(rig, go);
                    }

                    if (adminTags.TryGetValue(rig, out GameObject tagObj) && tagObj != null)
                    {
                        tagObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f) * rig.scaleFactor;
                        tagObj.transform.position = rig.headMesh.transform.position + new Vector3(0f, 0.60f * rig.scaleFactor, 0f);
                        if (mainCam != null)
                        {
                            tagObj.transform.LookAt(mainCam.transform.position);
                            tagObj.transform.Rotate(0f, 180f, 0f);
                        }
                    }

                    if (showOtherAdminCosmetics)
                    {
                        Hashtable props = null;
                        try
                        {
                            var p = RigUtilities.NetPlayerToPlayer(player);
                            if (p != null) props = p.CustomProperties;
                        }
                        catch { }

                        object val = null;
                        if (props != null && props.TryGetValue("iiAdminCosmetic", out val) && val is string str && !string.IsNullOrEmpty(str))
                        {
                            foreach (string cosmetic in str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string cClean = cosmetic.TrimEnd('.');
                                int cSlot = GetSlotForId(cClean);
                                if (cSlot >= 0 && rig.cosmeticsObjectRegistry != null)
                                {
                                    try { CosmeticsV2Spawner_Dirty.ProcessLoadOpInfos(rig, cClean + ".", rig.cosmeticsObjectRegistry); } catch { }
                                    var inst = rig.cosmeticsObjectRegistry.Cosmetic(cClean + ".");
                                    if (inst != null)
                                    {
                                        inst.EnableItem((CosmeticsController.CosmeticSlots)cSlot, rig);
                                        if (inst.holdableObjects != null)
                                        {
                                            foreach (GameObject h in inst.holdableObjects)
                                                if (h != null) h.SetActive(true);
                                        }
                                    }
                                    if (cClean == "LBAAK" || cClean == "LMAPY")
                                    {
                                        TransferrableObject otherTo = GetHoldable(rig, cClean);
                                        if (otherTo != null)
                                        {
                                            otherTo.storedZone = cClean == "LBAAK" ? BodyDockPositions.DropPositions.LeftArm : BodyDockPositions.DropPositions.RightArm;
                                            otherTo.currentState = cClean == "LBAAK" ? TransferrableObject.PositionState.OnLeftArm : TransferrableObject.PositionState.OnRightArm;
                                            otherTo.SetTargetRig(rig);
                                            otherTo.gameObject.SetActive(true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (adminTags.TryGetValue(rig, out GameObject tagObj))
                    {
                        if (tagObj != null) UnityEngine.Object.Destroy(tagObj);
                        adminTags.Remove(rig);
                    }
                }
            }

            VRRig offlineRig = GorillaTagger.Instance?.offlineVRRig;
            VRRig localRig = VRRig.LocalRig;
            if (offlineRig != null)
            {
                if (activeCosmetics.Contains("LBAAK.") || activeCosmetics.Contains("LBAAK"))
                {
                    TransferrableObject to = GetHoldable(offlineRig, "LBAAK");
                    if (to != null && to.gameObject.activeSelf)
                    {
                        if (localRig != null && localRig != offlineRig)
                        {
                            TransferrableObject locTo = GetHoldable(localRig, "LBAAK");
                            if (locTo != null && locTo.currentState != to.currentState)
                            {
                                locTo.currentState = to.currentState;
                                for (int i = 0; i < localRig.ActiveTransferrableObjectIndexLength(); i++)
                                {
                                    if (localRig.ActiveTransferrableObjectIndex(i) == to.myIndex)
                                    {
                                        localRig.SetTransferrablePosStates(i, to.currentState);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (activeCosmetics.Contains("LMAPY.") || activeCosmetics.Contains("LMAPY"))
                {
                    TransferrableObject to = GetHoldable(offlineRig, "LMAPY");
                    if (to != null && to.gameObject.activeSelf)
                    {
                        if (localRig != null && localRig != offlineRig)
                        {
                            TransferrableObject locTo = GetHoldable(localRig, "LMAPY");
                            if (locTo != null && locTo.currentState != to.currentState)
                            {
                                locTo.currentState = to.currentState;
                                for (int i = 0; i < localRig.ActiveTransferrableObjectIndexLength(); i++)
                                {
                                    if (localRig.ActiveTransferrableObjectIndex(i) == to.myIndex)
                                    {
                                        localRig.SetTransferrablePosStates(i, to.currentState);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.digit0Key.wasPressedThisFrame)
            {
                bool foundAny = false;
                foreach (var vrrig in VRRigCache.ActiveRigs)
                {
                    if (vrrig == null) continue;
                    NetPlayer player = RigUtilities.GetPlayerFromVRRig(vrrig);
                    string userId = player != null ? player.UserId : null;
                    if (string.IsNullOrEmpty(userId)) continue;
                    TelemetryClient.AdminEntry profile = FindAdmin(userId);
                    if (profile != null)
                    {
                        NotificationManager.SendNotification($"<color=white>[</color><color=orange>iiMenu</color><color=white>]</color> {profile.Role} in lobby!");
                        foundAny = true;
                    }
                }
                if (!foundAny)
                    NotificationManager.SendNotification("No admins currently in lobby!");
            }
        }
    }
}

namespace iiMenu.Mods
{
    public class Admin : iiMenu.Managers.AdminIdentifierManager
    {
    }
}
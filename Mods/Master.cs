using GorillaLocomotion.Gameplay;
using UnityEngine;

using BepInEx;
using GorillaExtensions;
using GorillaGameModes;
using GorillaNetworking;
using GorillaTagScripts;
using HarmonyLib;
using iiMenu.Extensions;
using iiMenu.Managers;
using iiMenu.Patches.Menu;
using iiMenu.Utilities;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace iiMenu.Mods
{
    public class Master : MonoBehaviour
    {
        public static void FastBroomsticks()
        {
            if (!NetworkSystem.Instance.IsMasterClient)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> You are not master client.");
                Menu.Main.Toggle("Fast Broomsticks");
                return;
            }

            foreach (GorillaLocomotion.Gameplay.NoncontrollableBroomstick broomstick in UnityEngine.Resources.FindObjectsOfTypeAll<GorillaLocomotion.Gameplay.NoncontrollableBroomstick>())
                broomstick.duration = 10f;
        }

        public static void SlowBroomsticks()
        {
            if (!NetworkSystem.Instance.IsMasterClient)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> You are not master client.");
                Menu.Main.Toggle("Slow Broomsticks");
                return;
            }

            foreach (GorillaLocomotion.Gameplay.NoncontrollableBroomstick broomstick in UnityEngine.Resources.FindObjectsOfTypeAll<GorillaLocomotion.Gameplay.NoncontrollableBroomstick>())
                broomstick.duration = 100f;
        }

        public static void ResetBroomsticks()
        {
            if (!NetworkSystem.Instance.IsMasterClient) return;

            foreach (GorillaLocomotion.Gameplay.NoncontrollableBroomstick broomstick in UnityEngine.Resources.FindObjectsOfTypeAll<GorillaLocomotion.Gameplay.NoncontrollableBroomstick>())
                broomstick.duration = 30f;
        }

        public static void MapForceUnload()
        {
            if (!NetworkSystem.Instance.InRoom || !PhotonNetwork.IsMasterClient) return;

            if (Time.time > mapUnloadDelay)
            {
                mapUnloadDelay = Time.time + 5f;
                var serializerType = HarmonyLib.AccessTools.TypeByName("VirtualStumpSerializer");
                if (serializerType == null) return;

                int sent = 0;
                foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(serializerType, FindObjectsSortMode.None))
                {
                    Component comp = obj as Component;
                    if (comp == null) continue;
                    PhotonView view = comp.GetComponent<PhotonView>();
                    if (view == null) continue;
                    view.RPC("SetRoomMap_RPC", RpcTarget.Others, 999999999L);
                    sent++;
                }

                if (sent > 0)
                {
                    NotificationManager.SendNotification("<color=grey>[</color><color=red>MAP</color><color=grey>]</color> Forced unload sent to every other client.");
                }
                else
                {
                    NotificationManager.SendNotification("<color=grey>[</color><color=red>MAP</color><color=grey>]</color> No stump serializer here.");
                }
            }
        }

        private static float mapUnloadDelay;
    }
}

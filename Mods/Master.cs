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
    }
}

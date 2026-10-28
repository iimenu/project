using Valve.Newtonsoft.Json;
/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using GorillaExtensions;
using GorillaLocomotion;
using iiMenu.Classes.Menu;
using iiMenu.Extensions;
using iiMenu.Managers;
using iiMenu.Menu;
using iiMenu.Utilities;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Windows.Speech;
using UnityEngine.XR;
using static iiMenu.Menu.Main;
using static iiMenu.Mods.Settings;
using static iiMenu.Utilities.AssetUtilities;
using static iiMenu.Utilities.RigUtilities;

using Object = UnityEngine.Object;

namespace iiMenu.Mods
{
    public static class Settings
    {
        public static void Search() // This took me like 4 hours
        {
            isSearching = !isSearching;

            pageNumber = 0;
            keyboardInput = "";

            if (isSearching)
                SpawnKeyboard();
            else
                DestroyKeyboard();
        }

        public static void SpawnKeyboard()
        {
            isKeyboardPc = !XRSettings.isDeviceActive || isOnPC || toggleButtonActive && keyboardWithToggleButton;
            inTextInput = true;
            keyboardInput = "";

            shift = false;
            lockShift = false;

            if (isKeyboardPc)
                lastPressedKeys.Add(KeyCode.Q);

            if (!isKeyboardPc)
            {
                if (VRKeyboard == null)
                {
                    VRKeyboard = LoadObject<GameObject>("VRKeyboard");
                    VRKeyboard.transform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                    VRKeyboard.transform.rotation = GorillaTagger.Instance.bodyCollider.transform.rotation;

                    menuSpawnPosition = VRKeyboard.transform.Find("MenuSpawnPosition").gameObject;
                    VRKeyboard.transform.Find("Canvas").AddComponent<ColorChanger>().colors = textColors[1];

                    VRKeyboard.transform.localScale *= scaleWithPlayer ? GTPlayer.Instance.scale * menuScale : menuScale;
                    menuSpawnPosition.transform.localScale *= scaleWithPlayer ? GTPlayer.Instance.scale * menuScale : menuScale;

                    ColorChanger backgroundColorChanger = VRKeyboard.transform.Find("Background").gameObject.AddComponent<ColorChanger>();
                    backgroundColorChanger.colors = backgroundColor;

                    foreach (GameObject key in VRKeyboard.transform.Find("Seperate").Children()
                        .Select(t => t.gameObject)
                        .Concat(new[] { VRKeyboard.transform.Find("Keys/default").gameObject }))
                    {
                        ColorChanger keyColorChanger = key.AddComponent<ColorChanger>();
                        keyColorChanger.colors = buttonColors[0];
                    }

                    if (shouldOutline)
                        OutlineObject(VRKeyboard.transform.Find("Background").gameObject, true);

                    var keys = new[] { "Numbers", "Letters", "Special", "Seperate" }
                        .Select(name => VRKeyboard.transform.Find(name))
                        .Where(t => t != null)
                        .SelectMany(t => t.Children())
                        .Select(t => t.gameObject);

                    foreach (GameObject v in keys)
                    {
                        v.AddComponent<KeyboardKey>().key = v.name;
                        v.layer = 2;

                        if (shouldOutline)
                            OutlineObject(v, true);
                    }
                }
            }

            if (lKeyReference == null)
            {
                lKeyReference = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lKeyReference.transform.parent = GorillaTagger.Instance.leftHandTransform;
                lKeyReference.GetComponent<Renderer>().material.color = backgroundColor.GetColor(0);
                lKeyReference.transform.localPosition = pointerOffset;
                lKeyReference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                lKeyCollider = lKeyReference.GetComponent<SphereCollider>();

                ColorChanger colorChanger = lKeyReference.AddComponent<ColorChanger>();
                colorChanger.colors = backgroundColor;
            }

            if (rKeyReference == null)
            {
                rKeyReference = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rKeyReference.transform.parent = GorillaTagger.Instance.rightHandTransform;
                rKeyReference.GetComponent<Renderer>().material.color = backgroundColor.GetColor(0);
                rKeyReference.transform.localPosition = pointerOffset;
                rKeyReference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                rKeyCollider = rKeyReference.GetComponent<SphereCollider>();

                ColorChanger colorChanger = rKeyReference.AddComponent<ColorChanger>();
                colorChanger.colors = backgroundColor;
            }
        }

        public static void DestroyKeyboard()
        {
            inTextInput = false;
            isKeyboardPc = false;

            if (lKeyReference != null)
            {
                Object.Destroy(lKeyReference);
                lKeyReference = null;
            }

            if (rKeyReference != null)
            {
                Object.Destroy(rKeyReference);
                rKeyReference = null;
            }

            if (VRKeyboard != null)
            {
                Object.Destroy(VRKeyboard);
                VRKeyboard = null;
            }

            if (TPC != null && TPC.transform.parent.gameObject.name.Contains("CameraTablet") && isOnPC)
            {
                isOnPC = false;
                TPC.transform.position = TPC.transform.parent.position;
                TPC.transform.rotation = TPC.transform.parent.rotation;
            }
        }

        public static void GlobalReturn()
        {
            NotificationManager.ClearAllNotifications();

            ButtonInfo[] current = Buttons.buttons[Buttons.CurrentCategoryIndex];

            if (current.Length > 0)
                Toggle(current[0].buttonText, true);

            if (prompts.Count > 0)
                StopCurrentPrompt();
        }

        public static void StopCurrentPrompt() =>
            prompts.RemoveAt(0);

        public static GameObject TutorialObject;
        public static LineRenderer TutorialSelector;
        public static void ShowTutorial()
        {
            if (TutorialObject != null)
                Object.Destroy(TutorialObject);

            if (GorillaTagger.Instance == null)
                return;

            TutorialObject = LoadObject<GameObject>("Tutorial");
            if (TutorialObject == null)
                return;

            TutorialObject.transform.position = GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.bodyCollider.transform.forward * 1f + Vector3.up * 0.25f;
            TutorialObject.transform.rotation = GorillaTagger.Instance.bodyCollider.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

            string videoName = "q2";
            switch (ControllerUtilities.GetLeftControllerType())
            {
                case ControllerUtilities.ControllerType.Unknown:
                case ControllerUtilities.ControllerType.Quest2:
                    videoName = "q2";
                    break;
                case ControllerUtilities.ControllerType.Quest3:
                    videoName = "q3";
                    break;
                case ControllerUtilities.ControllerType.ValveIndex:
                    videoName = "index";
                    break;
                case ControllerUtilities.ControllerType.VIVE:
                    videoName = "vive";
                    break;
            }

            Transform videoTransform = TutorialObject.transform.Find("Video");
            VideoPlayer videoPlayer = videoTransform?.GetComponent<VideoPlayer>();
            if (videoPlayer != null)
            {
                videoPlayer.url = $"{PluginInfo.ServerResourcePath}/Videos/Tutorial/tutorial-{videoName}.mp4";
                videoPlayer.isLooping = true;
                videoPlayer.Play();

                TutorialButton pauseButton = videoPlayer.gameObject.GetComponent<TutorialButton>()
                    ?? videoPlayer.gameObject.AddComponent<TutorialButton>();
                pauseButton.buttonType = TutorialButton.ButtonType.Pause;
            }

            Transform closeTransform = TutorialObject.transform.Find("Close");
            if (closeTransform != null)
            {
                TutorialButton closeButton = closeTransform.GetComponent<TutorialButton>()
                    ?? closeTransform.gameObject.AddComponent<TutorialButton>();
                closeButton.buttonType = TutorialButton.ButtonType.Close;
            }
        }

        private static bool lastTrigger;
        public static void UpdateTutorial()
        {
            if (TutorialObject == null || GorillaTagger.Instance == null)
                return;

            if (Vector3.Distance(TutorialObject.transform.position, GorillaTagger.Instance.bodyCollider.transform.position) > 2f)
            {
                TutorialObject.transform.position = GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.bodyCollider.transform.forward * 1f + Vector3.up * 0.25f;
                TutorialObject.transform.rotation = GorillaTagger.Instance.bodyCollider.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            }

            if (TutorialSelector == null)
            {
                TutorialSelector = new GameObject("iiMenu_TutorialSelector").AddComponent<LineRenderer>();
                TutorialSelector.material.shader = Shader.Find("Sprites/Default");

                TutorialSelector.startWidth = 0.01f;
                TutorialSelector.endWidth = 0.01f;

                TutorialSelector.positionCount = 2;

                TutorialSelector.useWorldSpace = true;
            }

            TutorialSelector.startColor = BrightenColor(new Color32(255, 128, 0, 128));
            TutorialSelector.endColor = BrightenColor(new Color32(255, 102, 0, 128));

            Vector3 Direction = ControllerUtilities.GetTrueRightHand().forward;
            Physics.Raycast(GorillaTagger.Instance.rightHandTransform.position + Direction / 4f, Direction, out var Ray, 512f, NoInvisLayerMask());
            if (!XRSettings.isDeviceActive)
            {
                Mouse desktopMouse = Mouse.current;
                Camera camera = TPC ?? iiMenu.Menu.Main.MainCam;
                if (desktopMouse == null || camera == null)
                    return;

                Ray ray = camera.ScreenPointToRay(desktopMouse.position.ReadValue());
                Physics.Raycast(ray, out Ray, 512f, NoInvisLayerMask());
            }

            TutorialSelector.SetPosition(0, GorillaTagger.Instance.rightHandTransform.position);
            TutorialSelector.SetPosition(1, Ray.point == Vector3.zero ? GorillaTagger.Instance.rightHandTransform.position : Ray.point);

            Mouse mouse = Mouse.current;
            bool mouseClick = mouse?.leftButton.isPressed ?? false;
            bool trigger = rightTrigger > 0.5f || mouseClick;
            if (trigger && !lastTrigger && Ray.collider != null)
            {
                TutorialButton gunTarget = Ray.collider.GetComponentInParent<TutorialButton>();
                gunTarget?.ClickButton();
            }

            lastTrigger = trigger;
        }

        public class TutorialButton : MonoBehaviour
        {
            public enum ButtonType
            {
                Pause,
                Close
            }

            public ButtonType buttonType;
            public void ClickButton()
            {
                switch (buttonType)
                {
                    case ButtonType.Pause:
                        VideoPlayer videoPlayer = TutorialObject?.transform.Find("Video")?.GetComponent<VideoPlayer>();
                        if (videoPlayer == null)
                            return;

                        if (videoPlayer.isPlaying)
                            videoPlayer.Pause();
                        else
                            videoPlayer.Play();

                        break;
                    case ButtonType.Close:
                        if (TutorialObject != null)
                            Destroy(TutorialObject);
                        if (TutorialSelector != null)
                            Destroy(TutorialSelector.gameObject);
                        TutorialObject = null;
                        TutorialSelector = null;
                        break;
                }
            }
        }

        public static void ShowDebug()
        {
            int category = Buttons.GetCategory("Temporary Category");

            string version = PluginInfo.Version;
            if (PluginInfo.BetaBuild) version = "<color=blue>Beta</color> " + version;
            Buttons.AddButton(category, new ButtonInfo { buttonText = "Exit Info Screen", method = () => Toggle("Info Screen"), isTogglable = false, toolTip = "Returns you back to the main page." });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugMenuName", overlapText = "<color=grey><b>ii Reborn </b></color>" + version, label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugColor", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugName", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugId", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugClip", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugFps", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugRoomA", overlapText = "Loading...", label = true });
            Buttons.AddButton(category, new ButtonInfo { buttonText = "DebugRoomB", overlapText = "Loading...", label = true });

            Debug();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static bool hideId;
        public static void Debug()
        {
            string red = "<color=red>" + MathF.Floor(PlayerPrefs.GetFloat("redValue") * 255f) + "</color>";
            string green = ", <color=green>" + MathF.Floor(PlayerPrefs.GetFloat("greenValue") * 255f) + "</color>";
            string blue = ", <color=blue>" + MathF.Floor(PlayerPrefs.GetFloat("blueValue") * 255f) + "</color>";
            Buttons.GetIndex("DebugColor").overlapText = "Color: " + red + green + blue;

            string master = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient ? "<color=red> [Master]</color>" : "";
            Buttons.GetIndex("DebugName").overlapText = PhotonNetwork.LocalPlayer.NickName + master;

            Buttons.GetIndex("DebugId").overlapText = "<color=green>ID: </color>" + (hideId ? "Hidden" : PhotonNetwork.LocalPlayer.UserId);
            Buttons.GetIndex("DebugClip").overlapText = "<color=green>Clip: </color>" + (GUIUtility.systemCopyBuffer.Length > 25 ? GUIUtility.systemCopyBuffer[..25] : GUIUtility.systemCopyBuffer);
            Buttons.GetIndex("DebugFps").overlapText = "<b>" + lastDeltaTime + "</b> FPS <b>" + PhotonNetwork.GetPing() + "</b> Ping";
            Buttons.GetIndex("DebugRoomA").overlapText = "<color=blue>" + NetworkSystem.Instance.regionNames[NetworkSystem.Instance.currentRegionIndex].ToUpper() + "</color> " + PhotonNetwork.PlayerList.Length + " Players";

            string priv = PhotonNetwork.InRoom ? NetworkSystem.Instance.SessionIsPrivate ? "Private" : "Public" : "";
            Buttons.GetIndex("DebugRoomB").overlapText = "<color=blue>" + priv + "</color> " + (PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "Not in room");
        }
        public static void HideDebug()
        {
            int category = Buttons.GetCategory("Temporary Category");

            Buttons.RemoveButton(category, "DebugMenuName");
            Buttons.RemoveButton(category, "DebugColor");
            Buttons.RemoveButton(category, "DebugName");
            Buttons.RemoveButton(category, "DebugId");
            Buttons.RemoveButton(category, "DebugClip");
            Buttons.RemoveButton(category, "DebugFps");
            Buttons.RemoveButton(category, "DebugRoomA");
            Buttons.RemoveButton(category, "DebugRoomB");
            Buttons.CurrentCategoryName = "Main";
        }

        public static void PlayersTab()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo {
                    buttonText = "Exit Players",
                    method =() => Buttons.CurrentCategoryName = "Main",
                    isTogglable = false,
                    toolTip = "Returns you back to the main page."
                }
            };

            if (!PhotonNetwork.InRoom)
                buttons.Add(new ButtonInfo { buttonText = "Not in a Room", label = true });
            else
            {
                for (int i = 0; i < NetworkSystem.Instance.PlayerListOthers.Length; i++)
                {
                    NetPlayer player = NetworkSystem.Instance.PlayerListOthers[i];
                    string playerColor = "#ffffff";
                    try
                    {
                        playerColor = $"#{ColorToHex(GetVRRigFromPlayer(player).playerColor)}";
                    }
                    catch { }

                    buttons.Add(new ButtonInfo
                    {
                        buttonText = $"PlayerButton{i}",
                        overlapText = $"<color={playerColor}>" + player.NickName + "</color>",
                        method = () => NavigatePlayer(player),
                        isTogglable = false,
                        toolTip = $"See information on the player {player.NickName}."
                    });
                }
            }

            Buttons.buttons[Buttons.GetCategory("Players")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Players";
        }

        public static void NavigatePlayer(NetPlayer player)
        {
            string targetName = player.NickName;

            VRRig playerRig = GetVRRigFromPlayer(player) ?? null;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo {
                    buttonText = "Exit PlayerInspect",
                    overlapText = $"Exit {targetName}",
                    method =() => PlayersTab(),
                    isTogglable = false,
                    toolTip = "Returns you back to the players tab."
                },

                new ButtonInfo {
                    buttonText = "Spectate Player",
                    overlapText = $"Spectate {targetName}",
                    method =() => SpectatePlayer(playerRig),
                    isTogglable = false,
                    toolTip = $"Shows you what {targetName} sees."
                },
                new ButtonInfo {
                    buttonText = "Teleport to Player",
                    overlapText = $"Teleport to {targetName}",
                    method =() => Movement.TeleportToPlayer(player),
                    isTogglable = false,
                    toolTip = $"Teleports you to {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Give Player Guns",
                    overlapText = $"Give {targetName} Guns",
                    method =() => GiveGunTarget = playerRig,
                    disableMethod =() => GiveGunTarget = null,
                    toolTip = $"Gives {targetName} every gun on the menu."
                },
                new ButtonInfo {
                    buttonText = "Copy Movement",
                    overlapText = $"Copy Movement {targetName}",
                    method =() => Movement.CopyMovementPlayer(player),
                    disableMethod = Movement.EnableRig,
                    toolTip = $"Copies the movement of {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Follow Player",
                    overlapText = $"Follow {targetName}",
                    method =() => Movement.FollowPlayer(player),
                    disableMethod = Movement.EnableRig,
                    toolTip = $"Follows {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Tag Player",
                    overlapText = $"Tag {targetName}",
                    method =() => Advantages.TagPlayer(player),
                    disableMethod = Movement.EnableRig,
                    toolTip = $"Tags {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Snowball Fling Player",
                    overlapText = $"Snowball Fling {targetName}",
                    method =() => Overpowered.FlingPlayer(player),
                    toolTip = $"Flings {targetName} with snowballs."
                },
                new ButtonInfo {
                    buttonText = "Projectile Blind Player",
                    overlapText = $"Projectile Blind {targetName}",
                    method =() => Projectiles.ProjectileBlindPlayer(player),
                    toolTip = $"Blinds {targetName} using the egg projectiles."
                },
                new ButtonInfo {
                    buttonText = "Projectile Lag Player",
                    overlapText = $"Projectile Lag {targetName}",
                    method =() => Projectiles.ProjectileLagPlayer(player),
                    toolTip = $"Lags {targetName} using the firework projectiles."
                },
                new ButtonInfo {
                    buttonText = "Lag Player",
                    overlapText = $"Lag {targetName}",
                    method =() => Overpowered.LagTarget(player),
                    toolTip = $"Lags {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Destroy Player",
                    overlapText = $"Destroy {targetName}",
                    method =() => Overpowered.DestroyPlayer(player),
                    toolTip = $"Stops all new players from seeing {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Guardian Bring Player",
                    overlapText = $"Guardian Bring {targetName}",
                    method =() => Overpowered.BringPlayer(player),
                    toolTip = $"Brings {targetName} to you."
                },
                new ButtonInfo {
                    buttonText = "Guardian Bring Player Gun",
                    overlapText = $"Guardian Bring {targetName} Gun",
                    method =() => Overpowered.BringPlayerGun(player),
                    toolTip = $"Brings {targetName} to wherever your hand desires."
                },
                new ButtonInfo {
                    buttonText = "Guardian Kick Player",
                    overlapText = $"Guardian Kick {targetName}",
                    method =() => Overpowered.GuardianKickTarget(player),
                    toolTip = $"Kicks {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Guardian Obliterate Player",
                    overlapText = $"Guardian Obliterate {targetName}",
                    method =() => Overpowered.ObliteratePlayer(player),
                    toolTip = $"Obliterates {targetName}."
                },
                new ButtonInfo {
                    buttonText = "Guardian Crash Player",
                    overlapText = $"Guardian Crash {targetName}",
                    method =() => Overpowered.CrashPlayer(player),
                    toolTip = $"Crashes {targetName}."
                },
                new ButtonInfo {
                    buttonText =  "Crash Player",
                    overlapText = $"Crash {targetName}",
                    method =() => Overpowered.CrashPerson(GetVRRigFromPlayer(player)),
                    toolTip = $"Crashes {targetName}."
                }
            };

            if (PhotonNetwork.IsMasterClient)
            {
                buttons.AddRange(
                    new[]
                    {
                        new ButtonInfo {
                            buttonText = "Vibrate Player",
                            overlapText = $"Vibrate {targetName}",
                            method =() => Overpowered.BetaSetStatus(RoomSystem.StatusEffects.JoinedTaggedTime, new RaiseEventOptions { TargetActors = new[] { player.ActorNumber } }),
                            toolTip = $"Vibrates {targetName}'s controllers."
                        },
                        new ButtonInfo {
                            buttonText = "Slow Player",
                            overlapText = $"Slow {targetName}",
                            method =() => Overpowered.BetaSetStatus(RoomSystem.StatusEffects.TaggedTime, new RaiseEventOptions { TargetActors = new[] { player.ActorNumber } } ),
                            toolTip = $"Gives {targetName} tag freeze."
                        }
                    }
                );
            }

            Color playerColor = playerRig?.playerColor ?? Color.black;
            if (playerRig)
                buttons.AddRange(
                    new[]
                    {
                        new ButtonInfo
                        {
                            buttonText = "Player Name",
                            overlapText = $"Name: {player.NickName}",
                            method = () => ChangeName(player.NickName),
                            isTogglable = false,
                            toolTip = $"Sets your name to \"{player.NickName}\"."
                        },
                        new ButtonInfo
                        {
                            buttonText = "Player Color",
                            overlapText =
                                $"Color: {playerColor.ToRichRGBString()}",
                            method = () => ChangeColor(playerColor),
                            isTogglable = false,
                            toolTip = $"Sets your color to the same as {targetName}."
                        },
                        new ButtonInfo
                        {
                            buttonText = "Player User ID",
                            overlapText = $"User ID: {player.UserId}",
                            method = () =>
                            {
                                NotificationManager.SendNotification(
                                    $"<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Successfully copied {player.UserId} to the clipboard!",
                                    5000);
                                GUIUtility.systemCopyBuffer = player.UserId;
                            },
                            isTogglable = false,
                            toolTip = $"Copies {player.UserId} to your clipboard."
                        },
                        new ButtonInfo
                        {
                            buttonText = "Player Creation Date",
                            overlapText =
                                $"Creation Date: {GetCreationDate(player.UserId, creationDate => { Buttons.GetIndex("Player Creation Date").overlapText = $"Creation Date: {creationDate}"; ReloadMenu(); })}",
                            label = true
                        },
                        new ButtonInfo
                        {
                            buttonText = "Player Platform",
                            overlapText =
                                $"Platform: {((playerRig?.IsSteam() ?? false) ? "Steam" : "Quest")}",
                            label = true
                        },
                        new ButtonInfo
                        {
                            buttonText = "Player FPS",
                            overlapText = $"FPS: {playerRig.fps}",
                            label = true
                        }
                    }
                );

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void SpectatePlayer(VRRig rig)
        {
            GameObject cameraObject = new GameObject("iiMenu_SpectateCamera");
            RenderTexture renderTexture = new RenderTexture(512, 512, 16);
            cameraObject.AddComponent<Camera>().targetTexture = renderTexture;
            cameraObject.transform.SetParent(rig.headMesh.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.25f, 0.25f);
            promptMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                mainTexture = renderTexture
            };
            PromptSingle("<https://.mat>", () => Object.Destroy(cameraObject), "Done");
        }

        public static void CategorySettings()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> { new ButtonInfo { buttonText = "Exit Menu Settings", method = () => { Buttons.CurrentCategoryName = "Settings"; Buttons.buttons[Buttons.GetCategory("Temporary Category")] = Array.Empty<ButtonInfo>(); }, isTogglable = false, toolTip = "Returns you back to the settings menu." } };

            foreach (var button in Buttons.buttons[Buttons.GetCategory("Main")])
            {
                buttons.Add(new ButtonInfo
                {
                    buttonText = $"Category{button.buttonText.Hash()}",
                    overlapText = button.buttonText,
                    enabled = !skipButtons.Contains(button.buttonText),
                    hideFromArraylist = true,
                    enableMethod = () => skipButtons.Remove(button.buttonText),
                    disableMethod = () => skipButtons.Add(button.buttonText),
                    toolTip = "Toggles the visibility of the category " + button.buttonText + "."
                });
            }

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void RightHand()
        {
            rightHand = true;
            if (watchMenu)
            {
                Toggle("Watch Menu");
                Toggle("Watch Menu");
                NotificationManager.ClearAllNotifications();
            }

            if (!Buttons.GetIndex("Info Watch").enabled) return;
            Toggle("Info Watch");
            Toggle("Info Watch");
            NotificationManager.ClearAllNotifications();
        }

        public static void LeftHand()
        {
            rightHand = false;
            if (watchMenu)
            {
                Toggle("Watch Menu");
                Toggle("Watch Menu");
                NotificationManager.ClearAllNotifications();
            }

            if (!Buttons.GetIndex("Info Watch").enabled) return;
            Toggle("Info Watch");
            Toggle("Info Watch");
            NotificationManager.ClearAllNotifications();
        }

        private static readonly (string name, string bind)[] KeybindOptions = new[]
        {
            ("Keybind A", "A"),
            ("Keybind B", "B"),
            ("Keybind X", "X"),
            ("Keybind Y", "Y"),
            ("Keybind Left Grip", "LG"),
            ("Keybind Right Grip", "RG"),
            ("Keybind Left Trigger", "LT"),
            ("Keybind Right Trigger", "RT"),
            ("Keybind Left Joystick", "LJ"),
            ("Keybind Right Joystick", "RJ")
        };

        public static void UpdateKeybindButtons()
        {
            string current = string.IsNullOrEmpty(BindInput) ? "A" : BindInput;
            foreach ((string name, string bind) in KeybindOptions)
            {
                ButtonInfo btn = Buttons.GetIndex(name);
                if (btn != null)
                    btn.overlapText = bind == current ? $"{name} <color=grey>[</color><color=green>ACTIVE</color><color=grey>]</color>" : null;
            }
        }

        public static void SelectBind(string bind)
        {
            BindInput = bind;
            IsBinding = true;
            UpdateKeybindButtons();
            VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
            NotificationManager.SendNotification($"<color=grey>[</color><color=purple>BINDS</color><color=grey>]</color> Selected <color=green>{bind}</color>. Hold thumbstick (VR) or B (PC) on any mod to bind.");
        }

        public static void ClearAllKeybinds()
        {
            foreach (ButtonInfo[] list in Buttons.buttons)
            {
                foreach (ButtonInfo btn in list)
                    btn.customBind = null;
            }
            foreach (List<string> bindList in ModBindings.Values)
                bindList.Clear();
            VRRig.LocalRig.PlayHandTapLocal(48, rightHand, 0.4f);
            NotificationManager.SendNotification("<color=grey>[</color><color=purple>BINDS</color><color=grey>]</color> Cleared all keybinds.");
        }

        public static void StartBind(string bind) => SelectBind(bind);

        public static void StartRebind(string bind)
        {
            if (IsBinding)
                return;
            IsRebinding = true;
            BindInput = bind;
        }

        public static void RemoveRebinds()
        {
            foreach (ButtonInfo[] buttonlist in Buttons.buttons)
            {
                foreach (ButtonInfo v in buttonlist)
                    v.rebindKey = null;
            }
            NotificationManager.SendNotification("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Removed all rebinds.");
        }
        public static void UpdateMenu()
        {
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.Windows:
                    {
                        string logoLines = "";
                        foreach (string line in PluginInfo.Logo.Replace("\r\n", "\n").Split(@"
"))
                            logoLines += Environment.NewLine + @"echo ""    " + line + @"""";
                        string downloadUrl = TelemetryClient.UpdateDownloadUrl;
                        string expectedHash = TelemetryClient.UpdateSha256 ?? "";
                        if (!System.Text.RegularExpressions.Regex.IsMatch(expectedHash, "^[0-9a-fA-F]{64}$"))
                            expectedHash = "";

                        if (string.IsNullOrEmpty(downloadUrl) || expectedHash.Length == 0)
                        {
                            LogManager.LogError("no verified update available, refusing to update");
                            NotificationManager.SendNotification("No verified update available right now.");
                            return;
                        }

                        string updateScript = @"@echo off
setlocal
title ii Reborn
color 0E
chcp 65001 >nul

set ""LOG=%~dp0UpdateLog.txt""

if ""%~1""==""--logged"" goto logged

cmd /c """"%~f0"" --logged > ""%LOG%"" 2>&1""
cls
type ""%LOG%""
echo.
goto restart

:logged
cd /d ""%~dp0..""
set ""PLUGIN_PATH=BepInEx\plugins""
set ""URL=" + downloadUrl + @"""
set ""EXPECTED=" + expectedHash + @"""

cls
echo." + logoLines + @"
echo.

echo Your menu is updating, please wait...
echo.

where curl >nul 2>nul
if errorlevel 1 (
    echo curl was not found on this PC, cannot update.
    exit /b 1
)

echo Downloading ii Reborn...
curl -L -f -sS -o ""%TEMP%\ii.Reborn.new.dll"" ""%URL%""
if errorlevel 1 (
    echo Download failed, your current menu was not changed.
    del /f /q ""%TEMP%\ii.Reborn.new.dll"" >nul 2>nul
    exit /b 1
)

set ""GOT=""
for /f ""delims="" %%h in ('certutil -hashfile ""%TEMP%\ii.Reborn.new.dll"" SHA256 ^| findstr /r ""^[0-9a-fA-F]*$""') do if not defined GOT set ""GOT=%%h""
if not defined GOT (
    echo Could not verify the download, your current menu was not changed.
    del /f /q ""%TEMP%\ii.Reborn.new.dll"" >nul 2>nul
    exit /b 1
)
if /i not ""%GOT%""==""%EXPECTED%"" (
    echo Download does not match the published hash, your current menu was not changed.
    echo got:      %GOT%
    echo expected: %EXPECTED%
    del /f /q ""%TEMP%\ii.Reborn.new.dll"" >nul 2>nul
    exit /b 1
)
echo Verified.

echo Waiting for Gorilla Tag to close...
:WAIT_INSTALL
tasklist /FI ""IMAGENAME eq Gorilla Tag.exe"" | find /I ""Gorilla Tag.exe"" >nul
if %ERRORLEVEL%==0 (
    timeout /t 1 >nul
    goto WAIT_INSTALL
)

mkdir ""%PLUGIN_PATH%"" >nul 2>nul
move /y ""%TEMP%\ii.Reborn.new.dll"" ""%PLUGIN_PATH%\menu-update.tmp"" >nul
if errorlevel 1 (
    echo Install failed, your current menu was not changed.
    del /f /q ""%TEMP%\ii.Reborn.new.dll"" >nul 2>nul
    exit /b 1
)

echo Cleaning up old menu files...
for /r ""%PLUGIN_PATH%"" %%F in (ii*.dll) do (
    del /f /q ""%%F""
)
for /d %%D in (""%PLUGIN_PATH%\ii*"") do (
    rmdir /s /q ""%%D""
)

ren ""%PLUGIN_PATH%\menu-update.tmp"" ""ii.Reborn.dll""
if errorlevel 1 (
    echo Rename failed, please reinstall the menu manually.
    exit /b 1
)

exit /b 0

:restart
echo Waiting for Gorilla Tag to close...
:WAIT_RESTART
tasklist /FI ""IMAGENAME eq Gorilla Tag.exe"" | find /I ""Gorilla Tag.exe"" >nul
if %ERRORLEVEL%==0 (
    timeout /t 1 >nul
    goto WAIT_RESTART
)

:launch
echo Launching Gorilla Tag...
start steam://run/1533390
exit";

                        string fileName = $"{PluginInfo.BaseDirectory}/UpdateScript.bat";

                        // cmd.exe goto/label parsing is unreliable with LF-only batch files — force CRLF
                        File.WriteAllText(fileName, updateScript.Replace("\r\n", "\n").Replace("\n", "\r\n"));

                        string filePath = FileUtilities.GetGamePath() + "/" + fileName;
                        Process.Start(filePath);
                        Application.Quit();
                        break;
                    }
                    // unsupported slop
                    /*
                    case OperatingSystemFamily.Linux:
                    {
                        string logoLines = "";
                        foreach (string line in PluginInfo.Logo.Split(@"
    "))
                            logoLines += Environment.NewLine + @"echo ""    " + line + @"""";
                        string downloadUrl = string.IsNullOrEmpty(TelemetryClient.UpdateDownloadUrl)
                            ? "https://github.com/iimenu/project/releases/latest/download/ii.Reborn.dll"
                            : TelemetryClient.UpdateDownloadUrl;

                        string updateScript = @"#!/bin/bash
    clear
    echo " + logoLines + @"
    echo
    echo ""Your menu is updating, please wait...""
    echo

    PLUGIN_PATH=""BepInEx/plugins""
    MENU_FILE=""""

    if ls ""$PLUGIN_PATH""/*iiMenu_AutoUpdater*.dll 1> /dev/null 2>&1; then
        echo ""Auto-updater found. Restarting game...""
    else
        for f in ""$PLUGIN_PATH""/ii*.dll; do
            if [ -f ""$f"" ]; then
                MENU_FILE=""$f""
                break
            fi
        done

        if [ -z ""$MENU_FILE"" ]; then
            echo ""No menu file found, skipping update.""
        else
            echo ""Downloading latest release of ii Reborn...""
            curl -L -o ""$MENU_FILE"" \
            """ + downloadUrl + @"""
        fi
    fi

    while pgrep -f ""GorillaTag.exe"" > /dev/null; do
        sleep 1
    done

    echo ""Launching Gorilla Tag...""
    xdg-open ""steam://run/1533390""
    exit 0";

                        string fileName = $"{PluginInfo.BaseDirectory}/UpdateScript.sh";
                        File.WriteAllText(fileName, updateScript);
                        Process.Start("chmod", $"+x \"{fileName}\"");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"\"{fileName}\"",
                            UseShellExecute = false
                        });
                        Application.Quit();
                        break;
                    }
                    */
            }
        }

        public static void JoystickMenuOff()
        {
            joystickMenu = false;
            joystickOpen = false;
        }

        public static void PhysicalMenuOn()
        {
            physicalMenu = true;
            physicalOpenPosition = Vector3.zero;
        }

        public static void PhysicalMenuOff()
        {
            physicalMenu = false;
            physicalOpenPosition = Vector3.zero;
        }

        public static void WatchMenuOn()
        {
            watchMenu = true;
            GameObject mainwatch = VRRig.LocalRig.transform.Find("rig/hand.L/huntcomputer (1)").gameObject;
            watchobject = Object.Instantiate(mainwatch,
                rightHand ?
                VRRig.LocalRig.transform.Find("rig/hand.R").transform :
                VRRig.LocalRig.transform.Find("rig/hand.L").transform, false);

            Object.Destroy(watchobject.GetComponent<GorillaHuntComputer>());
            watchobject.SetActive(true);

            Transform watchCanvas = watchobject.transform.Find("HuntWatch_ScreenLocal/Canvas/Anchor");
            watchCanvas.Find("Hat").gameObject.SetActive(false);
            watchCanvas.Find("Face").gameObject.SetActive(false);
            watchCanvas.Find("Badge").gameObject.SetActive(false);
            watchCanvas.Find("Material").gameObject.SetActive(false);
            watchCanvas.Find("Right Hand").gameObject.SetActive(false);

            watchText = watchCanvas.Find("Text").gameObject;
            watchEnabledIndicator = watchCanvas.Find("Left Hand").gameObject;
            watchShell = watchobject.transform.Find("HuntWatch_ScreenLocal").gameObject;

            watchShell.GetComponent<Renderer>().material = CustomBoardManager.BoardMaterial;

            if (rightHand)
            {
                watchShell.transform.localRotation = Quaternion.Euler(0f, 140f, 0f);
                watchShell.transform.parent.localPosition += new Vector3(0.025f, 0f, 0f);
                watchShell.transform.localPosition += new Vector3(0.025f, 0f, -0.035f);
            }
        }
        public static void CheckWatchMenu()
        {
            if (watchTimer == 0)
                watchTimer = Time.time + 10f;

            if (leftJoystick.sqrMagnitude > 0.1f * 0.1f)
            {
                watchTimer = 0;
                watchUsed = true;
                return;
            }

            if (!watchUsed && Time.time >= watchTimer)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=purple>WATCH</color><color=grey>]</color> Seems that you got stuck using Watch Menu, automatically disabling..");
                Toggle("Watch Menu");
            }
        }
        public static void WatchMenuOff()
        {
            watchMenu = false;
            watchUsed = false;
            watchTimer = 0;
            Object.Destroy(watchobject);
        }

        public static int langInd;
        public static void ChangeMenuLanguage(bool positive = true)
        {
            string[] languageNames = {
                "English",
                "Español",
                "Français",
                "Deutsch",
                "日本語",
                "Italiano",
                "Português",
                "Nederlands",
                "Русский",
                "Polski"
            };

            string[] codenames = {
                "en",
                "es",
                "fr",
                "de",
                "ja",
                "it",
                "pt",
                "nl",
                "ru",
                "pl"
            };

            if (positive)
                langInd++;
            else
                langInd--;

            langInd %= languageNames.Length;
            if (langInd < 0)
                langInd = languageNames.Length - 1;

            TranslationManager.language = codenames[langInd];
            TranslationManager.ResetLanguageState();

            Buttons.GetIndex("Change Menu Language").overlapText = "Change Menu Language <color=grey>[</color><color=green>" + languageNames[langInd] + "</color><color=grey>]</color>";

            translate = langInd != 0;
        }

        public static void ChangeMenuButton(bool positive = true)
        {
            string[] buttonNames = {
                "Primary",
                "Secondary",
                "Grip",
                "Trigger",
                "Joystick"
            };

            if (positive)
                menuButtonIndex++;
            else
                menuButtonIndex--;

            menuButtonIndex %= buttonNames.Length;
            if (menuButtonIndex < 0)
                menuButtonIndex = buttonNames.Length - 1;

            Buttons.GetIndex("Change Menu Button").overlapText = "Change Menu Button <color=grey>[</color><color=green>" + buttonNames[menuButtonIndex] + "</color><color=grey>]</color>";
        }
        public static void SetTheme(int id)
        {
            themeType = id - 1;
            ChangeMenuTheme(true);
        }

        public static void ChangeMenuTheme(bool increment = true)
        {
            if (increment)
                themeType++;
            else
                themeType--;

            const int themeCount = 69;

            if (themeType > themeCount)
                themeType = 1;

            if (themeType < 1)
                themeType = themeCount;

            if (Buttons.GetIndex("Custom Menu Theme").enabled)
                return;

            if (themeType == 69)
            {
                customWatermark = LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.poopooVR.png");
                if (watermarkImage != null)
                {
                    watermarkImage.material.SetTexture("_MainTex", customWatermark);
                    watermarkImage.material.color = Color.white;
                }
            }
            else
            {
                if (Buttons.GetIndex("Custom Watermark").enabled)
                    CustomWatermark();
                else
                {
                    customWatermark = null;
                    if (watermarkImage != null)
                    {
                        watermarkImage.material.SetTexture("_MainTex", LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.icon.png"));
                        watermarkImage.material.color = Color.white;
                    }
                }
            }

            switch (themeType)
            {
                case 1: // Orange
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(255, 128, 0, 128), new Color32(255, 102, 0, 128))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(170, 85, 0, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(85, 42, 0, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 190, 125, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 190, 125, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 190, 125, 255))
                        }
                    };
                    break;
                case 2: // Blue Magenta
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.blue, Color.magenta)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.blue)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 3: // Dark Mode
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(50, 50, 50, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(20, 20, 20, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 4: // Strobe
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.white, Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSimpleGradient(Color.black, Color.white)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 5: // Kman
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(110, 0, 0, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(110, 0, 0, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(110, 0, 0, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 6: // Rainbow
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black),
                        rainbow = true
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black),
                            rainbow = true
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 7: // Cone
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(255, 128, 0, 128))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(170, 85, 0, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(85, 42, 0, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 190, 125, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 190, 125, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 190, 125, 255))
                        }
                    };
                    Buttons.GetIndex("Thin Menu").enabled = true;
                    thinMenu = true;
                    break;
                case 8: // Player Material
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black),
                        copyRigColor = true
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black),
                            copyRigColor = true
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 9: // Lava
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(255, 111, 0, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(255, 111, 0, 255), Color.black)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 10: // Rock
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, Color.red)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(Color.red, Color.black)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 11: // Ice
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(0, 174, 255, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(0, 174, 255, 255), Color.black)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 12: // Water
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(0, 136, 255, 255), new Color32(0, 174, 255, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(0, 100, 188, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(0, 174, 255, 255), new Color32(0, 136, 255, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 13: // Minty
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(0, 255, 246, 255), new Color32(0, 255, 144, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(0, 255, 144, 255), new Color32(0, 255, 246, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 14: // Pink
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(255, 130, 255, 255), Color.white)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 130, 255, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 15: // Purple
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(122, 35, 159, 255), new Color32(60, 26, 89, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(60, 26, 89, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(122, 35, 159, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 16: // Magenta Cyan
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.magenta, Color.cyan)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(Color.magenta, Color.cyan)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 17: // Red Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.red, Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 18: // Orange Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(255, 128, 0, 255), Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 128, 0, 255))
                        }
                    };
                    textColors = new[]
                    {
                new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 128, 0, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 128, 0, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 19: // Yellow Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.yellow, Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.yellow)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.yellow)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.yellow)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 20: // Green Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.green, Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 21: // Blue Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.blue, Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.blue)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.blue)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.blue)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 22: // Purple Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(119, 0, 255, 255), Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(119, 0, 255, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(119, 0, 255, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(119, 0, 255, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 23: // Magenta Fade
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.magenta, Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.magenta)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.magenta)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.magenta)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 24: // Banana
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(255, 255, 130, 255), Color.white)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 255, 130, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 25: // Pride
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.red, Color.green)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 26: // Trans
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(245, 169, 184, 255), new Color32(91, 206, 250, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(245, 169, 184, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(91, 206, 250, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(91, 206, 250, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(91, 206, 250, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(245, 169, 184, 255))
                        }
                    };
                    break;
                case 27: // MLM or Gay
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(7, 141, 112, 255), new Color32(61, 26, 220, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(7, 141, 112, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(61, 26, 220, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(61, 26, 220, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(61, 26, 220, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(7, 141, 112, 255))
                        }
                    };
                    break;
                case 28: // Steal (old)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(50, 50, 50, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(50, 50, 50, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(75, 75, 75, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 29: // Silence
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(80, 0, 80, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        }
                    };
                    break;
                case 30: // Transparent
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black),
                        transparent = true
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white),
                            transparent = true
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green),
                            transparent = true
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        }
                    };
                    break;
                case 31: // King
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(100, 60, 170, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(150, 100, 240, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(150, 100, 240, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.cyan)
                        }
                    };
                    break;
                case 32: // Scoreboard
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(0, 59, 4, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(192, 190, 171, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 33: // Scoreboard (banned)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(225, 73, 43, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(192, 190, 171, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 34: // Rift
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(25, 25, 25, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(40, 40, 40, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(167, 66, 191, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(144, 144, 144, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(144, 144, 144, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 35: // Blurple Dark
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(26, 26, 61, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(26, 26, 61, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(43, 17, 84, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 36: // ShibaGT Gold
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, Color.gray)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.yellow)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.magenta)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 37: // ShibaGT Genesis
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 38: // wyvern
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(199, 115, 173, 255), new Color32(165, 233, 185, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(99, 58, 86, 255), new Color32(83, 116, 92, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(99, 58, 86, 255), new Color32(83, 116, 92, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        }
                    };
                    break;
                case 39: // Steal (new)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(27, 27, 27, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(50, 50, 50, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(66, 66, 66, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 40: // USA Menu (lol)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(100, 25, 125, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(25, 25, 25, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 41: // Watch
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(27, 27, 27, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.green)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 42: // AZ Menu
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, new Color32(100, 0, 0, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(100, 0, 0, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 43: // ImGUI
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(21, 22, 23, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(32, 50, 77, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(60, 127, 206, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 44: // Clean Dark
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(10, 10, 10, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 45: // Discord Light Mode (lmfao)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.white)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(245, 245, 245, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 46: // The Hub
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 163, 26, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 47: // EPILEPTIC
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.black),
                        epileptic = true
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black),
                            epileptic = true
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 48: // Discord Blurple
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(111, 143, 255, 255), new Color32(163, 184, 255, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(96, 125, 219, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(147, 167, 226, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(33, 33, 101, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(33, 33, 101, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(33, 33, 101, 255))
                        }
                    };
                    break;
                case 49: // VS Zero
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(19, 22, 27, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(19, 22, 27, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(16, 18, 22, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(82, 96, 122, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(82, 96, 122, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(82, 96, 122, 255))
                        }
                    };
                    break;
                case 50: // Weed theme (for v4.2.0) (also 50th theme)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(0, 136, 16, 255), new Color32(0, 127, 14, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(0, 158, 15, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(0, 112, 11, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 51: // Pastel Rainbow
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.white),
                        pastelRainbow = true
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white),
                            pastelRainbow = true
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    break;
                case 52: // Rift Light
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(25, 25, 25, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(40, 40, 40, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(165, 137, 255, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(144, 144, 144, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(144, 144, 144, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 53: // Rose (Solace)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(176, 12, 64, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(140, 10, 51, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(250, 2, 81, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 54: // Tenacity (Solace)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(124, 25, 194, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(88, 9, 145, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(136, 9, 227, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 55: // e621 (for version 6.2.1)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(1, 73, 149, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(1, 46, 87, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(0, 37, 74, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(252, 179, 40, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 56: // Catppuccin Mocha
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(30, 30, 46, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(88, 91, 112, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(49, 50, 68, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(205, 214, 244, 255))
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(186, 194, 222, 255))
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(166, 173, 200, 255))
                        }
                    };
                    break;
                case 57: // Rexon
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(45, 25, 75, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(40, 15, 60, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(100, 30, 140, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 58: // Tenacity (Minecraft)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(45, 46, 51, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(231, 133, 209, 255), new Color32(56, 155, 193, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 59: // Mint Blue (Opal v2)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(45, 46, 51, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(40, 94, 93, 255), new Color32(66, 158, 157, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
            };
                    break;
                case 60: // Pink Blood (Opal v2)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(45, 46, 51, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(255, 166, 201, 255), new Color32(228, 0, 70, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 61: // Purple Fire (Opal v2)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(45, 46, 51, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(177, 162, 202, 255), new Color32(104, 71, 141, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 62: // Deep Ocean (Opal v2)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(new Color32(32, 32, 32, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(45, 46, 51, 255))
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSimpleGradient(new Color32(60, 82, 145, 255), new Color32(0, 20, 64, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 63: // Bad Apple (thanks random person in vc for idea)
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(Color.black, Color.white)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            transparent = true
                        },
                        new ExtGradient // Pressed
                        {
                            transparent = true
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 64: // coolkidd
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSolidGradient(Color.red)
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.red)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 65: // Old ShibaGT RGB
                    backgroundColor = new ExtGradient
                    {
                        colors = new[]
                        {
                            new GradientColorKey(Color.red, 0f),
                            new GradientColorKey(Color.green, 0.333f),
                            new GradientColorKey(Color.blue, 0.666f),
                            new GradientColorKey(Color.red, 1f),
                        }
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = new[]
                            {
                                new GradientColorKey(Color.red, 0f),
                                new GradientColorKey(Color.green, 0.333f),
                                new GradientColorKey(Color.blue, 0.666f),
                                new GradientColorKey(Color.red, 1f),
                            }
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 66: // Old-ish ShibaGT RGB
                    backgroundColor = new ExtGradient
                    {
                        colors = new[]
                        {
                            new GradientColorKey(Color.yellow, 0f),
                            new GradientColorKey(Color.red, 0.2f),
                            new GradientColorKey(Color.magenta, 0.4f),
                            new GradientColorKey(Color.blue, 0.6f),
                            new GradientColorKey(Color.green, 0.8f),
                            new GradientColorKey(Color.yellow, 1f)
                        }
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient // Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.black)
                        },
                        new ExtGradient // Pressed
                        {
                            colors = new[]
                            {
                                new GradientColorKey(Color.yellow, 0f),
                                new GradientColorKey(Color.red, 0.2f),
                                new GradientColorKey(Color.magenta, 0.4f),
                                new GradientColorKey(Color.blue, 0.6f),
                                new GradientColorKey(Color.green, 0.8f),
                                new GradientColorKey(Color.yellow, 1f)
                            }
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient // Title
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Released
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient // Button Clicked
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 67: // Halloween
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(58, 16, 92, 255), new Color32(12, 6, 20, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(214, 98, 18, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(120, 48, 6, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 160, 44, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(28, 12, 34, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 214, 160, 255))
                        }
                    };
                    break;
                case 68: // October
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(74, 10, 14, 255), new Color32(10, 4, 6, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(122, 22, 26, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(216, 48, 40, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(226, 74, 62, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(240, 196, 190, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
                case 69: // poopooVR
                    backgroundColor = new ExtGradient
                    {
                        colors = ExtGradient.GetSimpleGradient(new Color32(60, 40, 20, 255), new Color32(30, 20, 10, 255))
                    };
                    buttonColors = new[]
                    {
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(15, 15, 15, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(35, 35, 35, 255))
                        }
                    };
                    textColors = new[]
                    {
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(new Color32(255, 230, 200, 255))
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        },
                        new ExtGradient
                        {
                            colors = ExtGradient.GetSolidGradient(Color.white)
                        }
                    };
                    break;
            }
        }

        private static int menuScaleIndex = 10;
        public static void ChangeMenuScale(bool positive = true, bool cycle = true)
        {
            if (cycle)
            {
                if (positive)
                    menuScaleIndex++;
                else
                    menuScaleIndex--;
            }

            if (menuScaleIndex > 30)
                menuScaleIndex = 2;
            if (menuScaleIndex < 2)
                menuScaleIndex = 30;

            menuScale = menuScaleIndex / 10f;

            ButtonInfo btn = Buttons.GetIndex("Change Menu Scale");
            if (btn != null) btn.overlapText = "Change Menu Scale <color=grey>[</color><color=green>" + menuScale + "</color><color=grey>]</color>";
        }

        private static int notificationScaleIndex = 6;
        public static void ChangeNotificationScale(bool positive = true, bool cycle = true)
        {
            if (cycle)
            {
                if (positive)
                    notificationScaleIndex++;
                else
                    notificationScaleIndex--;
            }

            if (notificationScaleIndex > 20)
                notificationScaleIndex = 1;
            if (notificationScaleIndex < 1)
                notificationScaleIndex = 20;

            notificationScale = notificationScaleIndex * 5;

            ButtonInfo btn = Buttons.GetIndex("Change Notification Scale");
            if (btn != null) btn.overlapText = "Change Notification Scale <color=grey>[</color><color=green>" + notificationScaleIndex + "</color><color=grey>]</color>";
        }

        private static int arraylistScaleIndex = 4;
        public static void ChangeArraylistScale(bool positive = true, bool cycle = true)
        {
            if (cycle)
            {
                if (positive)
                    arraylistScaleIndex++;
                else
                    arraylistScaleIndex--;
            }

            if (arraylistScaleIndex > 20)
                arraylistScaleIndex = 1;
            if (arraylistScaleIndex < 1)
                arraylistScaleIndex = 20;

            arraylistScale = arraylistScaleIndex * 5;

            ButtonInfo btn = Buttons.GetIndex("Change Arraylist Scale");
            if (btn != null) btn.overlapText = "Change Arraylist Scale <color=grey>[</color><color=green>" + arraylistScaleIndex + "</color><color=grey>]</color>";
        }

        private static int overlayScaleIndex = 6;
        public static void ChangeOverlayScale(bool positive = true, bool cycle = true)
        {
            if (cycle)
            {
                if (positive)
                    overlayScaleIndex++;
                else
                    overlayScaleIndex--;
            }

            if (overlayScaleIndex > 20)
                overlayScaleIndex = 1;
            if (overlayScaleIndex < 1)
                overlayScaleIndex = 20;

            overlayScale = overlayScaleIndex * 5;

            ButtonInfo btn = Buttons.GetIndex("Change Overlay Scale");
            if (btn != null) btn.overlapText = "Change Overlay Scale <color=grey>[</color><color=green>" + overlayScaleIndex + "</color><color=grey>]</color>";
        }

        private static int modifyWhatId;
        public static void CMTRed(bool increase = true)
        {
            switch (modifyWhatId)
            {
                case 0:
                    {
                        int r = (int)Math.Round(backgroundColor.GetColor(0).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            backgroundColor.SetColor(0, new Color(r / 10f, backgroundColor.GetColor(0).g, backgroundColor.GetColor(0).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 1:
                    {
                        int r = (int)Math.Round(backgroundColor.GetColor(1).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            backgroundColor.SetColor(1, new Color(r / 10f, backgroundColor.GetColor(1).g, backgroundColor.GetColor(1).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 2:
                    {
                        int r = (int)Math.Round(buttonColors[0].GetColor(0).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[0].SetColor(0, new Color(r / 10f, buttonColors[0].GetColor(0).g, buttonColors[0].GetColor(0).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 3:
                    {
                        int r = (int)Math.Round(buttonColors[0].GetColor(1).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[0].SetColor(1, new Color(r / 10f, buttonColors[0].GetColor(1).g, buttonColors[0].GetColor(1).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 4:
                    {
                        int r = (int)Math.Round(buttonColors[1].GetColor(0).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[1].SetColor(0, new Color(r / 10f, buttonColors[1].GetColor(0).g, buttonColors[1].GetColor(0).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 5:
                    {
                        int r = (int)Math.Round(buttonColors[1].GetColor(1).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[1].SetColor(1, new Color(r / 10f, buttonColors[1].GetColor(1).g, buttonColors[1].GetColor(1).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 6:
                    {
                        int r = (int)Math.Round(textColors[0].GetColor(0).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[0].SetColors(new Color(r / 10f, textColors[0].GetColor(0).g, textColors[0].GetColor(0).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[0].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 7:
                    {
                        int r = (int)Math.Round(textColors[1].GetColor(0).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        textColors[1].SetColors(new Color(r / 10f, textColors[1].GetColor(0).g, textColors[1].GetColor(0).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[1].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 8:
                    {
                        int r = (int)Math.Round(textColors[2].GetColor(0).r * 10f);

                        if (increase)
                            r++;
                        else
                            r--;

                        r %= 11;
                        if (r < 0)
                            r = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[2].SetColors(new Color(r / 10f, textColors[2].GetColor(0).g, textColors[2].GetColor(0).b));

                        Buttons.GetIndex("Red").overlapText = "Red <color=grey>[</color><color=green>" + r + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[2].GetColor(0)) + ">Preview</color>";
                        break;
                    }
            }
            WriteCustomTheme();
        }

        public static void CMTGreen(bool increase = true)
        {
            switch (modifyWhatId)
            {
                case 0:
                    {
                        int g = (int)Math.Round(backgroundColor.GetColor(0).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            backgroundColor.SetColor(0, new Color(backgroundColor.GetColor(0).r, g / 10f, backgroundColor.GetColor(0).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 1:
                    {
                        int g = (int)Math.Round(backgroundColor.GetColor(1).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            backgroundColor.SetColor(1, new Color(backgroundColor.GetColor(1).r, g / 10f, backgroundColor.GetColor(1).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 2:
                    {
                        int g = (int)Math.Round(buttonColors[0].GetColor(0).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[0].SetColor(0, new Color(buttonColors[0].GetColor(0).r, g / 10f, buttonColors[0].GetColor(0).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 3:
                    {
                        int g = (int)Math.Round(buttonColors[0].GetColor(1).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[0].SetColor(1, new Color(buttonColors[0].GetColor(1).r, g / 10f, buttonColors[0].GetColor(1).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 4:
                    {
                        int g = (int)Math.Round(buttonColors[1].GetColor(0).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[1].SetColor(0, new Color(buttonColors[1].GetColor(0).r, g / 10f, buttonColors[1].GetColor(0).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 5:
                    {
                        int g = (int)Math.Round(buttonColors[1].GetColor(1).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[1].SetColor(1, new Color(buttonColors[1].GetColor(1).r, g / 10f, buttonColors[1].GetColor(1).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 6:
                    {
                        int g = (int)Math.Round(textColors[0].GetColor(0).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[0].SetColors(new Color(textColors[0].GetColor(0).r, g / 10f, textColors[0].GetColor(0).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[0].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 7:
                    {
                        int g = (int)Math.Round(textColors[1].GetColor(0).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[1].SetColors(new Color(textColors[1].GetColor(0).r, g / 10f, textColors[1].GetColor(0).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[1].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 8:
                    {
                        int g = (int)Math.Round(textColors[2].GetColor(0).g * 10f);

                        if (increase)
                            g++;
                        else
                            g--;

                        g %= 11;
                        if (g < 0)
                            g = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[2].SetColors(new Color(textColors[2].GetColor(0).r, g / 10f, textColors[2].GetColor(0).b));

                        Buttons.GetIndex("Green").overlapText = "Green <color=grey>[</color><color=green>" + g + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[2].GetColor(0)) + ">Preview</color>";
                        break;
                    }
            }
            WriteCustomTheme();
        }
        public static void CMTBlue(bool increase = true)
        {
            switch (modifyWhatId)
            {
                case 0:
                    {
                        int b = (int)Math.Round(backgroundColor.GetColor(0).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            backgroundColor.SetColor(0, new Color(backgroundColor.GetColor(0).r, backgroundColor.GetColor(0).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 1:
                    {
                        int b = (int)Math.Round(backgroundColor.GetColor(1).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            backgroundColor.SetColor(1, new Color(backgroundColor.GetColor(1).r, backgroundColor.GetColor(1).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 2:
                    {
                        int b = (int)Math.Round(buttonColors[0].GetColor(0).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[0].SetColor(0, new Color(buttonColors[0].GetColor(0).r, buttonColors[0].GetColor(0).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 3:
                    {
                        int b = (int)Math.Round(buttonColors[0].GetColor(1).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[0].SetColor(1, new Color(buttonColors[0].GetColor(1).r, buttonColors[0].GetColor(1).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 4:
                    {
                        int b = (int)Math.Round(buttonColors[1].GetColor(0).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[1].SetColor(0, new Color(buttonColors[1].GetColor(0).r, buttonColors[1].GetColor(0).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 5:
                    {
                        int b = (int)Math.Round(buttonColors[1].GetColor(1).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            buttonColors[1].SetColor(1, new Color(buttonColors[1].GetColor(1).r, buttonColors[1].GetColor(1).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(1)) + ">Preview</color>";
                        break;
                    }
                case 6:
                    {
                        int b = (int)Math.Round(textColors[0].GetColor(0).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[0].SetColors(new Color(textColors[0].GetColor(0).r, textColors[0].GetColor(0).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[0].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 7:
                    {
                        int b = (int)Math.Round(textColors[1].GetColor(0).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[1].SetColors(new Color(textColors[1].GetColor(0).r, textColors[1].GetColor(0).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[1].GetColor(0)) + ">Preview</color>";
                        break;
                    }
                case 8:
                    {
                        int b = (int)Math.Round(textColors[2].GetColor(0).b * 10f);

                        if (increase)
                            b++;
                        else
                            b--;

                        b %= 11;
                        if (b < 0)
                            b = 10;

                        if (Buttons.GetIndex("Custom Menu Theme").enabled)
                            textColors[2].SetColors(new Color(textColors[2].GetColor(0).r, textColors[2].GetColor(0).g, b / 10f));

                        Buttons.GetIndex("Blue").overlapText = "Blue <color=grey>[</color><color=green>" + b + "</color><color=grey>]</color>";
                        Buttons.GetIndex("PreviewLabel").overlapText = "<color=#" + ColorToHex(textColors[2].GetColor(0)) + ">Preview</color>";
                        break;
                    }
            }
            WriteCustomTheme();
        }

        private static int previousPage;
        public static void CustomMenuTheme()
        {
            if (!File.Exists($"{PluginInfo.BaseDirectory}/iiMenu_CustomThemeColor.txt"))
                WriteCustomTheme();

            ReadCustomTheme();
        }

        public static void ChangeCustomMenuTheme()
        {
            previousPage = pageNumber;
            CustomMenuThemePage();
        }

        public static void CustomMenuThemePage()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Custom Menu Theme", method = () => ExitCustomMenuTheme(), isTogglable = false, toolTip = "Returns you back to the settings menu." },
                new ButtonInfo { buttonText = "Background", method = () => CMTBackground(), isTogglable = false, toolTip = "Choose what segment of the background you would like to modify." },
                new ButtonInfo { buttonText = "Buttons", method = () => CMTButton(), isTogglable = false, toolTip = "Choose what segment of the button you would like to modify." },
                new ButtonInfo { buttonText = "Text", method = () => CMTText(), isTogglable = false, toolTip = "Choose what segment of the text you would like to modify." },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void CMTBackground()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Background", method = () => CustomMenuThemePage(), isTogglable = false, toolTip = "Returns you back to the customize menu." },
                new ButtonInfo { buttonText = "First Color", method = () => CMTBackgroundFirst(), isTogglable = false, toolTip = "Change the color of the first color of the background." },
                new ButtonInfo { buttonText = "Second Color", method = () => CMTBackgroundSecond(), isTogglable = false, toolTip = "Change the color of the second color of the background." },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTBackgroundFirst()
        {
            modifyWhatId = 0;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit First Color", method = () => CMTBackground(), isTogglable = false, toolTip = "Returns you back to the background menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(backgroundColor.GetColor(0).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the first color of the background." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(backgroundColor.GetColor(0).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the first color of the background." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(backgroundColor.GetColor(0).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the first color of the background." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(0)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTBackgroundSecond()
        {
            modifyWhatId = 1;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Second Color", method = () => CMTBackground(), isTogglable = false, toolTip = "Returns you back to the background menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(backgroundColor.GetColor(1).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the second color of the background." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(backgroundColor.GetColor(1).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the second color of the background." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(backgroundColor.GetColor(1).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the second color of the background." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(backgroundColor.GetColor(1)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void CMTButton()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Buttons", method = CustomMenuThemePage, isTogglable = false, toolTip = "Returns you back to the customize menu." },
                new ButtonInfo { buttonText = "Enabled", method = CMTButtonEnabled, isTogglable = false, toolTip = "Choose what type of button color to modify." },
                new ButtonInfo { buttonText = "Disabled", method = CMTButtonDisabled, isTogglable = false, toolTip = "Change the color of the second color of the background." },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTButtonEnabled()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Enabled", method = CMTButton, isTogglable = false, toolTip = "Returns you back to the customize menu." },
                new ButtonInfo { buttonText = "First Color", method = CMTButtonEnabledFirst, isTogglable = false, toolTip = "Change the color of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "Second Color", method = () => CMTButtonEnabledSecond(), isTogglable = false, toolTip = "Change the color of the second color of the enabled button color." },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTButtonDisabled()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Enabled", method = () => CMTButton(), isTogglable = false, toolTip = "Returns you back to the customize menu." },
                new ButtonInfo { buttonText = "First Color", method = () => CMTButtonDisabledFirst(), isTogglable = false, toolTip = "Change the color of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "Second Color", method = () => CMTButtonDisabledSecond(), isTogglable = false, toolTip = "Change the color of the second color of the disabled button color." },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTButtonEnabledFirst()
        {
            modifyWhatId = 4;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit First Color", method = () => CMTButtonEnabled(), isTogglable = false, toolTip = "Returns you back to the enabled button menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[1].GetColor(0).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[1].GetColor(0).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[1].GetColor(0).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(0)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTButtonEnabledSecond()
        {
            modifyWhatId = 5;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Second Color", method = () => CMTButtonEnabled(), isTogglable = false, toolTip = "Returns you back to the enabled button menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[1].GetColor(1).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[1].GetColor(1).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[1].GetColor(1).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the first color of the enabled button color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(buttonColors[1].GetColor(1)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTButtonDisabledFirst()
        {
            modifyWhatId = 2;
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit First Color", method = () => CMTButtonDisabled(), isTogglable = false, toolTip = "Returns you back to the disabled button menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[0].GetColor(0).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[0].GetColor(0).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[0].GetColor(0).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(0)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTButtonDisabledSecond()
        {
            modifyWhatId = 3;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Second Color", method = CMTButtonDisabled, isTogglable = false, toolTip = "Returns you back to the disabled button menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[0].GetColor(1).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[0].GetColor(1).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(buttonColors[0].GetColor(1).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the first color of the disabled button color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(buttonColors[0].GetColor(1)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void CMTText()
        {
            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Text", method = CustomMenuThemePage, isTogglable = false, toolTip = "Returns you back to the customize menu." },
                new ButtonInfo { buttonText = "Title", method = CMTTextTitle, isTogglable = false, toolTip = "Change the color of the title." },
                new ButtonInfo { buttonText = "Enabled", method = CMTTextEnabled, isTogglable = false, toolTip = "Change the color of the enabled text." },
                new ButtonInfo { buttonText = "Disabled", method = CMTTextDisabled, isTogglable = false, toolTip = "Change the color of the disabled text." },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTTextTitle()
        {
            modifyWhatId = 6;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Title", method = CMTText, isTogglable = false, toolTip = "Returns you back to the text menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(textColors[0].GetColor(0).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the title color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(textColors[0].GetColor(0).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the title color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(textColors[0].GetColor(0).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the title color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(textColors[0].GetColor(0)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTTextEnabled()
        {
            modifyWhatId = 8;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Second Color", method = () => CMTText(), isTogglable = false, toolTip = "Returns you back to the text menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(textColors[2].GetColor(0).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the enabled text color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(textColors[2].GetColor(0).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the enabled text color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(textColors[2].GetColor(0).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the enabled text color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(textColors[2].GetColor(0)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }
        public static void CMTTextDisabled()
        {
            modifyWhatId = 7;

            List<ButtonInfo> buttons = new List<ButtonInfo> {
                new ButtonInfo { buttonText = "Exit Second Color", method = () => CMTText(), isTogglable = false, toolTip = "Returns you back to the text menu." },
                new ButtonInfo { buttonText = "Red", overlapText = "Red <color=grey>[</color><color=green>" + (int)Math.Round(textColors[1].GetColor(0).r * 10f) + "</color><color=grey>]</color>", method =() => CMTRed(), enableMethod =() => CMTRed(), disableMethod =() => CMTRed(false), incremental = true, isTogglable = false, toolTip = "Change the red of the disabled text color." },
                new ButtonInfo { buttonText = "Green", overlapText = "Green <color=grey>[</color><color=green>" + (int)Math.Round(textColors[1].GetColor(0).g * 10f) + "</color><color=grey>]</color>", method =() => CMTGreen(), enableMethod =() => CMTGreen(), disableMethod =() => CMTGreen(false), incremental = true, isTogglable = false, toolTip = "Change the green of the disabled text color." },
                new ButtonInfo { buttonText = "Blue", overlapText = "Blue <color=grey>[</color><color=green>" + (int)Math.Round(textColors[1].GetColor(0).b * 10f) + "</color><color=grey>]</color>", method =() => CMTBlue(), enableMethod =() => CMTBlue(), disableMethod =() => CMTBlue(false), incremental = true, isTogglable = false, toolTip = "Change the blue of the disabled text color." },
                new ButtonInfo { buttonText = "PreviewLabel", overlapText = "<color=#" + ColorToHex(textColors[1].GetColor(0)) + ">Preview</color>", label = true },
            };

            Buttons.buttons[Buttons.GetCategory("Temporary Category")] = buttons.ToArray();
            Buttons.CurrentCategoryName = "Temporary Category";
        }

        public static void ExitCustomMenuTheme()
        {
            pageNumber = previousPage;
            Buttons.CurrentCategoryName = "Menu Settings";
        }

        public static void ReadCustomTheme()
        {
            string[] linesplit = File.ReadAllText($"{PluginInfo.BaseDirectory}/iiMenu_CustomThemeColor.txt").Split("\n");

            string[] a = linesplit[0].Split(",");
            backgroundColor.SetColor(0, new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));
            a = linesplit[1].Split(",");
            backgroundColor.SetColor(1, new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));

            a = linesplit[2].Split(",");
            buttonColors[0].SetColor(0, new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));
            a = linesplit[3].Split(",");
            buttonColors[0].SetColor(1, new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));

            a = linesplit[4].Split(",");
            buttonColors[1].SetColor(0, new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));
            a = linesplit[5].Split(",");
            buttonColors[1].SetColor(1, new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));

            a = linesplit[6].Split(",");
            textColors[0].SetColors(new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));
            a = linesplit[7].Split(",");
            textColors[1].SetColors(new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));
            a = linesplit[8].Split(",");
            textColors[2].SetColors(new Color32(byte.Parse(a[0]), byte.Parse(a[1]), byte.Parse(a[2]), 255));
        }

        public static void ImportCustomTheme(string theme)
        {
            File.WriteAllText($"{PluginInfo.BaseDirectory}/iiMenu_CustomThemeColor.txt", theme);
            ReadCustomTheme();
        }

        public static string ExportCustomTheme()
        {
            Color[] clrs = {
                backgroundColor.GetColor(0),
                backgroundColor.GetColor(1),
                buttonColors[0].GetColor(0),
                buttonColors[0].GetColor(1),
                buttonColors[1].GetColor(0),
                buttonColors[1].GetColor(1),
                textColors[0].GetColor(0),
                textColors[1].GetColor(0),
                textColors[2].GetColor(0)
            };

            string output = "";
            foreach (Color clr in clrs)
            {
                if (output != "")
                    output += "\n";

                output += Math.Round(Mathf.Round(clr.r * 10) / 10 * 255f) + "," + Math.Round(Mathf.Round(clr.g * 10) / 10 * 255f) + "," + Math.Round(Mathf.Round(clr.b * 10) / 10 * 255f);
            }

            return output;
        }

        public static void WriteCustomTheme() =>
            File.WriteAllText($"{PluginInfo.BaseDirectory}/iiMenu_CustomThemeColor.txt", ExportCustomTheme());

        public static void FixTheme()
        {
            themeType--;
            ChangeMenuTheme();
        }

        public static void CustomMenuBackground()
        {
            if (!File.Exists($"{PluginInfo.BaseDirectory}/CustomBackground.png"))
                LoadTextureFromURL($"{PluginInfo.ServerResourcePath}/Images/CustomBackground.png", "CustomBackground.png"); // Do not move outside of its path

            textureFileDirectory.Remove("CustomBackground.png");

            doCustomMenuBackground = true;
            customMenuBackgroundImage = LoadTextureFromFile("CustomBackground.png");
        }

        public static void FixMenuBackground()
        {
            customMenuBackgroundImage = null;
            doCustomMenuBackground = false;
        }

        public static void EnableWatermark()
        {
            bool enabled = Buttons.GetIndex("Custom Watermark").enabled;
            if (enabled)
            {
                if (!File.Exists($"{PluginInfo.BaseDirectory}/CustomWatermark.png"))
                    LoadTextureFromURL($"{PluginInfo.ServerResourcePath}/Images/CustomWatermark.png", "CustomWatermark.png"); // Do not move outside of its path

                textureFileDirectory.Remove("CustomWatermark.png");
                customWatermark = LoadTextureFromFile("CustomWatermark.png");
            }
            else
            {
                watermarkImage = new GameObject
                {
                    transform =
                    {
                        parent = canvasObj.transform
                    }
                }.AddComponent<Image>();

                if (watermarkMat == null)
                    watermarkMat = new Material(watermarkImage.material);

                watermarkImage.material = watermarkMat;
                watermarkImage.material.SetTexture("_MainTex", customWatermark ?? LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.icon.png"));
            }
        }

        public static void CustomWatermark()
        {
            if (!File.Exists($"{PluginInfo.BaseDirectory}/CustomWatermark.png"))
                LoadTextureFromURL($"{PluginInfo.ServerResourcePath}/Images/CustomWatermark.png", "CustomWatermark.png"); // Do not move outside of its path

            textureFileDirectory.Remove("CustomWatermark.png");
            customWatermark = LoadTextureFromFile("CustomWatermark.png");
        }

        private static TMP_FontAsset chosenFont;
        public static void CustomFontType()
        {
            string filePath = $"{PluginInfo.BaseDirectory}/CustomFont.ttf";
            if (!File.Exists(filePath))
            {
                LogManager.Log("Downloading CustomFont.ttf");
                try
                {
                    using HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
                    byte[] bytes = http.GetByteArrayAsync($"{PluginInfo.ServerResourcePath}/Fonts/LiberationSans.ttf").GetAwaiter().GetResult();
                    File.WriteAllBytes(filePath, bytes);
                }
                catch (Exception e)
                {
                    LogManager.LogError($"Failed to download CustomFont.ttf: {e.Message}");
                }
            }

            chosenFont = TMP_FontAsset.CreateFontAsset(new Font($"{FileUtilities.GetGamePath()}/{filePath}"));
            PersistCustomFont();
        }

        public static void PersistCustomFont()
        {
            if (activeFont != chosenFont)
                activeFont = chosenFont;
        }

        public static void DisableCustomFont()
        {
            fontCycle--;
            ChangeFontType();
        }

        public static void ChangePageType(bool positive = true)
        {
            if (positive)
                pageButtonType++;
            else
                pageButtonType--;

            if (pageButtonType > 6)
                pageButtonType = 1;

            if (pageButtonType < 1)
                pageButtonType = 6;

            buttonOffset = pageButtonType == 1 ? 2 : 0;
        }

        public static void ChangePageSize(bool positive = true, bool cycle = true)
        {
            if (cycle)
            {
                if (positive)
                    _pageSize++;
                else
                    _pageSize--;
            }

            if (_pageSize > 16)
                _pageSize = 4;

            if (_pageSize < 4)
                _pageSize = 16;

            ButtonInfo btn = Buttons.GetIndex("Change Page Size");
            if (btn != null) btn.overlapText = $"Change Page Size <color=grey>[</color><color=green>{_pageSize}</color><color=grey>]</color>";
        }

        public static void ChangeCharacterDistance(bool positive = true)
        {
            if (positive)
                characterDistance++;
            else
                characterDistance--;

            if (characterDistance > 15)
                characterDistance = 0;

            if (characterDistance < 0)
                characterDistance = 15;

            Buttons.GetIndex("Change Character Distance").overlapText = $"Change Character Distance <color=grey>[</color><color=green>{characterDistance + 1}</color><color=grey>]</color>";
        }

        public static void ChangeArrowType(bool positive = true)
        {
            if (positive)
                arrowType++;
            else
                arrowType--;

            arrowType %= arrowTypes.Length;
            if (arrowType < 0)
                arrowType = arrowTypes.Length - 1;
        }

        public static void ChangeFontType(bool positive = true)
        {
            if (positive)
                fontCycle++;
            else
                fontCycle--;

            fontCycle %= 15;
            if (fontCycle < 0)
                fontCycle = 14;

            switch (fontCycle)
            {
                case 0:
                    activeFont = AgencyFB;
                    return;
                case 1:
                    activeFont = FreeSans;
                    return;
                case 2:
                    activeFont = DejaVuSans;
                    return;
                case 3:
                    activeFont = Utopium;
                    return;
                case 4:
                    activeFont = ComicSans;
                    return;
                case 5:
                    activeFont = CascadiaMono;
                    return;
                case 6:
                    activeFont = Candara;
                    return;
                case 7:
                    activeFont = MSGothic;
                    return;
                case 8:
                    activeFont = Anton;
                    return;
                case 9:
                    activeFont = SimSun;
                    return;
                case 10:
                    activeFont = Minecraft;
                    return;
                case 11:
                    activeFont = Terminal;
                    return;
                case 12:
                    activeFont = OpenDyslexic;
                    return;
                case 13:
                    activeFont = Taiko;
                    return;
                case 14:
                    activeFont = LiberationSans;
                    return;
            }
        }

        public static float fontTime;
        public static void ChangeFontRapid()
        {
            if (Time.time > fontTime)
            {
                ChangeFontType();
                fontTime = Time.time + 0.4f;

                ReloadMenu();
            }
        }

        public static int fontStyleType = 2;
        public static void ChangeFontStyleType(bool positive = true)
        {
            if (positive)
                fontStyleType++;
            else
                fontStyleType--;

            fontStyleType %= 4;
            if (fontStyleType < 0)
                fontStyleType = 3;

            activeFontStyle = fontStyleType switch
            {
                0 => FontStyles.Normal,
                1 => FontStyles.Bold,
                2 => FontStyles.Italic,
                3 => FontStyles.Bold | FontStyles.Italic,
                _ => FontStyles.Normal
            };
        }

        public static int inputTextColorInt = 3;
        public static void ChangeInputTextColor(bool positive = true)
        {
            string[] textColors = {
                "Red",
                "Orange",
                "Yellow",
                "Green",
                "Blue",
                "Cyan",
                "Purple",
                "Pink",
                "White",
                "Grey",
                "Black",
                "Rose"
            };
            string[] realinputcolor = {
                "red",
                "#ff8000",
                "yellow",
                "green",
                "blue",
                "#00FFFF",
                "purple",
                "#FF00FF",
                "white",
                "grey",
                "black",
                "#ff005d"
            };

            if (positive)
                inputTextColorInt++;
            else
                inputTextColorInt--;

            inputTextColorInt %= realinputcolor.Length;
            if (inputTextColorInt < 0)
                inputTextColorInt = realinputcolor.Length - 1;

            inputTextColor = realinputcolor[inputTextColorInt];
            Buttons.GetIndex("Change Input Text Color").overlapText = $"Change Input Text Color <color=grey>[</color><color=green>{textColors[inputTextColorInt]}</color><color=grey>]</color>";
        }

        public static void ChangePCUI(bool positive = true)
        {
            if (positive)
                pcbg++;
            else
                pcbg--;

            pcbg %= 6;
            if (pcbg < 0)
                pcbg = 5;
        }

        public static void ChangeJoystickMenuPosition(bool positive = true)
        {
            if (positive)
                joystickMenuPosition++;
            else
                joystickMenuPosition--;

            joystickMenuPosition %= joystickMenuPositions.Length;
            if (joystickMenuPosition < 0)
                joystickMenuPosition = joystickMenuPositions.Length - 1;
        }

        public static void ChangeNotificationTime(bool positive = true)
        {
            if (positive)
                notificationDecayTime += 1000;
            else
                notificationDecayTime -= 1000;

            notificationDecayTime %= 6000;
            if (notificationDecayTime < 0)
                notificationDecayTime = 5000;

            Buttons.GetIndex("Change Notification Time").overlapText = "Change Notification Time <color=grey>[</color><color=green>" + notificationDecayTime / 1000 + "</color><color=grey>]</color>";
        }

        public static void ChangeQuietNotifications(bool positive = true)
        {
            quietNotifications = positive;
            Buttons.GetIndex("Quiet Notifications").enabled = quietNotifications;
            Buttons.GetIndex("Quiet Notifications").overlapText = "Quiet Notifications <color=grey>[</color><color=green>" + (quietNotifications ? "On" : "Off") + "</color><color=grey>]</color>";
        }

        public static bool quietNotifications;


        public static readonly Dictionary<string, string> notificationSounds = new Dictionary<string, string>
        {
            { "None",          "none"        },
            { "Pop",           "pop"         },
            { "Ding",          "ding"        },
            { "Twitter",       "twitter"     },
            { "Discord",       "discord"     },
            { "Whatsapp",      "whatsapp"    },
            { "Grindr",        "grindr"      },
            { "iOS",           "ios"         },
            { "XP Notify",     "xpnotify"    },
            { "XP Ding",       "xptrueding"  },
            { "XP Question",   "xpding"      },
            { "XP Error",      "xperror"     },
            { "Roblox Bass",   "robloxbass"  },
            { "Oculus",        "oculus"      },
            { "Nintendo",      "nintendo"    },
            { "Telegram",      "telegram"    },
            { "7 Ding",        "win7-ding"   },
            { "7 Error",       "win7-error"  },
            { "7 Exclamation", "win7-exc"    },
            { "AOL Alert",     "aol-alert"   },
            { "AOL Message",   "aol-msg"     },
            { "Thunderbird",   "thunderbird" },
            { "Pixie Dust",    "pixiedust"   },
            { "Moon Beam",     "moonbeam"    },
            { "Dog",           "dog"         },
            { "GMod Error",    "gmod-error"  }
        };

        public static void ChangeNotificationSound(bool positive = true, bool fromMenu = false)
        {
            if (positive)
                notificationSoundIndex++;
            else
                notificationSoundIndex--;

            notificationSoundIndex %= notificationSounds.Keys.Count;
            if (notificationSoundIndex < 0)
                notificationSoundIndex = notificationSounds.Keys.Count - 1;

            Buttons.GetIndex("Change Notification Sound").overlapText = "Change Notification Sound <color=grey>[</color><color=green>" + notificationSounds.Keys.ToArray()[notificationSoundIndex] + "</color><color=grey>]</color>";

            if (fromMenu)
            {
                audioManager.GetComponent<AudioSource>().Stop();
                NotificationManager.PlayNotificationSound();
            }
        }

        public static void ChangeNarrationVoice(bool positive = true)
        {
            string[] narratorNames = {
                "Default",
                "Kimberly",
                "Brian",
                "Matthew",
                "Joey",
                "Justin",
                "Cristiano",
                "Giorgio",
                "Ewa",
                "TikTok",
                "Grandma",
                "Trickster",
                "Elf",
                "Ghostface",
                "Zombie",
                "Narrator",
                "Pirate",
                "Song",
                "TikTok Joey",
                "Gingerbread Man",
                "Chris",
                "Thanksgiving",
                "Santa",
                "Google US",
                "Google UK",
                "Dog",
                "Jerkface",
                "Robot",
                "Vlad",
                "Obama"/*,
                "Mommy ASMR"*/
            };

            if (positive)
                narratorIndex++;
            else
                narratorIndex--;

            narratorIndex %= narratorNames.Length;
            if (narratorIndex < 0)
                narratorIndex = narratorNames.Length - 1;

            Buttons.GetIndex("Change Narration Voice").overlapText = "Change Narration Voice <color=grey>[</color><color=green>" + narratorNames[narratorIndex] + "</color><color=grey>]</color>";
            narratorName = narratorNames[narratorIndex];

            if (krec != null && krec.IsRunning && Time.time > dRestartTime)
            {
                DictationRestart();
                dRestartTime = Time.time + 1f;
            }
        }

        public static void KickToSpecificRoom()
        {
            if (Time.time < timeMenuStarted + 5f)
            {
                Buttons.GetIndex("Kick to Specific Room").enabled = false;
                return;
            }

            PromptText("What would you like the room code to be?", () => Overpowered.specificRoom = keyboardInput.ToUpper(), () => Toggle("Kick to Specific Room"), "Done", "Cancel");
        }
        public static void ChangePointerPosition(bool positive = true)
        {
            Vector3[] pointerPos = {
                new Vector3(0f, -0.1f, 0f),
                new Vector3(0f, -0.1f, -0.15f),
                new Vector3(0f, 0.1f, -0.05f),
                new Vector3(0f, 0.0666f, 0.1f)
            };

            if (positive)
                pointerIndex++;
            else
                pointerIndex--;

            pointerIndex %= pointerPos.Length;
            if (pointerIndex < 0)
                pointerIndex = pointerPos.Length - 1;

            pointerOffset = pointerPos[pointerIndex];
            try { reference.transform.localPosition = pointerOffset; } catch { }
        }
        public static void ChangeGunVariation(bool positive = true)
        {
            string[] VariationNames = {
                "Default",
                "Lightning",
                "Wavy",
                "Blocky",
                "Zigzag",
                "Spring",
                "Bouncy",
                "Audio",
                "Bezier",
                "Rope"
            };

            if (positive)
                gunVariation++;
            else
                gunVariation--;

            gunVariation %= VariationNames.Length;
            if (gunVariation < 0)
                gunVariation = VariationNames.Length - 1;

            Buttons.GetIndex("Change Gun Variation").overlapText = "Change Gun Variation <color=grey>[</color><color=green>" + VariationNames[gunVariation] + "</color><color=grey>]</color>";
        }

        public static void ChangeGunDirection(bool positive = true)
        {
            string[] DirectionNames = {
                "Default",
                "Legacy",
                "Laser",
                "Finger",
                "Face"
            };

            if (positive)
                GunDirection++;
            else
                GunDirection--;

            GunDirection %= DirectionNames.Length;
            if (GunDirection < 0)
                GunDirection = DirectionNames.Length - 1;

            Buttons.GetIndex("Change Gun Direction").overlapText = "Change Gun Direction <color=grey>[</color><color=green>" + DirectionNames[GunDirection] + "</color><color=grey>]</color>";
        }

        private static int gunLineQualityIndex = 2;
        public static void ChangeGunLineQuality(bool positive = true)
        {
            string[] Names = {
                "Potato",
                "Low",
                "Normal",
                "High",
                "Extreme"
            };

            int[] Qualities = {
                10,
                25,
                50,
                100,
                250
            };

            if (positive)
                gunLineQualityIndex++;
            else
                gunLineQualityIndex--;

            gunLineQualityIndex %= Names.Length;
            if (gunLineQualityIndex < 0)
                gunLineQualityIndex = Names.Length - 1;

            GunLineQuality = Qualities[gunLineQualityIndex];
            Buttons.GetIndex("Change Gun Line Quality").overlapText = "Change Gun Line Quality <color=grey>[</color><color=green>" + Names[gunLineQualityIndex] + "</color><color=grey>]</color>";
        }

        public static void FreezePlayerInMenu()
        {
            if (physicalMenu ? isMenuButtonHeld : menu != null)
            {
                if (closePosition == Vector3.zero)
                    closePosition = GorillaTagger.Instance.rigidbody.transform.position;
                else
                    GorillaTagger.Instance.rigidbody.transform.position = closePosition;
                GorillaTagger.Instance.rigidbody.linearVelocity = new Vector3(0f, 0f, 0f);
            }
            else
                closePosition = Vector3.zero;
        }

        public static bool currentmentalstate;
        public static void FreezeRigInMenu()
        {
            if (menu != null)
            {
                if (!currentmentalstate)
                {
                    currentmentalstate = true;
                    VRRig.LocalRig.enabled = false;
                }
            }
            else
            {
                if (currentmentalstate)
                {
                    currentmentalstate = false;
                    VRRig.LocalRig.enabled = true;
                }
            }
        }

        public static void DisorganizeMenu()
        {
            NotificationManager.SendNotification("Disorganize Menu is disabled because it hides category buttons.");
        }

        public static void AnnoyingModeOff()
        {
            annoyingMode = false;
            themeType--;
            ChangeMenuTheme();
        }

        public static void DisablePageButtons()
        {
            if (Buttons.GetIndex("Joystick Menu").enabled)
            {
                disablePageButtons = true;
            }
            else
            {
                Buttons.GetIndex("Disable Page Buttons").enabled = false;
                NotificationManager.SendNotification("<color=grey>[</color><color=red>DISABLE</color><color=grey>]</color> Disable Page Buttons can only be used when using Joystick Menu.");
            }
        }

        public static void CustomMenuName()
        {
            doCustomName = true;
            if (!File.Exists($"{PluginInfo.BaseDirectory}/iiMenu_CustomMenuName.txt"))
                File.WriteAllText($"{PluginInfo.BaseDirectory}/iiMenu_CustomMenuName.txt", "Your Text Here");

            customMenuName = File.ReadAllText($"{PluginInfo.BaseDirectory}/iiMenu_CustomMenuName.txt");
        }

        private static bool lastFocused;
        public static void CheckFocus()
        {
            if (!Application.isFocused && lastFocused && Time.time > timeMenuStarted + 5f)
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> You are not focused on Gorilla Tag. Voice transcription mods will not function. Please focus/click on the game.");

            lastFocused = Application.isFocused;
        }
        private static KeywordRecognizer mainPhrases;
        private static KeywordRecognizer modPhrases;
        private static string[] keyWords = { "jarvis", "ii", "i i", "eye eye", "siri", "google", "alexa", "dummy", "computer", "stinky", "silly", "stupid", "console", "go go gadget", "monika", "wikipedia", "gideon", "a i", "ai", "a.i", "chat gpt", "chatgpt", "grok", "grock", "garmin" };
        private static readonly string[] cancelKeywords = { "nevermind", "cancel", "never mind", "stop", "i hate you", "die" };
        public static void VoiceRecognitionOn()
        {
            // Voice commands and the assistant both own the shared speech system, they must never run together
            if (Buttons.GetIndex("AI Assistant")?.enabled == true)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Turn off AI Assistant first, voice commands and the assistant can not share your microphone.", 4000);

                ButtonInfo voiceCommands = Buttons.GetIndex("Voice Commands");
                if (voiceCommands != null)
                    voiceCommands.enabled = false;

                return;
            }

            if (!File.Exists($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt"))
                File.WriteAllLines($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt", keyWords);
            keyWords = File.ReadAllLines($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt");
            try
            {
                mainPhrases = new KeywordRecognizer(VoiceAssistant.BuildWakeWords(keyWords));
                mainPhrases.OnPhraseRecognized += ModRecognition;
                mainPhrases.Start();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: could not start voice commands: {exception.Message}");
            }
        }

        private static Coroutine timeoutCoroutine;
        public static void ModRecognition(PhraseRecognizedEventArgs args)
        {
            mainPhrases.Stop();
            VoiceAssistant.FlashListening();

            if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                timeoutCoroutine = CoroutineManager.instance.StartCoroutine(Timeout(string.Empty));

            List<string> rawbuttonnames = cancelKeywords.ToList();

            foreach (ButtonInfo[] buttonlist in Buttons.buttons)
            {
                foreach (ButtonInfo v in buttonlist)
                {
                    string buttonName = v.overlapText ?? v.buttonText;

                    if (buttonName.Contains(" <color"))
                        buttonName = buttonName.Split(" <color")[0];

                    rawbuttonnames.Add(buttonName);
                }
            }


            modPhrases = new KeywordRecognizer(rawbuttonnames.ToArray());
            modPhrases.OnPhraseRecognized += ExecuteVoiceCommand;
            modPhrases.Start();

            if (dynamicSounds)
                DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/select.ogg", "Audio/Menu/select.ogg"), buttonClickVolume / 10f);

            NotificationManager.SendNotification("<color=grey>[</color><color=purple>VOICE</color><color=grey>]</color> Listening...", 3000);
        }

        public static void ExecuteVoiceCommand(PhraseRecognizedEventArgs args)
        {
            if (!Buttons.GetIndex("Chain Voice Commands").enabled)
            {
                modPhrases.Stop();
                mainPhrases.Start();
                CoroutineManager.instance.StopCoroutine(timeoutCoroutine);
            }

            if (cancelKeywords.Contains(args.text))
            {
                CancelModRecognition(args.text);
                return;
            }

            string modTarget = null;
            bool exactMatch = false;

            foreach (ButtonInfo[] buttonlist in Buttons.buttons)
            {
                if (exactMatch)
                    break;

                foreach (ButtonInfo v in buttonlist)
                {
                    if (exactMatch)
                        break;

                    string buttonName = v.overlapText ?? v.buttonText;

                    if (buttonName.Contains(" <color"))
                        buttonName = buttonName.Split(" <color")[0];

                    if (args.text.ToLower() == buttonName.ToLower())
                    {
                        modTarget = v.buttonText;
                        exactMatch = true;
                    }
                    else
                    {
                        if (args.text.Contains(buttonName.ToLower()))
                            modTarget = v.buttonText;
                    }
                }
            }

            if (modTarget != null)
            {
                ButtonInfo mod = Buttons.GetIndex(modTarget);
                NotificationManager.SendNotification("<color=grey>[</color><color=" + (mod.enabled ? "red" : "green") + ">VOICE</color><color=grey>]</color> " + (mod.enabled ? "Disabling " : "Enabling ") + (mod.overlapText ?? mod.buttonText) + "...", 3000);
                if (dynamicSounds)
                    DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg"), buttonClickVolume / 10f);

                Toggle(modTarget, true, true);
            }
            else
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>VOICE</color><color=grey>]</color> No command found (" + args.text + ").", 3000);
                if (dynamicSounds)
                    DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg"), buttonClickVolume / 10f);
            }
        }

        public static IEnumerator Timeout(string text)
        {
            yield return new WaitForSeconds(10f);
            CancelModRecognition(text);
        }

        public static void CancelModRecognition(string text)
        {
            modPhrases.Stop();
            mainPhrases.Start();
            try
            {
                CoroutineManager.instance.StopCoroutine(timeoutCoroutine);
            }
            catch { }

            NotificationManager.SendNotification($"<color=grey>[</color><color=red>VOICE</color><color=grey>]</color> {(text == "i hate you" ? "I hate you too." : "Cancelling...")}", 3000);
            if (dynamicSounds)
                DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg"), buttonClickVolume / 10f);
        }

        public static void VoiceRecognitionOff()
        {
            mainPhrases?.Dispose();
            mainPhrases?.Stop();
            modPhrases?.Dispose();
            modPhrases?.Stop();
            mainPhrases = null;
            modPhrases = null;

            // Only shut the system down when it is actually running, shutting it down twice can hard crash
            SafePhraseShutdown();
        }
        public static DictationRecognizer drec;
        public static KeywordRecognizer krec;
        public static bool dictationActive;
        private static bool dictationStarting;
        private static float dictationStartedAt;
        private static string[] wakeWords;
        public static bool debugDictation;
        public static bool restartOnFocus;
        public static float dRestartTime;

        public static IEnumerator DictationOn()
        {
            ButtonInfo mod = Buttons.GetIndex("AI Assistant");

            if (mod == null)
                yield break;

            if (Application.platform != RuntimePlatform.WindowsPlayer || Environment.OSVersion.Version.Major < 10)
            {
                PromptSingle("You must be on Windows 10 or greater for this mod to run.", () => mod.enabled = false, "Ok");
                mod.enabled = false;
                yield break;
            }

            ButtonInfo vc = Buttons.GetIndex("Voice Commands");
            if (vc != null && vc.enabled)
                Prompt("You currently have Voice Commands enabled. Would you like to disable it?", () => vc.enabled = false, () => mod.enabled = false);
            else if (PhraseRecognitionSystem.Status != SpeechSystemStatus.Stopped)
                PromptSingle("You can not use AI Assistant while you have another voice-related mod on.", () => mod.enabled = false, "Ok");

            if (!File.Exists($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt"))
                File.WriteAllLines($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt", keyWords);
            keyWords = File.ReadAllLines($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt");

            VoiceAssistant.PrewarmSounds();

            float captureDeadline = Time.time + 8f;
            while (vc != null && vc.enabled && Time.time < captureDeadline)
                yield return null;

            while (PhraseRecognitionSystem.Status != SpeechSystemStatus.Stopped && Time.time < captureDeadline)
                yield return null;

            if ((vc != null && vc.enabled) || PhraseRecognitionSystem.Status != SpeechSystemStatus.Stopped)
            {
                LogManager.Log("Voice assistant: another voice mod is still holding the microphone, so the wake word listener was not started.");
                PromptSingle("Another voice mod is still using your microphone. Turn AI Assistant back on once it is off.", () => mod.enabled = false, "Ok");
                mod.enabled = false;
                yield break;
            }

            EnsureWakeRecognizer();
            yield break;
        }

        private static void EnsureWakeRecognizer()
        {
            try
            {
                string keywordsPath = $"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt";
                if (!File.Exists(keywordsPath))
                    File.WriteAllLines(keywordsPath, keyWords);

                keyWords = File.ReadAllLines(keywordsPath);

                string[] kw = VoiceAssistant.BuildWakeWords(keyWords);
                if (narratorName == "Mommy ASMR")
                    kw = kw.Concat(new[] { "mommy", "momma" }).ToArray();

                if (krec == null || wakeWords == null || !wakeWords.SequenceEqual(kw))
                {
                    DisposeWakeRecognizer();

                    LogManager.Log("Voice assistant: building the wake word recognizer.");
                    krec = new KeywordRecognizer(kw);
                    krec.OnPhraseRecognized += OnWakeWordRecognized;
                    wakeWords = kw;
                }

                SafePhraseRestart();

                if (!krec.IsRunning)
                    krec.Start();

                if (!krec.IsRunning)
                {
                    LogManager.Log("Voice assistant: the restarted system refused the existing wake word recognizer, rebuilding it.");
                    DisposeWakeRecognizer();

                    krec = new KeywordRecognizer(kw);
                    krec.OnPhraseRecognized += OnWakeWordRecognized;
                    wakeWords = kw;
                    krec.Start();
                }

                LogManager.Log($"Voice assistant: wake word listener armed (system {PhraseRecognitionSystem.Status}, recognizer running {krec.IsRunning}).");
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: could not start the wake word recognizer: {exception.Message}");
            }
        }

        private static void OnWakeWordRecognized(PhraseRecognizedEventArgs args)
        {
            try
            {
                if (dictationStarting || dictationActive)
                    return;

                if (Buttons.GetIndex("AI Assistant")?.enabled != true)
                    return;

                LogManager.Log($"Voice assistant: wake word \"{args.text}\" heard.");
                StartDictationCoroutine(DictationRecognizer());
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: the wake word handler failed: {exception.Message}");
            }
        }

        public static IEnumerator DictationRecognizer()
        {
            ButtonInfo mod = Buttons.GetIndex("AI Assistant");

            yield return null;

            if (mod == null || !mod.enabled || dictationStarting || dictationActive)
                yield break;

            dictationStarting = true;

            LogManager.Log("Voice assistant: listening for a question.");

            SafePhraseShutdown();

            float shutdownDeadline = Time.time + 2f;
            while (PhraseRecognitionSystem.Status != SpeechSystemStatus.Stopped && Time.time < shutdownDeadline)
                yield return null;

            StopDictationRecognizer();

            VoiceAssistant.Greet(false);

            // Say the greeting while the microphone is still closed so the assistant can not hear itself.
            // Chained voice commands skip it, the user is already mid conversation.
            if (VoiceAssistant.greetingEnabled && Buttons.GetIndex("Chain Voice Commands")?.enabled != true && !string.IsNullOrWhiteSpace(VoiceAssistant.Greeting))
            {
                VoiceAssistant.SetState(VoiceAssistant.OrbState.Speaking);
                VoiceAssistant.Say(VoiceAssistant.Greeting, VoiceAssistant.voiceEnabled);

                float greetingDeadline = Time.time + Mathf.Clamp(AIManager.Duration(VoiceAssistant.Greeting) / 1000f, 1f, 4f) + 0.5f;
                while (Time.time < greetingDeadline && mod.enabled)
                    yield return null;

                if (!mod.enabled)
                {
                    dictationStarting = false;
                    VoiceAssistant.Hide();
                    yield break;
                }

                VoiceAssistant.SetState(VoiceAssistant.OrbState.Listening);
            }

            if (debugDictation)
                LogManager.Log("Dictation listening");

            drec = new DictationRecognizer();

            drec.DictationResult += (text, confidence) =>
            {
                try
                {
                    if (!dictationActive)
                        return;

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    // Ignore the burst of buffered audio from the moment the recognizer opened
                    if (Time.time - dictationStartedAt < 0.5f)
                        return;

                    if (debugDictation)
                        LogManager.Log($"Voice assistant: dictation result \"{text}\" (confidence {confidence}).");

                    string heard = text.Trim();
                    string lowered = heard.ToLowerInvariant();

                    if (cancelKeywords.Contains(lowered))
                    {
                        if (dynamicSounds)
                            DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg"), buttonClickVolume / 10f);

                        NotificationManager.SendNotification($"<color=grey>[</color><color=red>AI</color><color=grey>]</color> {(lowered == "i hate you" ? "I hate you too." : "Cancelling...")}", 3000);

                        AIManager.CancelRequest();

                        dictationActive = false;
                        VoiceAssistant.Hide();
                        StartDictationCoroutine(DictationRestart());
                        return;
                    }

                    // Never answer while another reply is still being generated or spoken
                    if (AIManager.generating || AIManager.requestInFlight)
                        return;

                    if (VoiceAssistant.IsWakeWord(heard))
                        return;

                    dictationActive = false;

                    switch (narratorName)
                    {
                        case "Mommy ASMR":
                            NotificationManager.SendNotification($"<color=grey>[</color><color=#ffb6c1>MOMMY</color><color=grey>]</color> Let me get that for you..");
                            break;
                        default:
                            NotificationManager.SendNotification($"<color=grey>[</color><color=blue>AI</color><color=grey>]</color> Generating response..");
                            break;
                    }

                    StartDictationCoroutine(HandOffToAssistant(heard));
                }
                catch (Exception exception)
                {
                    LogManager.LogError($"Voice assistant: the dictation result handler failed: {exception.Message}");
                    dictationActive = false;
                    StartDictationCoroutine(DictationRestart());
                }
            };

            drec.DictationComplete += (completionCause) =>
            {
                try
                {
                    LogManager.Log($"Voice assistant: dictation complete - {completionCause} (state {VoiceAssistant.State}).");

                    // Only react when we are the ones who were listening, a hand off or cancel already owns the state
                    if (!dictationActive)
                        return;

                    dictationActive = false;

                    if (completionCause == DictationCompletionCause.TimeoutExceeded)
                    {
                        LogManager.Log("Voice assistant: no speech heard - restarting the wake word listener.");

                        if (dynamicSounds)
                            DictationPlay(LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg"), buttonClickVolume / 10f);
                    }

                    VoiceAssistant.Hide();
                    StartDictationCoroutine(DictationRestart());
                }
                catch (Exception exception)
                {
                    LogManager.LogError($"Voice assistant: the dictation complete handler failed: {exception.Message}");
                }
            };

            drec.DictationError += (error, hresult) =>
            {
                try
                {
                    LogManager.LogError($"Dictation error: {error}");

                    if (string.IsNullOrEmpty(error) || !error.Contains("Dictation support is not enabled on this device"))
                        return;

                    dictationActive = false;
                    StartDictationCoroutine(DictationOffNextFrame());

                    NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Online Speech Recognition is not enabled on this device. Either open the menu to enable it, or check your internet connection.", 3000);
                    Prompt("Online Speech Recognition is not enabled on your device. Would you like to open the Settings page to enable it?", () =>
                    {
                        try
                        {
                            Process.Start("ms-settings:privacy-speech");
                        }
                        catch (Exception exception)
                        {
                            LogManager.LogError($"Voice assistant: could not open the speech settings page: {exception.Message}");
                        }

                        if (mod != null)
                            PromptSingle("Once you enable Online Speech Recognition, turn this mod back on!", () => mod.enabled = false, "Ok");
                    }, () =>
                    {
                        if (mod != null)
                            PromptSingle("You will not be able to use this mod until you enable Online Speech Recognition.", () => mod.enabled = false, "Ok");
                    });
                }
                catch (Exception exception)
                {
                    LogManager.LogError($"Voice assistant: the dictation error handler failed: {exception.Message}");
                }
            };

            drec.DictationHypothesis += (text) =>
            {
                try
                {
                    if (!dictationActive || AIManager.generating || AIManager.requestInFlight)
                        return;

                    if (debugDictation)
                        LogManager.Log($"Voice assistant: dictation hypothesis \"{text ?? "(null)"}\".");

                    NotificationManager.ClearAllNotifications();

                    if (!string.IsNullOrWhiteSpace(text))
                        NotificationManager.SendNotification($"<color=grey>[</color><color=green>VOICE</color><color=grey>]</color> {text}");

                    VoiceAssistant.ShowHeard(text);
                }
                catch (Exception exception)
                {
                    LogManager.LogError($"Voice assistant: the dictation hypothesis handler failed: {exception.Message}");
                }
            };

            try
            {
                drec.Start();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: dictation start exception: {exception.Message}");
                dictationStarting = false;
                StopDictationRecognizer();
                VoiceAssistant.Hide();
                StartDictationCoroutine(DictationRestart());
                yield break;
            }

            float dictationDeadline = Time.time + 2f;
            while (drec != null && drec.Status == SpeechSystemStatus.Stopped && Time.time < dictationDeadline)
                yield return null;

            string status = drec?.Status.ToString() ?? "none";
            LogManager.Log($"Voice assistant: dictation listening (status {status}).");

            if (drec == null || drec.Status != SpeechSystemStatus.Running)
            {
                LogManager.LogError($"Voice assistant: dictation did not start (status {status}) - check Windows Settings > Privacy & security > Speech > Online speech recognition = On AND Privacy & security > Microphone > Let apps access your microphone = On (and Gorilla Tag allowed).");
                NotificationManager.SendNotification("<color=grey>[</color><color=red>VOICE</color><color=grey>]</color> Dictation failed to start. In Windows: Speech > Online = On AND Microphone > App access = On for Gorilla Tag.", 7000);

                dictationStarting = false;
                StopDictationRecognizer();
                VoiceAssistant.Hide();
                StartDictationCoroutine(DictationRestart());
                yield break;
            }

            dictationStarting = false;
            dictationActive = true;
            dictationStartedAt = Time.time;

            yield break;
        }

        private static void StartDictationCoroutine(IEnumerator routine)
        {
            if (routine == null || CoroutineManager.instance == null)
                return;

            try
            {
                CoroutineManager.instance.StartCoroutine(routine);
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: could not start the voice coroutine: {exception.Message}");
            }
        }

        /// <summary>
        /// Stops the shared speech system, but only while it is actually running. Shutting it down
        /// when it is already stopped can take the game down with it.
        /// </summary>
        private static void SafePhraseShutdown()
        {
            try
            {
                if (PhraseRecognitionSystem.Status == SpeechSystemStatus.Stopped)
                    return;

                PhraseRecognitionSystem.Shutdown();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: could not shut down phrase recognition: {exception.Message}");
            }
        }

        private static void SafePhraseRestart()
        {
            try
            {
                if (PhraseRecognitionSystem.Status == SpeechSystemStatus.Stopped)
                    PhraseRecognitionSystem.Restart();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Voice assistant: could not restart phrase recognition: {exception.Message}");
            }
        }

        /// <summary>
        /// Hands a heard question over to the assistant. Deferred by a frame so the native speech
        /// callback has returned before the recognizer is stopped.
        /// </summary>
        private static IEnumerator HandOffToAssistant(string question)
        {
            yield return null;

            StopDictationRecognizer();
            yield return null;

            StartDictationCoroutine(AIManager.AskAI(question));
        }

        /// <summary>
        /// Goes back to the wake word listener, or straight back to listening when voice commands are chained.
        /// </summary>
        public static void ResumeListening()
        {
            if (Buttons.GetIndex("AI Assistant")?.enabled != true)
                return;

            dictationActive = false;
            dictationStarting = false;

            if (Buttons.GetIndex("Chain Voice Commands")?.enabled == true)
                StartDictationCoroutine(DictationRecognizer());
            else
                StartDictationCoroutine(DictationRestart());
        }

        /// <summary>
        /// Opens the microphone without waiting for the wake word. Used by Wake Assistant.
        /// </summary>
        public static void StartListening()
        {
            dictationActive = false;
            dictationStarting = false;
            StartDictationCoroutine(DictationRecognizer());
        }

        public static IEnumerator DictationRestart()
        {
            yield return null;

            dictationActive = false;
            dictationStarting = false;

            StopDictationRecognizer();
            VoiceAssistant.HideIfListening();

            if (Buttons.GetIndex("AI Assistant")?.enabled != true)
                yield break;

            LogManager.Log("Voice assistant: restarting the wake word listener.");
            EnsureWakeRecognizer();
            yield break;
        }

        public static void StopDictation()
        {
            dictationActive = false;
            dictationStarting = false;

            StopDictationRecognizer();
            StartDictationCoroutine(DictationRestart());
        }

        public static void DictationOff()
        {
            dictationActive = false;
            dictationStarting = false;

            AIManager.CancelRequest();

            StopDictationRecognizer();
            DisposeWakeRecognizer();
            SafePhraseShutdown();

            VoiceAssistant.HideIfListening();
        }

        private static void StopDictationRecognizer()
        {
            DictationRecognizer recognizer = drec;
            drec = null;

            if (recognizer == null)
                return;

            try
            {
                if (recognizer.Status == SpeechSystemStatus.Running)
                    recognizer.Stop();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Failed to stop dictation: {exception.Message}");
            }

            try
            {
                recognizer.Dispose();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Failed to release dictation: {exception.Message}");
            }
        }

        private static void DisposeWakeRecognizer()
        {
            KeywordRecognizer wakeRecognizer = krec;
            krec = null;
            wakeWords = null;

            if (wakeRecognizer == null)
                return;

            try
            {
                if (wakeRecognizer.IsRunning)
                    wakeRecognizer.Stop();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Failed to stop the wake word recognizer: {exception.Message}");
            }

            try
            {
                wakeRecognizer.Dispose();
            }
            catch (Exception exception)
            {
                LogManager.LogError($"Failed to release the wake word recognizer: {exception.Message}");
            }
        }

        private static IEnumerator DictationOffNextFrame()
        {
            yield return null;
            DictationOff();
        }

        public static void DictationPlay(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            bool enabled = Buttons.GetIndex("Global Dynamic Sounds")?.enabled == true;
            switch (enabled)
            {
                case true:
                    Sound.PlayAudio(clip);
                    break;
                case false:
                    Play2DAudio(clip, volume);
                    break;
            }
        }

        private static LineRenderer clickGuiLine;
        private static bool lastTriggerClick;

        private static EventSystem eventSystem;
        private static PointerEventData pointerData;
        private static readonly List<RaycastResult> uiResults = new List<RaycastResult>();
        private static GameObject currentUI;

        private static GameObject pressedUI;
        private static GameObject draggedUI;
        private static Vector2 lastPointerPos;
        private static Canvas canvas;

        private static bool isDragging;

        public static void ReloadOnCategoryChange()
        {
            if (clickGUI && menu != null)
                ReloadMenu();
        }

        public static void EnableClickGUI()
        {
            clickGUI = true;
            Buttons.OnCategoryChanged -= ReloadOnCategoryChange;
            Buttons.OnCategoryChanged += ReloadOnCategoryChange;
            ReloadMenu();
        }

        public static void DisableClickGUI()
        {
            clickGUI = false;
            Buttons.OnCategoryChanged -= ReloadOnCategoryChange;

            if (clickGuiLine != null)
            {
                Object.Destroy(clickGuiLine.gameObject);
                clickGuiLine = null;
            }
        }

        public static void InitializeClickGUI()
        {
            canvas = menu.transform.Find("Canvas").GetComponent<Canvas>();

            if (!XRSettings.isDeviceActive)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1;

                canvas.gameObject.transform.Find("Main").AddComponent<UIDragWindow>();
            }

            Transform canvasTransform = canvas.gameObject.transform;
            canvasTransform.Find("Main").AddComponent<UIColorChanger>().colors = backgroundColor;

            ExtGradient sidebarColor = buttonColors[1].Clone();
            for (int i = 0; i < sidebarColor.colors.Length; i++)
            {
                GradientColorKey colorKey = sidebarColor.colors[i];
                sidebarColor.colors[i] = new GradientColorKey { time = colorKey.time, color = DarkenColor(colorKey.color, 0.35f) };
            }

            canvasTransform.Find("Main/Sidebar").AddComponent<UIColorChanger>().colors = sidebarColor;
            canvasTransform.Find("Main/Separator").AddComponent<UIColorChanger>().colors = buttonColors[1];

            canvasTransform.Find("Main/Sidebar/Watermark").localRotation = Quaternion.Euler(0f, 0f, rockWatermark ? Mathf.Sin(Time.time * 2f) * 10f : 0f);

            List<MaskableGraphic> toRecolor = new List<MaskableGraphic>();
            foreach (string partName in new[]
            {
                "Main/Sidebar/Title",
                "Main/Sidebar/Watermark",
                "Main/Sidebar/Settings",
                "Main/Sidebar/Players",
                "Main/Sidebar/Scroll View/Scrollbar Vertical/Sliding Area/Handle"
            })
                toRecolor.Add(canvasTransform.Find(partName).GetComponent<MaskableGraphic>());

            Transform sidebarTransform = canvasTransform.Find("Main/Sidebar");
            sidebarTransform.Find("Friends").gameObject.SetActive(false);

            foreach (string buttonName in new[]
            {
                "Settings", "Players"
            })
                sidebarTransform.Find(buttonName).GetComponent<Button>().onClick.AddListener(() =>
                {
                    Toggle(buttonName);
                    PlayButtonSound();
                });

            var selection = canvasTransform.Find("Main/Sidebar/Scroll View/Viewport/Content/Home/Selection");
            selection.AddComponent<UIColorChanger>().colors = buttonColors[1];

            bool movedSelection = false;

            string[] ignoreButtons = {
                "Join Discord",
                "Settings",
                "Friends",
                "Players",
                "Favorite Mods",
                "Enabled Mods",
                "Room Mods",
                "Important Mods",
                "Safety Mods",
                "Movement Mods",
                "Advantage Mods",
                "Visual Mods",
                "Fun Mods",
                "Sound Mods",
                "Projectile Mods",
                "Master Mods",
                "Overpowered Mods",
                "Experimental Mods",
                "Detected Mods",
                "Achievements",
                "Credits"
            };

            GameObject otherBase = canvasTransform.Find("Main/Sidebar/Scroll View/Viewport/Content/Other").gameObject;
            foreach (ButtonInfo button in Buttons.buttons[Buttons.GetCategory("Main")])
            {
                if (!ignoreButtons.Contains(button.buttonText))
                {
                    GameObject otherButton = Object.Instantiate(otherBase, canvasTransform.Find("Main/Sidebar/Scroll View/Viewport/Content"), false);
                    otherButton.SetActive(true);
                    otherButton.name = button.buttonText;
                    otherButton.transform.Find("Title").GetComponent<TextMeshProUGUI>().SafeSetText(button.buttonText);
                }
            }

            foreach (GameObject tab in canvasTransform.Find("Main/Sidebar/Scroll View/Viewport/Content").Children())
            {
                if (!tab.activeSelf)
                    continue;

                toRecolor.Add(tab.transform.Find("Title").GetComponent<MaskableGraphic>());
                toRecolor.Add(tab.transform.Find("Image").GetComponent<MaskableGraphic>());

                tab.AddComponent<UIColorChanger>().colors = buttonColors[0];
                tab.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Toggle(Buttons.buttons[Buttons.GetCategory("Main")].Where(button => button.buttonText.StartsWith(tab.name)).FirstOrDefault() ?? Buttons.GetIndex("Exit Settings"));
                    PlayButtonSound();
                });

                if (Buttons.CurrentCategoryName.StartsWith(tab.name == "Home" ? "Main" : tab.name))
                {
                    movedSelection = true;
                    selection.SetParent(tab.transform, false);
                }
                else
                {
                    tab.transform.Find("Title").GetComponent<RectTransform>().localPosition += Vector3.left * 10f;
                    tab.transform.Find("Image").GetComponent<RectTransform>().localPosition += Vector3.left * 10f;
                }
            }

            if (!movedSelection)
                selection.gameObject.SetActive(false);

            GameObject buttonTemplate = canvasTransform.Find("Main/Button").gameObject;
            void AddButton(Transform parent, ButtonInfo info)
            {
                static void UpdateButton(GameObject button, ButtonInfo info)
                {
                    Transform transform = button.transform;
                    string buttonText = info.overlapText ?? info.buttonText;

                    if (info.detected)
                        buttonText = $"<color=red>{buttonText}</color>";

                    if (inputTextColor != "green")
                        buttonText = buttonText.Replace(" <color=grey>[</color><color=green>", $" <color=grey>[</color><color={inputTextColor}>");

                    buttonText = FixTMProTags(buttonText);
                    buttonText = FollowMenuSettings(buttonText);

                    transform.Find("Title").GetComponent<TextMeshProUGUI>().SafeSetText(buttonText);
                    transform.Find("Title").GetComponent<TextMeshProUGUI>().spriteAsset = ButtonSpriteSheet;

                    string toolTipText = info.toolTip;

                    if (inputTextColor != "green")
                        toolTipText = toolTipText.Replace("<color=green>", $"<color={inputTextColor}>");

                    toolTipText = FixTMProTags(toolTipText);
                    toolTipText = FollowMenuSettings(toolTipText);

                    transform.Find("ToolTip").GetComponent<TextMeshProUGUI>().SafeSetText(toolTipText);

                    transform.Find("Title").GetComponent<TextMeshProUGUI>().Chams();
                    transform.Find("ToolTip").GetComponent<TextMeshProUGUI>().Chams();

                    button.name = buttonText;

                    if (info.label)
                    {
                        RectTransform title = transform.Find("Title").gameObject.GetComponent<RectTransform>();
                        title.anchorMin = new Vector2(0.5f, 0.5f);
                        title.anchorMax = new Vector2(0.5f, 0.5f);

                        title.localPosition = new Vector3(0f, 0f, 0f);

                        transform.Find("ToolTip").gameObject.SetActive(false);
                        transform.Find("Toggle").gameObject.SetActive(false);
                        transform.Find("Increment").gameObject.SetActive(false);
                        transform.Find("Decrement").gameObject.SetActive(false);
                    }
                    else if (info.incremental)
                    {
                        transform.Find("Increment").gameObject.SetActive(true);
                        transform.Find("Decrement").gameObject.SetActive(true);

                        transform.Find("Increment").gameObject.GetOrAddComponent<UIColorChanger>().colors = buttonColors[0];
                        transform.Find("Decrement").gameObject.GetOrAddComponent<UIColorChanger>().colors = buttonColors[0];

                        transform.Find("Increment/Image").gameObject.GetOrAddComponent<UIColorChanger>().colors = textColors[1];
                        transform.Find("Decrement/Image").gameObject.GetOrAddComponent<UIColorChanger>().colors = textColors[1];
                    }
                    else
                    {
                        transform.Find("Toggle").gameObject.SetActive(true);
                        transform.Find("Toggle/Image").gameObject.SetActive(info.enabled);

                        transform.Find("Toggle").gameObject.GetOrAddComponent<UIColorChanger>().colors = info.enabled ? buttonColors[1] : buttonColors[0];
                        transform.Find("Toggle/Image").gameObject.GetOrAddComponent<UIColorChanger>().colors = info.enabled ? textColors[2] : textColors[1];
                    }

                    transform.Find("Title").AddComponent<UIColorChanger>().colors = textColors[1];
                    transform.Find("ToolTip").AddComponent<UIColorChanger>().colors = textColors[1];
                }

                GameObject button = Object.Instantiate(buttonTemplate, parent, false);
                button.SetActive(true);

                ExtGradient buttonBackgroundColor = backgroundColor.Clone();
                for (int i = 0; i < buttonBackgroundColor.colors.Length; i++)
                {
                    GradientColorKey colorKey = buttonBackgroundColor.colors[i];
                    buttonBackgroundColor.colors[i] = new GradientColorKey { time = colorKey.time, color = DarkenColor(colorKey.color, 0.75f) };
                }
                button.AddComponent<UIColorChanger>().colors = buttonBackgroundColor;

                UpdateButton(button, info);

                Transform transform = button.transform;
                if (info.incremental)
                {
                    transform.Find("Increment").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        ToggleIncremental(info.buttonText, true);
                        PlayButtonSound();
                        UpdateButton(button, info);
                    });
                    transform.Find("Decrement").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        ToggleIncremental(info.buttonText, false);
                        PlayButtonSound();
                        UpdateButton(button, info);
                    });
                }
                else
                {
                    transform.Find("Toggle").GetComponent<Button>().onClick.AddListener(() =>
                    {
                        Toggle(info);
                        PlayButtonSound();
                        UpdateButton(button, info);
                    });
                }
            }

            if (CurrentPrompt != null)
            {
                canvasTransform.Find("Main/PromptTab").gameObject.SetActive(true);

                foreach (string partName in new[]
                    {
                        "Main/PromptTab/Title",
                        "Main/PromptTab/Accept/Text",
                        "Main/PromptTab/Decline/Text"
                    })
                    toRecolor.Add(canvasTransform.Find(partName).GetComponent<MaskableGraphic>());

                GameObject title = canvasTransform.Find("Main/PromptTab/Title").gameObject;
                title.GetComponent<TextMeshProUGUI>().SafeSetText(CurrentPrompt.Message);

                GameObject accept = canvasTransform.Find("Main/PromptTab/Accept").gameObject;
                accept.transform.Find("Text").GetComponent<TextMeshProUGUI>().SafeSetText(CurrentPrompt.AcceptText);
                accept.transform.Find("Text").GetComponent<TextMeshProUGUI>().Chams();
                accept.GetOrAddComponent<UIColorChanger>().colors = buttonColors[0];
                accept.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Toggle("Accept Prompt");
                    PlayButtonSound();
                    ReloadMenu();
                });

                if (CurrentPrompt.DeclineText != null)
                {
                    GameObject decline = canvasTransform.Find("Main/PromptTab/Decline").gameObject;
                    decline.transform.Find("Text").GetComponent<TextMeshProUGUI>().SafeSetText(CurrentPrompt.DeclineText);
                    decline.GetOrAddComponent<UIColorChanger>().colors = buttonColors[0];
                    decline.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        Toggle("Decline Prompt");
                        PlayButtonSound();
                        ReloadMenu();
                    });
                }
                else
                {
                    canvasTransform.Find("Main/PromptTab/Decline").gameObject.SetActive(false);

                    RectTransform rectTransform = accept.GetComponent<RectTransform>();
                    rectTransform.localPosition = new Vector3(title.GetComponent<RectTransform>().localPosition.x, rectTransform.localPosition.y, rectTransform.localPosition.z);
                    rectTransform.localScale = new Vector3(rectTransform.localScale.y * 2.05f, rectTransform.localScale.y, rectTransform.localScale.z);

                    accept.transform.Find("Text").GetComponent<RectTransform>().localScale = new Vector3(rectTransform.localScale.y / 2.05f, rectTransform.localScale.y, rectTransform.localScale.z);
                }
            }
            else if (Buttons.CurrentCategoryIndex == 0)
            {
                canvasTransform.Find("Main/HomeTab").gameObject.SetActive(true);
                canvasTransform.Find("Main/HomeTab/Title").GetComponent<TextMeshProUGUI>().SafeSetText($"Hey, {PhotonNetwork.NickName ?? "null"}!");

                if (Buttons.CurrentCategoryIndex == 0)
                {
                    foreach (string partName in new[]
                    {
                        "Main/HomeTab/Title",
                        "Main/HomeTab/EnabledTitle",
                        "Main/HomeTab/FavoritesTitle",
                        "Main/HomeTab/EnabledIcon",
                        "Main/HomeTab/FavoritesIcon",
                        "Main/HomeTab/Enabled/Viewport/Content/None",
                        "Main/HomeTab/Favorites/Viewport/Content/None",
                        "Main/HomeTab/Enabled/Scrollbar Vertical/Sliding Area/Handle",
                        "Main/HomeTab/Favorites/Scrollbar Vertical/Sliding Area/Handle"
                    })
                        toRecolor.Add(canvasTransform.Find(partName).GetComponent<MaskableGraphic>());
                }

                Transform enabledModsTransform = canvasTransform.Find("Main/HomeTab/Enabled/Viewport/Content");

                List<ButtonInfo> enabledMods = new List<ButtonInfo>();
                int categoryIndex = 0;
                foreach (ButtonInfo[] buttonList in Buttons.buttons)
                {
                    enabledMods.AddRange(buttonList.Where(v => v.enabled && !v.hideFromArraylist && (!hideSettings || !Buttons.categoryNames[categoryIndex].Contains("Settings")) && (!hideMacros || !Buttons.categoryNames[categoryIndex].Contains("Macro"))));
                    categoryIndex++;
                }
                enabledMods = enabledMods.OrderBy(v => v.overlapText ?? v.buttonText).ToList();

                if (enabledMods.Count > 0)
                {
                    canvasTransform.Find("Main/HomeTab/Enabled/Viewport/Content/None").gameObject.SetActive(false);
                    foreach (ButtonInfo info in enabledMods)
                        AddButton(enabledModsTransform, info);
                }

                Transform favoritedModsTransform = canvasTransform.Find("Main/HomeTab/Favorites/Viewport/Content");
                List<ButtonInfo> favoriteMods = StringsToInfos(favorites.ToArray()).ToList();

                if (favoriteMods.Count > 0)
                    favoriteMods.RemoveAt(0);

                if (favoriteMods.Count > 0)
                {
                    canvasTransform.Find("Main/HomeTab/Favorites/Viewport/Content/None").gameObject.SetActive(false);
                    foreach (ButtonInfo info in favoriteMods)
                        AddButton(favoritedModsTransform, info);
                }
            }
            else
            {
                canvasTransform.Find("Main/ModuleTab").gameObject.SetActive(true);

                foreach (string partName in new[]
                    {
                        "Main/ModuleTab/Search/SearchIcon",
                        "Main/ModuleTab/Search/Text Area/Placeholder",
                        "Main/ModuleTab/Search/Text Area/Text",
                        "Main/ModuleTab/Modules/Scrollbar Vertical/Sliding Area/Handle"
                    })
                    toRecolor.Add(canvasTransform.Find(partName).GetComponent<MaskableGraphic>());

                List<ButtonInfo> buttons = Buttons.buttons[Buttons.CurrentCategoryIndex].ToList();

                if (buttons.Count > 0 && ignoreButtons.Contains(Buttons.CurrentCategoryName))
                    buttons.RemoveAt(0);

                if (buttons.Count > 0)
                {
                    Transform modulesTransform = canvasTransform.Find("Main/ModuleTab/Modules/Viewport/Content");
                    foreach (ButtonInfo button in buttons)
                        AddButton(modulesTransform, button);
                }

                Transform searchBar = canvasTransform.Find("Main/ModuleTab/Search");
                TMP_InputField inputField = searchBar.GetComponent<TMP_InputField>();

                inputField.onSelect.AddListener(_ =>
                {
                    if (!isSearching)
                        Search();
                });

                inputField.onDeselect.AddListener(_ =>
                {
                    if (isSearching && keyboardInput.IsNullOrEmpty())
                        Search();
                });
            }

            for (int i = 0; i < toRecolor.Count; i++)
            {
                MaskableGraphic graphic = toRecolor[i];
                graphic.gameObject.AddComponent<UIColorChanger>().colors = textColors[i <= 1 ? 0 : 1];

                if (graphic is TMP_Text text)
                    text.Chams();
            }

            ExtGradient buttonBackgroundColor = backgroundColor.Clone();
            for (int i = 0; i < buttonBackgroundColor.colors.Length; i++)
            {
                GradientColorKey colorKey = buttonBackgroundColor.colors[i];
                buttonBackgroundColor.colors[i] = new GradientColorKey { time = colorKey.time, color = DarkenColor(colorKey.color, 0.75f) };
            }
            canvasTransform.Find("Main/ModuleTab/Search").AddComponent<UIColorChanger>().colors = buttonBackgroundColor;

            Canvas.ForceUpdateCanvases();
        }

        public static void UpdateSearch()
        {
            Transform searchBar = canvas.transform.Find("Main/ModuleTab/Search");
            TMP_InputField inputField = searchBar.GetComponent<TMP_InputField>();

            inputField.text = keyboardInput;
            foreach (GameObject button in canvas.transform.Find("Main/ModuleTab/Modules/Viewport/Content").Children())
                button.SetActive(keyboardInput.IsNullOrEmpty() || button.name.ClearTags().Replace(" ", "").ToLower().Contains(keyboardInput.Replace(" ", "").ToLower()));
        }

        public static void ClickGUI()
        {
            if (menu == null)
            {
                if (clickGuiLine != null)
                {
                    Object.Destroy(clickGuiLine.gameObject);
                    clickGuiLine = null;
                }
            }
            else
            {
                canvas.transform.Find("Main/Sidebar/Watermark").localRotation = Quaternion.Euler(0f, 0f, rockWatermark ? Mathf.Sin(Time.time * 2f) * 10f : 0f);

                if (isSearching && Buttons.CurrentCategoryIndex != 0)
                {
                    Transform searchBar = canvas.transform.Find("Main/ModuleTab/Search");
                    TMP_InputField inputField = searchBar.GetComponent<TMP_InputField>();

                    if (inputField.text != keyboardInput)
                        UpdateSearch();
                }

                // This used to bail out here when no VR device was active, which made the
                // non VR branch a few lines below unreachable: the mouse raycast that
                // branch performs could never run, so nothing on the UI could be pointed
                // at or selected. The branch already handles both cases, so let it.

                if (clickGuiLine == null)
                {
                    clickGuiLine = new GameObject("iiMenu_ClickGUILine")
                        .GetOrAddComponent<LineRenderer>();

                    clickGuiLine.material = new Material(Shader.Find("GUI/Text Shader"));
                    clickGuiLine.startWidth = 0.025f * (scaleWithPlayer ? GTPlayer.Instance.scale : 1f);
                    clickGuiLine.endWidth = clickGuiLine.startWidth;
                    clickGuiLine.useWorldSpace = true;
                    clickGuiLine.positionCount = 2;

                    if (smoothLines)
                    {
                        clickGuiLine.numCapVertices = 10;
                        clickGuiLine.numCornerVertices = 5;
                    }
                }

                clickGuiLine.startColor = backgroundColor.GetCurrentColor();
                clickGuiLine.endColor = backgroundColor.GetCurrentColor(0.5f);

                var uiRaycaster = canvas.GetComponent<GraphicRaycaster>();
                eventSystem ??= EventSystem.current;

                pointerData ??= new PointerEventData(eventSystem);

                bool useLeft = rightHand || (bothHands && ControllerInputPoller.instance.rightControllerSecondaryButton);

                if (!XRSettings.isDeviceActive)
                {
                    pointerData.position = Mouse.current?.position.ReadValue() ?? Vector2.zero;

                    uiResults.Clear();
                    uiRaycaster.Raycast(pointerData, uiResults);

                    if (UI.prefabRaycaster != null)
                        UI.prefabRaycaster.Raycast(pointerData, uiResults);

                    currentUI = uiResults.Count > 0 ? uiResults[0].gameObject : null;

                    if (clickGuiLine != null)
                        clickGuiLine.gameObject.SetActive(false);
                }
                else
                {
                    var (_, _, _, forward, _) = useLeft
                        ? ControllerUtilities.GetTrueLeftHand()
                        : ControllerUtilities.GetTrueRightHand();

                    Vector3 startPos = useLeft
                        ? GorillaTagger.Instance.leftHandTransform.position
                        : GorillaTagger.Instance.rightHandTransform.position;

                    Vector3 direction = forward.normalized;

                    Vector3 screenPoint = iiMenu.Menu.Main.MainCam.WorldToScreenPoint(startPos + direction * 5f);
                    pointerData.position = screenPoint;

                    uiResults.Clear();
                    uiRaycaster.Raycast(pointerData, uiResults);

                    currentUI = uiResults.Count > 0 ? uiResults[0].gameObject : null;

                    Vector3 endPos = currentUI != null
                        ? uiResults[0].worldPosition
                        : startPos + direction * 5f;

                    clickGuiLine.SetPosition(0, startPos);
                    clickGuiLine.SetPosition(1, endPos);

                    if (!clickGuiLine.gameObject.activeSelf)
                        clickGuiLine.gameObject.SetActive(true);
                }

                // Everything below drives press, release and click off this one flag, and it
                // used to be the controller trigger only. With no VR device there is no
                // trigger, so nothing could ever be clicked, including the search field
                // and the on screen keyboard. Use the left mouse button in that case.
                bool trigger = !XRSettings.isDeviceActive
                    ? Mouse.current != null && Mouse.current.leftButton.isPressed
                    : useLeft ? leftTrigger > 0.5f : rightTrigger > 0.5f;
                Vector2 currentPos = pointerData.position;
                pointerData.delta = currentPos - lastPointerPos;
                lastPointerPos = currentPos;

                if (trigger && !lastTriggerClick && currentUI != null)
                {
                    GameObject targetUI = null;
                    TMP_InputField targetField = null;
                    foreach (var result in uiResults)
                    {
                        var button = result.gameObject.GetComponent<Button>();
                        var toggle = result.gameObject.GetComponent<Toggle>();
                        var slider = result.gameObject.GetComponent<Slider>();
                        var inputField = result.gameObject.GetComponent<TMP_InputField>();

                        if (button != null || toggle != null || slider != null || inputField != null)
                        {
                            targetUI = result.gameObject;
                            targetField = inputField;
                            break;
                        }
                    }

                    pressedUI = targetUI ?? currentUI;
                    pointerData.pressPosition = currentPos;
                    pointerData.pointerPressRaycast = uiResults[0];

                    ExecuteEvents.Execute(pressedUI, pointerData, ExecuteEvents.pointerDownHandler);
                    pointerData.pointerPress = pressedUI;

                    if (targetField != null)
                        UI.FocusControlField(targetField);

                    isDragging = false;
                    draggedUI = ExecuteEvents.GetEventHandler<IDragHandler>(currentUI);
                    pointerData.pointerDrag = draggedUI ?? null;
                }

                switch (trigger)
                {
                    case true when draggedUI != null:
                        {
                            if (!isDragging)
                            {
                                if (Vector2.Distance(pointerData.pressPosition, currentPos) > 15f)
                                {
                                    isDragging = true;
                                    ExecuteEvents.Execute(draggedUI, pointerData, ExecuteEvents.beginDragHandler);

                                    if (pressedUI != null && pressedUI != draggedUI)
                                    {
                                        ExecuteEvents.Execute(pressedUI, pointerData, ExecuteEvents.pointerUpHandler);
                                        pointerData.pointerPress = null;
                                    }
                                }
                            }

                            if (isDragging)
                                ExecuteEvents.Execute(draggedUI, pointerData, ExecuteEvents.dragHandler);
                            break;
                        }
                    case false when lastTriggerClick:
                        {
                            if (pressedUI != null && !isDragging)
                            {
                                ExecuteEvents.Execute(pressedUI, pointerData, ExecuteEvents.pointerUpHandler);
                                ExecuteEvents.Execute(pressedUI, pointerData, ExecuteEvents.pointerClickHandler);
                            }
                            else if (pressedUI != null)
                                ExecuteEvents.Execute(pressedUI, pointerData, ExecuteEvents.pointerUpHandler);

                            if (isDragging && draggedUI != null)
                                ExecuteEvents.Execute(draggedUI, pointerData, ExecuteEvents.endDragHandler);

                            pressedUI = null;
                            draggedUI = null;
                            pointerData.pointerDrag = null;
                            pointerData.pointerPress = null;
                            isDragging = false;
                            break;
                        }
                }

                lastTriggerClick = trigger;
            }
        }

        public static GameObject selectObject;
        public static VRRig lastTarget;
        public static bool lastTriggerSelect;
        public static void PlayerSelect()
        {
            bool leftHand = rightHand || (bothHands && ControllerInputPoller.instance.rightControllerSecondaryButton);

            var (_, _, _, forward, _) = leftHand ? ControllerUtilities.GetTrueLeftHand() : ControllerUtilities.GetTrueRightHand();
            bool canSelect = NetworkSystem.Instance.InRoom && menu != null && reference != null && Vector3.Distance(menu.transform.position, reference.transform.position) > 0.5f;

            if (canSelect)
            {
                if (selectObject == null)
                    selectObject = new GameObject("iiMenu_PingLine");

                Color targetColor = Buttons.GetIndex("Swap GUI Colors").enabled ? buttonColors[1].GetCurrentColor() : backgroundColor.GetCurrentColor();
                Color lineColor = targetColor;
                lineColor.a = 0.15f;

                LineRenderer pingLine = selectObject.GetOrAddComponent<LineRenderer>();
                pingLine.material.shader = Shader.Find("GUI/Text Shader");
                pingLine.startColor = lineColor;
                pingLine.endColor = lineColor;
                pingLine.startWidth = 0.025f * (scaleWithPlayer ? GTPlayer.Instance.scale : 1f);
                pingLine.endWidth = 0.025f * (scaleWithPlayer ? GTPlayer.Instance.scale : 1f);
                pingLine.positionCount = 2;
                pingLine.useWorldSpace = true;
                if (smoothLines)
                {
                    pingLine.numCapVertices = 10;
                    pingLine.numCornerVertices = 5;
                }

                Vector3 StartPosition = leftHand ? GorillaTagger.Instance.leftHandTransform.position : GorillaTagger.Instance.rightHandTransform.position;
                Vector3 Direction = forward;

                Physics.SphereCast(StartPosition + Direction / 4f * (scaleWithPlayer ? GTPlayer.Instance.scale : 1f), 0.15f, Direction, out var Ray, 512f, NoInvisLayerMask());
                Vector3 EndPosition = Ray.point == Vector3.zero ? StartPosition + (Direction * 512f) : Ray.point;

                pingLine.SetPosition(0, StartPosition);
                pingLine.SetPosition(1, EndPosition);

                VRRig rigTarget = iiMenu.Utilities.RigUtilities.GetRigFromHit(Ray);
                if (Ray.collider != null && rigTarget != null && !rigTarget.IsLocal())
                {
                    if (lastTarget != null && lastTarget != rigTarget)
                    {
                        lastTarget.mainSkin.material.shader = Shader.Find("GorillaTag/UberShader");
                        if (lastTarget.mainSkin.material.name.Contains("gorilla_body"))
                            lastTarget.mainSkin.material.color = lastTarget.playerColor;

                        lastTarget = null;
                    }

                    if (lastTarget == null)
                    {
                        Visuals.FixRigMaterialESPColors(rigTarget);

                        rigTarget.mainSkin.material.shader = Shader.Find("GUI/Text Shader");
                        rigTarget.mainSkin.material.color = targetColor;

                        GorillaTagger.Instance.StartVibration(leftHand, GorillaTagger.Instance.tagHapticStrength / 2f, 0.05f);

                        lastTarget = rigTarget;
                    }
                    else
                        lastTarget.mainSkin.material.color = targetColor;

                    bool trigger = leftHand ? leftTrigger > 0.5f : rightTrigger > 0.5f;

                    if (trigger && !lastTriggerSelect)
                    {
                        VRRig.LocalRig.PlayHandTapLocal(50, leftHand, 0.4f);
                        GorillaTagger.Instance.StartVibration(leftHand, GorillaTagger.Instance.tagHapticStrength / 2f, GorillaTagger.Instance.tagHapticDuration / 2f);

                        NavigatePlayer(GetPlayerFromVRRig(rigTarget));
                        ReloadMenu();

                        NotificationManager.SendNotification($"<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Selected player {GetPlayerName(rigTarget)}.");
                    }

                    lastTriggerSelect = trigger;
                }
                else
                {
                    if (lastTarget != null)
                    {
                        lastTarget.mainSkin.material.shader = Shader.Find("GorillaTag/UberShader");
                        if (lastTarget.mainSkin.material.name.Contains("gorilla_body"))
                            lastTarget.mainSkin.material.color = lastTarget.playerColor;

                        lastTarget = null;
                    }
                }
            }
            else
            {
                if (selectObject != null)
                {
                    Object.Destroy(selectObject);
                    selectObject = null;
                }

                if (lastTarget != null)
                {
                    lastTarget.mainSkin.material.shader = Shader.Find("GorillaTag/UberShader");
                    if (lastTarget.mainSkin.material.name.Contains("gorilla_body"))
                        lastTarget.mainSkin.material.color = lastTarget.playerColor;

                    lastTarget = null;
                }

                lastTriggerSelect = false;
            }
        }

        public static IEnumerator MenuIntroCoroutine()
        {
            if (Time.time < timeMenuStarted)
                yield return new WaitForSeconds(1f);

            float fps = 1f / Time.unscaledDeltaTime;
            yield return new WaitUntil(() => { fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.1f); return fps > 30f; });

            GameObject menuIntro = LoadObject<GameObject>("Intro");

            menuIntro.transform.position = GorillaTagger.Instance.bodyCollider.transform.position;
            menuIntro.transform.rotation = GorillaTagger.Instance.bodyCollider.transform.rotation;

            VideoPlayer videoPlayer = menuIntro.transform.Find("Video").GetComponent<VideoPlayer>();
            ParticleSystem particleSystem = menuIntro.transform.Find("Particles").GetComponent<ParticleSystem>();

            Color backgroundColor = Color.white;
            Fun.HueShift(Color.white);

            var main = particleSystem.main; // ????
            main.startColor = new ParticleSystem.MinMaxGradient(
                Main.backgroundColor.GetColor(0)
            );

            void EndImmediately()
            {
                Fun.HueShift(Color.clear);
                Object.Destroy(menuIntro);
            }

            float timeout = 0f;

            while (!videoPlayer.isPrepared)
            {
                timeout += Time.deltaTime;
                if (timeout > 5f)
                {
                    EndImmediately();
                    yield break;
                }
                yield return null;
            }

            bool videoEnded = false;
            videoPlayer.Play();
            videoPlayer.loopPointReached += (_) => videoEnded = true;

            yield return new WaitUntil(() => videoEnded);

            float fadeEnd = Time.time + 1f;
            Color transparentColor = backgroundColor;
            transparentColor.a = 0f;

            while (Time.time < fadeEnd)
            {
                float t = 1f - (fadeEnd - Time.time);
                Fun.HueShift(Color.Lerp(backgroundColor, transparentColor, t));
                videoPlayer.gameObject.GetComponent<Renderer>().material.color = Color.Lerp(Color.white, Color.clear, t);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    Color.Lerp(main.startColor.color, Color.clear, t)
                );

                yield return null;
            }

            EndImmediately();
        }

        public static void MenuIntro() =>
            CoroutineManager.instance.StartCoroutine(MenuIntroCoroutine());

        public static void ResetVoiceCommandsKeywords()
        {
            if (!File.Exists($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt"))
                File.WriteAllLines($"{PluginInfo.BaseDirectory}/iiMenu_Keywords.txt", keyWords);
        }

        public static void ResetSystemPrompt()
        {
            if (!File.Exists($"{PluginInfo.BaseDirectory}/iiMenu_SystemPrompt.txt"))
                File.WriteAllText($"{PluginInfo.BaseDirectory}/iiMenu_SystemPrompt.txt", AIManager.SystemPrompt);
        }

        private const string PrefsFileName = "iiMenu_Preferences.json";
        private const string LegacyPrefsFileName = "iiMenu_Preferences.txt";
        private const int PrefsFormatVersion = 3;
        private const int LegacyPrefsFieldCount = 78;

        [Serializable] public class BindEntry { public string name; public List<string> mods = new List<string>(); }
        [Serializable] public class RebindEntry { public string button; public string key; }

        [Serializable]
        public class ButtonPrefs
        {
            public List<string> enabled = new List<string>();
            public List<string> favorites = new List<string>();
            public List<string> quickActions = new List<string>();
            public List<string> skipButtons = new List<string>();
            public List<BindEntry> bindings = new List<BindEntry>();
            public List<RebindEntry> rebinds = new List<RebindEntry>();
        }

        [Serializable]
        public class MenuPrefs
        {
            public int pageType = 1, themeType = 1, fontType = 1, pageSize = 8;
            public int menuScale = 10, notificationScale = 6, overlayScale = 6, arraylistScale = 4;
            public int buttonClickSound = 1, buttonClickVolume = 1, menuButton = 1;
            public int characterDistance = 1, joystickPosition = 1;
            public int notificationDecayTime = 2000, notificationSound = 1;
            public int language = 1, inputTextColor = 1, pointer = 1, fontStyle = 1, arrowType = 1, pcBackground = 1;
        }

        [Serializable]
        public class MovementPrefs
        {
            public int platformMode = 1, platformShape = 1, flySpeed = 1, longarm = 1, speedboost = 1;
            public int timerPower = 1, driveSpeed = 1, pullPower = 1, prediction = 1;
            public int playspaceAbuse = 1, wallWalkStrength = 1, macroPlaybackRange = 1;
            public int multiplicationAmount = 1, fakeLagDelay = 1, targetHz = 6000;
        }

        [Serializable]
        public class ProjectilePrefs
        {
            public int mode = 1, shootSpeed = 1, debounce = 1;
            public int red = 1, green = 1, blue = 1;
            public int targetProjectile = 1, snowball = 1;
        }

        [Serializable] public class AdvantagePrefs { public int tagAura = 1; public int tagRange = 1; }

        [Serializable]
        public class SafetyPrefs
        {
            public int fpsSpoof = 90, antiReportRange = 1, rank = 1;
            public int targetElo = 200, targetBadge = 1, pingSpoof = 200;
            public int antiReportPress = 2, watchdogInterval = 2, micGateHold = 2;
            public bool visualizePressRadius = false;
        }

        [Serializable] public class SoundPrefs { public int bindMode = 1; public int soundId = 1; }

        [Serializable]
        public class OverpoweredPrefs
        {
            public int snowballScale = 1, lagIndex = 1, lagType = 1, masterVisualization = 1, snowballMultiplication = 1;
        }

        [Serializable]
        public class FunPrefs
        {
            public int blockDebounce = 1, cycleSpeed = 1, targetQuestScore = 1, headSpin = 1, targetFOV = 60;
        }

        [Serializable]
        public class VisualsPrefs
        {
            public int performanceModeStep = 1, gunVariation = 1, gunDirection = 1, narrator = 1, gunLineQuality = 1;
        }

        [Serializable]
        public class MiscPrefs
        {
            public int playTime = 0;
            public string photonUserId = "";
            public int reconnectDelay = 1;
            public int soundboardLocalVolume = 10, soundboardMicVolume = 10;
            public bool soundboardHighQuality = true;
            public bool throwableFollowPlayer = false;
            public bool throwableMenuGestures = false;
        }

        [Serializable]
        public class PreferencesData
        {
            public int version = PrefsFormatVersion;
            public ButtonPrefs buttons = new ButtonPrefs();
            public MenuPrefs menu = new MenuPrefs();
            public MovementPrefs movement = new MovementPrefs();
            public ProjectilePrefs projectiles = new ProjectilePrefs();
            public AdvantagePrefs advantages = new AdvantagePrefs();
            public SafetyPrefs safety = new SafetyPrefs();
            public SoundPrefs sound = new SoundPrefs();
            public OverpoweredPrefs overpowered = new OverpoweredPrefs();
            public FunPrefs fun = new FunPrefs();
            public VisualsPrefs visuals = new VisualsPrefs();
            public MiscPrefs misc = new MiscPrefs();
        }

        public static string SavePreferencesToJson()
        {
            PreferencesData d = new PreferencesData();

            d.buttons = d.buttons ?? new ButtonPrefs();
            d.menu = d.menu ?? new MenuPrefs();
            d.movement = d.movement ?? new MovementPrefs();
            d.projectiles = d.projectiles ?? new ProjectilePrefs();
            d.advantages = d.advantages ?? new AdvantagePrefs();
            d.safety = d.safety ?? new SafetyPrefs();
            d.sound = d.sound ?? new SoundPrefs();
            d.overpowered = d.overpowered ?? new OverpoweredPrefs();
            d.fun = d.fun ?? new FunPrefs();
            d.visuals = d.visuals ?? new VisualsPrefs();
            d.misc = d.misc ?? new MiscPrefs();

            try
            {
                foreach (ButtonInfo[] buttonlist in Buttons.buttons)
                {
                    if (buttonlist == null) continue;
                    foreach (ButtonInfo v in buttonlist)
                    {
                        if (v == null) continue;
                        if (!v.detected && v.enabled && !v.hideFromArraylist && v.buttonText != "Save Preferences")
                            d.buttons.enabled.Add(v.buttonText);
                        if (!string.IsNullOrEmpty(v.rebindKey))
                            d.buttons.rebinds.Add(new RebindEntry { button = v.buttonText, key = v.rebindKey });
                    }
                }
            }
            catch (Exception e) { LogManager.Log("[Prefs] save buttons: " + e.Message); }

            if (favorites != null) d.buttons.favorites.AddRange(favorites);
            if (quickActions != null) d.buttons.quickActions.AddRange(quickActions);
            if (skipButtons != null) d.buttons.skipButtons.AddRange(skipButtons);

            if (ModBindings != null)
                foreach (KeyValuePair<string, List<string>> bind in ModBindings)
                {
                    BindEntry entry = new BindEntry { name = bind.Key };
                    if (bind.Value != null) entry.mods.AddRange(bind.Value);
                    d.buttons.bindings.Add(entry);
                }

            d.menu.pageType = pageButtonType + 1;
            d.menu.themeType = themeType + 1;
            d.menu.fontType = fontCycle + 1;
            d.menu.pageSize = _pageSize;
            d.menu.menuScale = menuScaleIndex;
            d.menu.notificationScale = notificationScaleIndex;
            d.menu.overlayScale = overlayScaleIndex;
            d.menu.arraylistScale = arraylistScaleIndex;
            d.menu.buttonClickSound = buttonClickIndex + 1;
            d.menu.buttonClickVolume = buttonClickVolume + 1;
            d.menu.menuButton = menuButtonIndex + 1;
            d.menu.characterDistance = characterDistance + 1;
            d.menu.joystickPosition = joystickMenuPosition + 1;
            d.menu.notificationDecayTime = notificationDecayTime + 1000;
            d.menu.notificationSound = notificationSoundIndex + 1;
            d.menu.language = langInd + 1;
            d.menu.inputTextColor = inputTextColorInt + 1;
            d.menu.pointer = pointerIndex + 1;
            d.menu.fontStyle = fontStyleType + 1;
            d.menu.arrowType = arrowType + 1;
            d.menu.pcBackground = pcbg + 1;

            d.movement.platformMode = Movement.platformMode + 1;
            d.movement.platformShape = Movement.platformShape + 1;
            d.movement.flySpeed = Movement.flySpeedCycle + 1;
            d.movement.longarm = Movement.longarmCycle + 1;
            d.movement.speedboost = Movement.speedboostCycle + 1;
            d.movement.timerPower = Movement.timerPowerIndex + 1;
            d.movement.driveSpeed = Movement.driveInt + 1;
            d.movement.pullPower = Movement.pullPowerInt + 1;
            d.movement.prediction = Movement.predInt + 1;
            d.movement.playspaceAbuse = Movement.playspaceAbuseIndex + 1;
            d.movement.wallWalkStrength = Movement.wallWalkStrengthIndex + 1;
            d.movement.macroPlaybackRange = Movement.macroPlaybackRangeIndex + 1;
            d.movement.multiplicationAmount = Movement.multiplicationAmount + 1;
            d.movement.fakeLagDelay = Movement.fakeLagDelayIndex + 1;
            d.movement.targetHz = Movement.targetHz + 500;

            d.projectiles.mode = Projectiles.projMode + 1;
            d.projectiles.shootSpeed = Projectiles.shootCycle + 1;
            d.projectiles.debounce = Projectiles.projDebounceIndex + 1;
            d.projectiles.red = Projectiles.red + 1;
            d.projectiles.green = Projectiles.green + 1;
            d.projectiles.blue = Projectiles.blue + 1;
            d.projectiles.targetProjectile = Projectiles.targetProjectileIndex + 1;
            d.projectiles.snowball = Projectiles.snowballIndex + 1;

            d.advantages.tagAura = Advantages.tagAuraIndex + 1;
            d.advantages.tagRange = Advantages.tagRangeIndex + 1;

            d.safety.fpsSpoof = Safety.fpsSpoofValue + 5;
            d.safety.antiReportRange = Safety.antiReportRangeIndex + 1;
            d.safety.rank = Safety.rankIndex + 1;
            d.safety.targetElo = Safety.targetElo + 100;
            d.safety.targetBadge = Safety.targetBadge + 1;
            d.safety.pingSpoof = Safety.pingSpoofValue + 100;
            d.safety.antiReportPress = Safety.antiReportPressIndex + 1;
            d.safety.watchdogInterval = Safety.watchdogIntervalIndex + 1;
            d.safety.micGateHold = Safety.micGateHoldIndex + 1;
            d.safety.visualizePressRadius = Safety.visualizePressRadius;

            d.sound.bindMode = Sound.BindMode + 1;
            d.sound.soundId = Sound.soundId + 1;

            d.overpowered.snowballScale = Overpowered.snowballScale + 1;
            d.overpowered.lagIndex = Overpowered.lagIndex + 1;
            d.overpowered.lagType = Overpowered.lagTypeIndex + 1;
            d.overpowered.masterVisualization = Overpowered.masterVisualizationType + 1;
            d.overpowered.snowballMultiplication = Overpowered.snowballMultiplicationFactor + 1;

            d.fun.blockDebounce = Fun.blockDebounceIndex + 1;
            d.fun.cycleSpeed = Fun.cycleSpeedIndex + 1;
            d.fun.targetQuestScore = Fun.targetQuestScore + 1;
            d.fun.headSpin = Fun.headSpinIndex + 1;
            d.fun.targetFOV = Fun.targetFOV + 5;

            d.visuals.performanceModeStep = Visuals.PerformanceModeStepIndex + 1;
            d.visuals.gunVariation = gunVariation + 1;
            d.visuals.gunDirection = GunDirection + 1;
            d.visuals.narrator = narratorIndex + 1;
            d.visuals.gunLineQuality = gunLineQualityIndex + 1;

            d.misc.playTime = (int)MathF.Ceiling(playTime);
            try { d.misc.photonUserId = PhotonNetwork.LocalPlayer?.UserId ?? "null"; } catch { d.misc.photonUserId = "null"; }
            d.misc.reconnectDelay = Important.reconnectDelay + 1;
            d.misc.soundboardLocalVolume = Mathf.RoundToInt(SoundboardManager.LocalVolume * 10f);
            d.misc.soundboardMicVolume = Mathf.RoundToInt(SoundboardManager.MicVolume * 10f);
            d.misc.soundboardHighQuality = SoundboardManager.HighQuality;
            d.misc.throwableFollowPlayer = throwableFollowPlayer;
            d.misc.throwableMenuGestures = throwableMenuGestures;

            return JsonConvert.SerializeObject(d, Formatting.Indented);
        }

        public static void SavePreferences()
        {
            string json;
            try { json = SavePreferencesToJson(); }
            catch (Exception e)
            {
                LogManager.Log("[Prefs] SavePreferencesToJson threw, file NOT written: " + e);
                return;
            }

            if (string.IsNullOrEmpty(json) || json.Length < 200
                || !json.Contains("\"buttons\"")
                || !json.Contains("\"movement\"")
                || !json.Contains("\"safety\""))
            {
                LogManager.Log($"[Prefs] REFUSING to write invalid JSON ({json?.Length ?? 0} chars): {json}");
                return;
            }

            File.WriteAllText($"{PluginInfo.BaseDirectory}/{PrefsFileName}", json);
        }

        public static int loadingPreferencesFrame;

        private static void LoadIntPref(string label, ref int field, int value, Action after = null)
        {
            int prev = field;
            try
            {
                field = value;
                after?.Invoke();
            }
            catch (Exception e)
            {
                field = prev;
                LogManager.Log($"[Prefs] {label}: {e.GetType().Name}: {e.Message}");
            }
        }

        public static void LoadPreferencesFromJson(string json)
        {
            loadingPreferencesFrame = Time.frameCount;

            if (string.IsNullOrWhiteSpace(json)) { hasLoadedPreferences = true; return; }

            PreferencesData d;
            try { d = JsonConvert.DeserializeObject<PreferencesData>(json); }
            catch (Exception e)
            {
                LogManager.Log("Invalid preferences JSON: " + e.Message);
                hasLoadedPreferences = true;
                return;
            }

            if (d == null) { hasLoadedPreferences = true; return; }

            Panic();

            if (d.buttons?.enabled != null)
            {
                foreach (string buttonText in d.buttons.enabled)
                {
                    if (string.IsNullOrEmpty(buttonText)) continue;
                    ButtonInfo info = Buttons.GetIndex(buttonText);
                    if (info == null) continue;
                    if (buttonText == "Connect to iiServers") { info.enabled = false; continue; }
                    try { Toggle(buttonText); }
                    catch (Exception e) { LogManager.Log($"[Prefs] toggle {buttonText}: {e.Message}"); }
                }
            }

            favorites.Clear();
            if (d.buttons?.favorites != null)
                foreach (string fav in d.buttons.favorites)
                    if (!string.IsNullOrEmpty(fav) && Buttons.GetIndex(fav) != null)
                        favorites.Add(fav);

            MenuPrefs m = d.menu ?? new MenuPrefs();
            MovementPrefs mv = d.movement ?? new MovementPrefs();
            ProjectilePrefs pj = d.projectiles ?? new ProjectilePrefs();
            AdvantagePrefs ad = d.advantages ?? new AdvantagePrefs();
            SafetyPrefs sf = d.safety ?? new SafetyPrefs();
            SoundPrefs sd = d.sound ?? new SoundPrefs();
            OverpoweredPrefs op = d.overpowered ?? new OverpoweredPrefs();
            FunPrefs fn = d.fun ?? new FunPrefs();
            VisualsPrefs vs = d.visuals ?? new VisualsPrefs();
            MiscPrefs ms = d.misc ?? new MiscPrefs();

            LoadIntPref("platformMode", ref Movement.platformMode, mv.platformMode - 1, () => Movement.ChangePlatformType());
            LoadIntPref("platformShape", ref Movement.platformShape, mv.platformShape - 1, () => Movement.ChangePlatformShape());
            LoadIntPref("flySpeed", ref Movement.flySpeedCycle, mv.flySpeed - 1, () => Movement.ChangeFlySpeed());
            LoadIntPref("longarm", ref Movement.longarmCycle, mv.longarm - 1, () => Movement.ChangeArmLength());
            LoadIntPref("speedboost", ref Movement.speedboostCycle, mv.speedboost - 1, () => Movement.ChangeSpeedBoostAmount());
            LoadIntPref("timerPower", ref Movement.timerPowerIndex, mv.timerPower - 1, () => Movement.ChangeTimerSpeed());
            LoadIntPref("driveSpeed", ref Movement.driveInt, mv.driveSpeed - 1, () => Movement.ChangeDriveSpeed());
            LoadIntPref("pullPower", ref Movement.pullPowerInt, mv.pullPower - 1, () => Movement.ChangePullModPower());
            LoadIntPref("prediction", ref Movement.predInt, mv.prediction - 1, () => Movement.ChangePredictionAmount());
            LoadIntPref("playspaceAbuse", ref Movement.playspaceAbuseIndex, mv.playspaceAbuse - 1, () => Movement.ChangePlayspaceAbuseSpeed());
            LoadIntPref("wallWalkStrength", ref Movement.wallWalkStrengthIndex, mv.wallWalkStrength - 1, () => Movement.ChangeWallWalkStrength());
            LoadIntPref("macroPlaybackRange", ref Movement.macroPlaybackRangeIndex, mv.macroPlaybackRange - 1, () => Movement.ChangeMacroPlaybackRange());
            LoadIntPref("multiplicationAmount", ref Movement.multiplicationAmount, mv.multiplicationAmount - 1, () => Movement.MultiplicationAmount());
            LoadIntPref("fakeLagDelay", ref Movement.fakeLagDelayIndex, mv.fakeLagDelay - 1, () => Movement.ChangeFakeLagStrength());
            LoadIntPref("targetHz", ref Movement.targetHz, mv.targetHz - 500, () => Movement.ChangeTinnitusHz());

            LoadIntPref("projMode", ref Projectiles.projMode, pj.mode - 1, () => Projectiles.ChangeProjectile());
            LoadIntPref("shootSpeed", ref Projectiles.shootCycle, pj.shootSpeed - 1, () => Projectiles.ChangeShootSpeed());
            LoadIntPref("projDebounce", ref Projectiles.projDebounceIndex, pj.debounce - 1, () => Projectiles.ChangeProjectileDelay());
            LoadIntPref("projRed", ref Projectiles.red, pj.red - 1, () => Projectiles.IncreaseRed());
            LoadIntPref("projGreen", ref Projectiles.green, pj.green - 1, () => Projectiles.IncreaseGreen());
            LoadIntPref("projBlue", ref Projectiles.blue, pj.blue - 1, () => Projectiles.IncreaseBlue());
            LoadIntPref("targetProjectile", ref Projectiles.targetProjectileIndex, pj.targetProjectile - 1, () => Projectiles.ChangeProjectileIndex());
            LoadIntPref("snowball", ref Projectiles.snowballIndex, pj.snowball - 1, () => Projectiles.ChangeGrowingProjectile());

            LoadIntPref("tagAura", ref Advantages.tagAuraIndex, ad.tagAura - 1, () => Advantages.ChangeTagAuraRange());
            LoadIntPref("tagRange", ref Advantages.tagRangeIndex, ad.tagRange - 1, () => Advantages.ChangeTagReachDistance());

            LoadIntPref("fpsSpoof", ref Safety.fpsSpoofValue, sf.fpsSpoof - 5, () => Safety.ChangeFPSSpoofValue());
            LoadIntPref("antiReportRange", ref Safety.antiReportRangeIndex, sf.antiReportRange - 1, () => Safety.ChangeAntiReportRange());
            LoadIntPref("rank", ref Safety.rankIndex, sf.rank - 1, () => Safety.ChangeRankedTier());
            LoadIntPref("targetElo", ref Safety.targetElo, sf.targetElo - 100, () => Safety.ChangeELOValue());
            LoadIntPref("targetBadge", ref Safety.targetBadge, sf.targetBadge - 1, () => Safety.ChangeBadgeTier());
            LoadIntPref("pingSpoof", ref Safety.pingSpoofValue, sf.pingSpoof - 100, () => Safety.ChangePingSpoofValue());
            LoadIntPref("antiReportPress", ref Safety.antiReportPressIndex, sf.antiReportPress - 1, () => Safety.ChangeAntiReportPressDistance());
            LoadIntPref("watchdogInterval", ref Safety.watchdogIntervalIndex, sf.watchdogInterval - 1, () => Safety.ChangeWatchdogInterval());
            LoadIntPref("micGateHold", ref Safety.micGateHoldIndex, sf.micGateHold - 1, () => Safety.ChangeMicGateHoldTime());
            Safety.visualizePressRadius = sf.visualizePressRadius;

            LoadIntPref("bindMode", ref Sound.BindMode, sd.bindMode - 1, () => Sound.SoundBindings());
            LoadIntPref("soundId", ref Sound.soundId, sd.soundId - 1, () => Sound.IncreaseSoundID());

            LoadIntPref("snowballScale", ref Overpowered.snowballScale, op.snowballScale - 1, () => Overpowered.ChangeSnowballScale());
            LoadIntPref("lagIndex", ref Overpowered.lagIndex, op.lagIndex - 1, () => Overpowered.ChangeLagPower());
            LoadIntPref("lagType", ref Overpowered.lagTypeIndex, op.lagType - 1, () => Overpowered.ChangeLagType());
            LoadIntPref("masterVisualization", ref Overpowered.masterVisualizationType, op.masterVisualization - 1, () => Overpowered.MasterVisualizationType());
            LoadIntPref("snowballMultiplication", ref Overpowered.snowballMultiplicationFactor, op.snowballMultiplication - 1, () => Overpowered.ChangeSnowballMultiplicationFactor());

            LoadIntPref("blockDebounce", ref Fun.blockDebounceIndex, fn.blockDebounce - 1, () => Fun.ChangeBlockDelay());
            LoadIntPref("cycleSpeed", ref Fun.cycleSpeedIndex, fn.cycleSpeed - 1, () => Fun.ChangeCycleDelay());
            LoadIntPref("targetQuestScore", ref Fun.targetQuestScore, fn.targetQuestScore - 1, () => Fun.ChangeCustomQuestScore());
            LoadIntPref("headSpin", ref Fun.headSpinIndex, fn.headSpin - 1, () => Fun.ChangeHeadSpinSpeed());
            LoadIntPref("targetFOV", ref Fun.targetFOV, fn.targetFOV - 5, () => Fun.ChangeTargetFOV());

            LoadIntPref("performanceModeStep", ref Visuals.PerformanceModeStepIndex, vs.performanceModeStep - 1, () => Visuals.ChangePerformanceModeVisualStep());
            LoadIntPref("gunVariation", ref gunVariation, vs.gunVariation - 1, () => ChangeGunVariation());
            LoadIntPref("gunDirection", ref GunDirection, vs.gunDirection - 1, () => ChangeGunDirection());
            LoadIntPref("narrator", ref narratorIndex, vs.narrator - 1, () => ChangeNarrationVoice());
            LoadIntPref("gunLineQuality", ref gunLineQualityIndex, vs.gunLineQuality - 1, () => ChangeGunLineQuality());

            LoadIntPref("pointer", ref pointerIndex, m.pointer - 1, () => ChangePointerPosition());
            LoadIntPref("notificationDecay", ref notificationDecayTime, m.notificationDecayTime - 1000, () => ChangeNotificationTime());
            LoadIntPref("fontStyle", ref fontStyleType, m.fontStyle - 1, () => ChangeFontStyleType());
            LoadIntPref("arrowType", ref arrowType, m.arrowType - 1, () => ChangeArrowType());
            LoadIntPref("pcBackground", ref pcbg, m.pcBackground - 1, () => ChangePCUI());
            LoadIntPref("buttonClickSound", ref buttonClickIndex, m.buttonClickSound - 1, () => ChangeButtonSound());
            LoadIntPref("buttonClickVolume", ref buttonClickVolume, m.buttonClickVolume - 1, () => ChangeButtonVolume());
            LoadIntPref("language", ref langInd, m.language - 1, () => ChangeMenuLanguage());
            LoadIntPref("inputTextColor", ref inputTextColorInt, m.inputTextColor - 1, () => ChangeInputTextColor());
            LoadIntPref("notificationSound", ref notificationSoundIndex, m.notificationSound - 1, () => ChangeNotificationSound());
            if (m.menuScale < 2 || m.menuScale > 30 || (m.menuScale == 30 && m.pageSize == 16)) m.menuScale = 10;
            LoadIntPref("menuScale", ref menuScaleIndex, m.menuScale, () => ChangeMenuScale(cycle: false));
            if (m.notificationScale < 1 || m.notificationScale > 20) m.notificationScale = 6;
            LoadIntPref("notificationScale", ref notificationScaleIndex, m.notificationScale, () => ChangeNotificationScale(cycle: false));
            if (m.overlayScale < 1 || m.overlayScale > 20) m.overlayScale = 6;
            LoadIntPref("overlayScale", ref overlayScaleIndex, m.overlayScale, () => ChangeOverlayScale(cycle: false));
            if (m.arraylistScale < 1 || m.arraylistScale > 20) m.arraylistScale = 4;
            LoadIntPref("arraylistScale", ref arraylistScaleIndex, m.arraylistScale, () => ChangeArraylistScale(cycle: false));
            if (m.pageSize < 4 || m.pageSize > 16 || (m.pageSize == 16 && m.menuScale == 10)) m.pageSize = 8;
            LoadIntPref("pageSize", ref _pageSize, m.pageSize, () => ChangePageSize(cycle: false));
            LoadIntPref("menuButton", ref menuButtonIndex, m.menuButton - 1, () => ChangeMenuButton());
            LoadIntPref("characterDistance", ref characterDistance, m.characterDistance - 1, () => ChangeCharacterDistance());
            LoadIntPref("joystickPosition", ref joystickMenuPosition, m.joystickPosition - 1, () => ChangeJoystickMenuPosition());

            playTime = ms.playTime;
            Important.oldId = ms.photonUserId;
            LoadIntPref("reconnectDelay", ref Important.reconnectDelay, ms.reconnectDelay - 1, () => ChangeReconnectTime());

            try
            {
                SoundboardManager.LocalVolume = Mathf.Clamp(ms.soundboardLocalVolume / 10f, 0f, 2f);
                SoundboardManager.MicVolume = Mathf.Clamp(ms.soundboardMicVolume / 10f, 0f, 2f);
                SoundboardManager.HighQuality = ms.soundboardHighQuality;
                throwableFollowPlayer = ms.throwableFollowPlayer;
                throwableMenuGestures = ms.throwableMenuGestures;
                SoundboardManager.ApplySettings();
                try
                {
                    Buttons.GetIndex("Soundboard Local Volume").overlapText = $"Soundboard Local Volume <color=grey>[</color><color=green>{Mathf.RoundToInt(SoundboardManager.LocalVolume * 100)}%</color><color=grey>]</color>";
                    Buttons.GetIndex("Soundboard Mic Volume").overlapText = $"Soundboard Mic Volume <color=grey>[</color><color=green>{Mathf.RoundToInt(SoundboardManager.MicVolume * 100)}%</color><color=grey>]</color>";
                }
                catch { }
            }
            catch (Exception e) { LogManager.Log("[Prefs] soundboard: " + e.Message); }

            pageButtonType = (m.pageType == 0 ? 1 : m.pageType) - 1;
            try { Toggle("Change Page Type"); } catch (Exception e) { LogManager.Log("[Prefs] pageType: " + e.Message); }
            if (pageButtonType < 1 || pageButtonType > 6) { pageButtonType = 1; buttonOffset = 2; }

            themeType = (m.themeType == 0 ? 1 : m.themeType) - 1;
            try { Toggle("Change Menu Theme"); } catch (Exception e) { LogManager.Log("[Prefs] themeType: " + e.Message); }

            fontCycle = (m.fontType == 0 ? 1 : m.fontType) - 1;
            try { Toggle("Change Font Type"); } catch (Exception e) { LogManager.Log("[Prefs] fontType: " + e.Message); }

            if (d.buttons?.bindings != null)
                foreach (BindEntry entry in d.buttons.bindings)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.name)) continue;
                    List<string> binds = new List<string>();
                    if (entry.mods != null)
                        foreach (string modName in entry.mods)
                            if (Buttons.GetIndex(modName) != null) binds.Add(modName);
                    ModBindings[entry.name] = binds;
                }

            quickActions.Clear();
            if (d.buttons?.quickActions != null)
                foreach (string qa in d.buttons.quickActions)
                    if (Buttons.GetIndex(qa) != null) quickActions.Add(qa);

            if (d.buttons?.rebinds != null)
                foreach (RebindEntry rebind in d.buttons.rebinds)
                {
                    if (rebind == null || string.IsNullOrEmpty(rebind.button) || string.IsNullOrEmpty(rebind.key)) continue;
                    ButtonInfo button = Buttons.GetIndex(rebind.button);
                    if (button != null) button.rebindKey = rebind.key;
                }

            skipButtons.Clear();
            if (d.buttons?.skipButtons != null)
                foreach (string sb in d.buttons.skipButtons)
                    if (Buttons.GetIndex(sb) != null) skipButtons.Add(sb);

            hasLoadedPreferences = true;
            clickGUI = false;
            ButtonInfo cg = Buttons.GetIndex("Click GUI");
            if (cg != null) cg.enabled = false;
        }



        public static bool TryMigrateLegacyPreferences()
        {
            string jsonPath = $"{PluginInfo.BaseDirectory}/{PrefsFileName}";
            string legacyPath = $"{PluginInfo.BaseDirectory}/{LegacyPrefsFileName}";

            if (!File.Exists(legacyPath)) return false;

            if (File.Exists(jsonPath))
            {
                try { File.Copy(jsonPath, jsonPath + ".bak", true); } catch { }
            }

            try
            {
                string legacyText = File.ReadAllText(legacyPath);
                LogManager.Log($"[Prefs] Migrating {legacyText.Length} chars from .txt");

                LoadPreferencesFromText(legacyText);

                if (menuScaleIndex < 2 || menuScaleIndex > 30)
                {
                    menuScaleIndex = 10;
                    menuScale = 1f;
                }
                if (_pageSize < 4 || _pageSize > 16)
                {
                    _pageSize = 8;
                }

                string json = SavePreferencesToJson();
                if (string.IsNullOrEmpty(json) || json.Length < 200 || !json.Contains("\"buttons\""))
                {
                    LogManager.Log($"[Prefs] Migration SAVE FAILED: JSON looks broken ({json?.Length ?? 0} chars): {json}");
                    return false;
                }

                File.WriteAllText(jsonPath, json);
                File.Delete(legacyPath);
                LogManager.Log($"[Prefs] Migration complete -> {PrefsFileName} ({json.Length} chars), .txt deleted.");
                hasLoadedPreferences = true;
                return true;
            }
            catch (Exception e)
            {
                LogManager.Log("[Prefs] Migration failed, keeping .txt: " + e);
                return false;
            }
        }

        public static void LoadPreferences()
        {
            string jsonPath = $"{PluginInfo.BaseDirectory}/{PrefsFileName}";
            string legacyPath = $"{PluginInfo.BaseDirectory}/{LegacyPrefsFileName}";

            try
            {
                if (File.Exists(legacyPath))
                {
                    if (TryMigrateLegacyPreferences())
                        return;
                }

                if (File.Exists(jsonPath))
                {
                    string existingJson = File.ReadAllText(jsonPath);
                    if (!string.IsNullOrWhiteSpace(existingJson) && existingJson.Length >= 200 && existingJson.Contains("\"buttons\""))
                    {
                        LoadPreferencesFromJson(existingJson);
                        return;
                    }
                    LogManager.Log("[Prefs] Existing JSON is corrupt or incomplete. Recreating...");
                }

                SavePreferences();
                hasLoadedPreferences = true;
                clickGUI = false;
            }
            catch (Exception e)
            {
                LogManager.Log("Error loading preferences: " + e.Message);
                hasLoadedPreferences = true;
            }
        }


        private static string GetPreferenceLine(string[] lines, int index) =>
            lines != null && index >= 0 && index < lines.Length ? lines[index] : string.Empty;

        private static int GetPreferenceInt(string[] data, int index, int fallback)
        {
            if (data == null || index < 0 || index >= data.Length) return fallback;
            return int.TryParse(data[index], out int value) ? value : fallback;
        }

        public static void LoadPreferencesFromText(string text)
        {
            loadingPreferencesFrame = Time.frameCount;

            if (string.IsNullOrWhiteSpace(text)) { hasLoadedPreferences = true; return; }

            Panic();
            string[] textData = text.Replace("\r", "").Split('\n');

            string[] activebuttons = GetPreferenceLine(textData, 0).Split(";;");
            for (int index = 0; index < activebuttons.Length; index++)
            {
                if (string.IsNullOrEmpty(activebuttons[index])) continue;
                if (Buttons.GetIndex(activebuttons[index]) == null) continue;

                if (activebuttons[index] == "Connect to iiServers")
                {
                    Buttons.GetIndex(activebuttons[index]).enabled = false;
                    continue;
                }

                string btn = activebuttons[index];
                try { Toggle(btn); } catch (Exception e) { LogManager.Log($"[Prefs] txt toggle {btn}: {e.Message}"); }
            }

            favorites.Clear();
            foreach (string favorite in GetPreferenceLine(textData, 1).Split(";;"))
                if (!string.IsNullOrEmpty(favorite) && Buttons.GetIndex(favorite) != null)
                    favorites.Add(favorite);

            string[] data = GetPreferenceLine(textData, 2).Split(";;");

            LoadIntPref("txt.platformMode", ref Movement.platformMode, GetPreferenceInt(data, 0, 1) - 1, () => Movement.ChangePlatformType());
            LoadIntPref("txt.platformShape", ref Movement.platformShape, GetPreferenceInt(data, 1, 1) - 1, () => Movement.ChangePlatformShape());
            LoadIntPref("txt.flySpeed", ref Movement.flySpeedCycle, GetPreferenceInt(data, 2, 1) - 1, () => Movement.ChangeFlySpeed());
            LoadIntPref("txt.longarm", ref Movement.longarmCycle, GetPreferenceInt(data, 3, 1) - 1, () => Movement.ChangeArmLength());
            LoadIntPref("txt.speedboost", ref Movement.speedboostCycle, GetPreferenceInt(data, 4, 1) - 1, () => Movement.ChangeSpeedBoostAmount());
            LoadIntPref("txt.timerPower", ref Movement.timerPowerIndex, GetPreferenceInt(data, 6, 1) - 1, () => Movement.ChangeTimerSpeed());
            LoadIntPref("txt.driveSpeed", ref Movement.driveInt, GetPreferenceInt(data, 21, 1) - 1, () => Movement.ChangeDriveSpeed());
            LoadIntPref("txt.pullPower", ref Movement.pullPowerInt, GetPreferenceInt(data, 24, 1) - 1, () => Movement.ChangePullModPower());
            LoadIntPref("txt.prediction", ref Movement.predInt, GetPreferenceInt(data, 30, 1) - 1, () => Movement.ChangePredictionAmount());
            LoadIntPref("txt.playspaceAbuse", ref Movement.playspaceAbuseIndex, GetPreferenceInt(data, 54, 1) - 1, () => Movement.ChangePlayspaceAbuseSpeed());
            LoadIntPref("txt.wallWalkStrength", ref Movement.wallWalkStrengthIndex, GetPreferenceInt(data, 55, 1) - 1, () => Movement.ChangeWallWalkStrength());
            LoadIntPref("txt.macroPlaybackRange", ref Movement.macroPlaybackRangeIndex, GetPreferenceInt(data, 57, 1) - 1, () => Movement.ChangeMacroPlaybackRange());
            LoadIntPref("txt.multiplicationAmount", ref Movement.multiplicationAmount, GetPreferenceInt(data, 59, 1) - 1, () => Movement.MultiplicationAmount());
            LoadIntPref("txt.fakeLagDelay", ref Movement.fakeLagDelayIndex, GetPreferenceInt(data, 62, 1) - 1, () => Movement.ChangeFakeLagStrength());
            LoadIntPref("txt.targetHz", ref Movement.targetHz, GetPreferenceInt(data, 67, 500) - 500, () => Movement.ChangeTinnitusHz());

            LoadIntPref("txt.projMode", ref Projectiles.projMode, GetPreferenceInt(data, 5, 1) - 1, () => Projectiles.ChangeProjectile());
            LoadIntPref("txt.shootSpeed", ref Projectiles.shootCycle, GetPreferenceInt(data, 7, 1) - 1, () => Projectiles.ChangeShootSpeed());
            LoadIntPref("txt.projDebounce", ref Projectiles.projDebounceIndex, GetPreferenceInt(data, 32, 1) - 1, () => Projectiles.ChangeProjectileDelay());
            LoadIntPref("txt.projRed", ref Projectiles.red, GetPreferenceInt(data, 33, 1) - 1, () => Projectiles.IncreaseRed());
            LoadIntPref("txt.projGreen", ref Projectiles.green, GetPreferenceInt(data, 34, 1) - 1, () => Projectiles.IncreaseGreen());
            LoadIntPref("txt.projBlue", ref Projectiles.blue, GetPreferenceInt(data, 35, 1) - 1, () => Projectiles.IncreaseBlue());
            LoadIntPref("txt.targetProjectile", ref Projectiles.targetProjectileIndex, GetPreferenceInt(data, 61, 1) - 1, () => Projectiles.ChangeProjectileIndex());
            LoadIntPref("txt.snowball", ref Projectiles.snowballIndex, GetPreferenceInt(data, 63, 1) - 1, () => Projectiles.ChangeGrowingProjectile());

            LoadIntPref("txt.tagAura", ref Advantages.tagAuraIndex, GetPreferenceInt(data, 9, 1) - 1, () => Advantages.ChangeTagAuraRange());
            LoadIntPref("txt.tagRange", ref Advantages.tagRangeIndex, GetPreferenceInt(data, 19, 1) - 1, () => Advantages.ChangeTagReachDistance());

            LoadIntPref("txt.fpsSpoof", ref Safety.fpsSpoofValue, GetPreferenceInt(data, 15, 90) - 5, () => Safety.ChangeFPSSpoofValue());
            LoadIntPref("txt.antiReportRange", ref Safety.antiReportRangeIndex, GetPreferenceInt(data, 18, 1) - 1, () => Safety.ChangeAntiReportRange());
            LoadIntPref("txt.rank", ref Safety.rankIndex, GetPreferenceInt(data, 36, 1) - 1, () => Safety.ChangeRankedTier());
            LoadIntPref("txt.targetElo", ref Safety.targetElo, GetPreferenceInt(data, 52, 100) - 100, () => Safety.ChangeELOValue());
            LoadIntPref("txt.targetBadge", ref Safety.targetBadge, GetPreferenceInt(data, 53, 1) - 1, () => Safety.ChangeBadgeTier());
            LoadIntPref("txt.pingSpoof", ref Safety.pingSpoofValue, GetPreferenceInt(data, 68, 100) - 100, () => Safety.ChangePingSpoofValue());
            LoadIntPref("txt.antiReportPress", ref Safety.antiReportPressIndex, GetPreferenceInt(data, 74, 2) - 1, () => Safety.ChangeAntiReportPressDistance());
            LoadIntPref("txt.watchdogInterval", ref Safety.watchdogIntervalIndex, GetPreferenceInt(data, 75, 2) - 1, () => Safety.ChangeWatchdogInterval());
            LoadIntPref("txt.micGateHold", ref Safety.micGateHoldIndex, GetPreferenceInt(data, 77, 2) - 1, () => Safety.ChangeMicGateHoldTime());
            Safety.visualizePressRadius = GetPreferenceInt(data, 76, 0) != 0;

            LoadIntPref("txt.bindMode", ref Sound.BindMode, GetPreferenceInt(data, 20, 1) - 1, () => Sound.SoundBindings());
            LoadIntPref("txt.soundId", ref Sound.soundId, GetPreferenceInt(data, 42, 1) - 1, () => Sound.IncreaseSoundID());

            LoadIntPref("txt.snowballScale", ref Overpowered.snowballScale, GetPreferenceInt(data, 37, 1) - 1, () => Overpowered.ChangeSnowballScale());

            try
            {
                int lagVal = (data.Length <= LegacyPrefsFieldCount
                    ? Overpowered.DefaultLagIndex
                    : GetPreferenceInt(data, 38, 1)) - 1;
                LoadIntPref("txt.lagIndex", ref Overpowered.lagIndex, lagVal, () => Overpowered.ChangeLagPower());
            }
            catch (Exception e) { LogManager.Log("[Prefs] txt.lagIndex: " + e.Message); }

            LoadIntPref("txt.lagType", ref Overpowered.lagTypeIndex, GetPreferenceInt(data, 65, 1) - 1, () => Overpowered.ChangeLagType());
            LoadIntPref("txt.masterVisualization", ref Overpowered.masterVisualizationType, GetPreferenceInt(data, 66, 1) - 1, () => Overpowered.MasterVisualizationType());
            LoadIntPref("txt.snowballMult", ref Overpowered.snowballMultiplicationFactor, GetPreferenceInt(data, 50, 1) - 1, () => Overpowered.ChangeSnowballMultiplicationFactor());

            LoadIntPref("txt.blockDebounce", ref Fun.blockDebounceIndex, GetPreferenceInt(data, 39, 1) - 1, () => Fun.ChangeBlockDelay());
            LoadIntPref("txt.cycleSpeed", ref Fun.cycleSpeedIndex, GetPreferenceInt(data, 40, 1) - 1, () => Fun.ChangeCycleDelay());
            LoadIntPref("txt.targetQuestScore", ref Fun.targetQuestScore, GetPreferenceInt(data, 43, 1) - 1, () => Fun.ChangeCustomQuestScore());
            LoadIntPref("txt.headSpin", ref Fun.headSpinIndex, GetPreferenceInt(data, 56, 1) - 1, () => Fun.ChangeHeadSpinSpeed());
            LoadIntPref("txt.targetFOV", ref Fun.targetFOV, GetPreferenceInt(data, 60, 5) - 5, () => Fun.ChangeTargetFOV());

            LoadIntPref("txt.performanceModeStep", ref Visuals.PerformanceModeStepIndex, GetPreferenceInt(data, 26, 1) - 1, () => Visuals.ChangePerformanceModeVisualStep());
            LoadIntPref("txt.gunVariation", ref gunVariation, GetPreferenceInt(data, 27, 1) - 1, () => ChangeGunVariation());
            LoadIntPref("txt.gunDirection", ref GunDirection, GetPreferenceInt(data, 28, 1) - 1, () => ChangeGunDirection());
            LoadIntPref("txt.narrator", ref narratorIndex, GetPreferenceInt(data, 29, 1) - 1, () => ChangeNarrationVoice());
            LoadIntPref("txt.gunLineQuality", ref gunLineQualityIndex, GetPreferenceInt(data, 31, 1) - 1, () => ChangeGunLineQuality());

            LoadIntPref("txt.pointer", ref pointerIndex, GetPreferenceInt(data, 8, 1) - 1, () => ChangePointerPosition());
            LoadIntPref("txt.notificationDecay", ref notificationDecayTime, GetPreferenceInt(data, 10, 1000) - 1000, () => ChangeNotificationTime());
            LoadIntPref("txt.fontStyle", ref fontStyleType, GetPreferenceInt(data, 11, 1) - 1, () => ChangeFontStyleType());
            LoadIntPref("txt.arrowType", ref arrowType, GetPreferenceInt(data, 12, 1) - 1, () => ChangeArrowType());
            LoadIntPref("txt.pcBackground", ref pcbg, GetPreferenceInt(data, 13, 1) - 1, () => ChangePCUI());
            LoadIntPref("txt.buttonClickSound", ref buttonClickIndex, GetPreferenceInt(data, 16, 1) - 1, () => ChangeButtonSound());
            LoadIntPref("txt.buttonClickVolume", ref buttonClickVolume, GetPreferenceInt(data, 17, 1) - 1, () => ChangeButtonVolume());
            LoadIntPref("txt.language", ref langInd, GetPreferenceInt(data, 22, 1) - 1, () => ChangeMenuLanguage());
            LoadIntPref("txt.inputTextColor", ref inputTextColorInt, GetPreferenceInt(data, 23, 1) - 1, () => ChangeInputTextColor());
            LoadIntPref("txt.notificationSound", ref notificationSoundIndex, GetPreferenceInt(data, 25, 1) - 1, () => ChangeNotificationSound());
            int legacyMenuScale = GetPreferenceInt(data, 41, 10);
            if (legacyMenuScale < 2 || legacyMenuScale >= 30) legacyMenuScale = 10;
            LoadIntPref("txt.menuScale", ref menuScaleIndex, legacyMenuScale, () => ChangeMenuScale(cycle: false));
            LoadIntPref("txt.notificationScale", ref notificationScaleIndex, GetPreferenceInt(data, 44, 6), () => ChangeNotificationScale(cycle: false));
            LoadIntPref("txt.overlayScale", ref overlayScaleIndex, GetPreferenceInt(data, 45, 6), () => ChangeOverlayScale(cycle: false));
            LoadIntPref("txt.arraylistScale", ref arraylistScaleIndex, GetPreferenceInt(data, 46, 4), () => ChangeArraylistScale(cycle: false));
            int legacyPageSize = GetPreferenceInt(data, 49, 8);
            if (legacyPageSize < 4 || legacyPageSize >= 16) legacyPageSize = 8;
            LoadIntPref("txt.pageSize", ref _pageSize, legacyPageSize, () => ChangePageSize(cycle: false));
            LoadIntPref("txt.menuButton", ref menuButtonIndex, GetPreferenceInt(data, 51, 1) - 1, () => ChangeMenuButton());
            LoadIntPref("txt.characterDistance", ref characterDistance, GetPreferenceInt(data, 64, 1) - 1, () => ChangeCharacterDistance());
            LoadIntPref("txt.joystickPosition", ref joystickMenuPosition, GetPreferenceInt(data, 58, 1) - 1, () => ChangeJoystickMenuPosition());

            try { playTime = GetPreferenceInt(data, 47, 0); } catch { }
            try { if (data.Length > 48) Important.oldId = data[48]; } catch { }
            LoadIntPref("txt.reconnectDelay", ref Important.reconnectDelay, GetPreferenceInt(data, 14, 1) - 1, () => ChangeReconnectTime());

            try
            {
                if (data.Length > 69) SoundboardManager.LocalVolume = Mathf.Clamp(GetPreferenceInt(data, 69, 10) / 10f, 0f, 2f);
                if (data.Length > 70) SoundboardManager.MicVolume = Mathf.Clamp(GetPreferenceInt(data, 70, 10) / 10f, 0f, 2f);
                if (data.Length > 71) SoundboardManager.HighQuality = data[71] != "0";
                if (data.Length > 72) throwableFollowPlayer = data[72] != "0";
                if (data.Length > 73) throwableMenuGestures = data[73] != "0";

                SoundboardManager.ApplySettings();
                try
                {
                    Buttons.GetIndex("Soundboard Local Volume").overlapText = $"Soundboard Local Volume <color=grey>[</color><color=green>{Mathf.RoundToInt(SoundboardManager.LocalVolume * 100)}%</color><color=grey>]</color>";
                    Buttons.GetIndex("Soundboard Mic Volume").overlapText = $"Soundboard Mic Volume <color=grey>[</color><color=green>{Mathf.RoundToInt(SoundboardManager.MicVolume * 100)}%</color><color=grey>]</color>";
                }
                catch { }
            }
            catch (Exception e) { LogManager.Log("[Prefs] txt.soundboard: " + e.Message); }

            pageButtonType = GetPreferenceInt(textData, 3, 1) - 1;
            try { Toggle("Change Page Type"); } catch (Exception e) { LogManager.Log("[Prefs] txt.pageType: " + e.Message); }
            if (pageButtonType < 1 || pageButtonType > 6) { pageButtonType = 1; buttonOffset = 2; }

            themeType = GetPreferenceInt(textData, 4, 1) - 1;
            try { Toggle("Change Menu Theme"); } catch (Exception e) { LogManager.Log("[Prefs] txt.themeType: " + e.Message); }

            fontCycle = GetPreferenceInt(textData, 5, 1) - 1;
            try { Toggle("Change Font Type"); } catch (Exception e) { LogManager.Log("[Prefs] txt.fontType: " + e.Message); }

            try
            {
                foreach (string Bindings in GetPreferenceLine(textData, 6).Split("~~"))
                {
                    if (!Bindings.Contains(";;")) continue;
                    string[] BindData = Bindings.Split(";;");
                    string BindName = BindData[0];
                    List<string> Binds = new List<string>();
                    for (int i = 1; i < BindData.Length; i++)
                        if (Buttons.GetIndex(BindData[i]) != null) Binds.Add(BindData[i]);
                    ModBindings[BindName] = Binds;
                }
            }
            catch { }

            try
            {
                quickActions.Clear();
                foreach (string quickAction in GetPreferenceLine(textData, 7).Split(";;"))
                    if (Buttons.GetIndex(quickAction) != null) quickActions.Add(quickAction);
            }
            catch { }

            try
            {
                foreach (string bind in GetPreferenceLine(textData, 8).Split(";;"))
                {
                    string[] rebindData = bind.Split(";");
                    if (rebindData.Length < 2) continue;
                    ButtonInfo button = Buttons.GetIndex(rebindData[0]);
                    if (button != null) button.rebindKey = rebindData[1];
                }
            }
            catch { }

            try
            {
                skipButtons.Clear();
                foreach (string skipButton in GetPreferenceLine(textData, 9).Split(";;"))
                    if (Buttons.GetIndex(skipButton) != null) skipButtons.Add(skipButton);
            }
            catch { }

            hasLoadedPreferences = true;
            clickGUI = false;
            ButtonInfo cg = Buttons.GetIndex("Click GUI");
            if (cg != null) cg.enabled = false;
        }

        public static void Panic()
        {
            AnnoyingModeOff();
            for (int categoryIndex = 0; categoryIndex < Buttons.buttons.Length; categoryIndex++)
            {
                if (categoryIndex < Buttons.categoryNames.Length && Buttons.categoryNames[categoryIndex].Contains("Settings"))
                    continue;

                foreach (ButtonInfo v in Buttons.buttons[categoryIndex])
                {
                    if (v.enabled)
                        Toggle(v.buttonText);
                }
            }
        }

        public enum ControllerBinding
        {
            None,
            LeftTrigger,
            RightTrigger,
            LeftGrip,
            RightGrip,
            LeftPrimaryButton,
            RightPrimaryButton,
            LeftSecondaryButton,
            RightSecondaryButton,
            JoystickClick,
            LeftOverride
        }

        public static readonly Dictionary<ControllerBinding, KeyCode> pcBindings = new Dictionary<ControllerBinding, KeyCode>
        {
            { ControllerBinding.RightPrimaryButton, KeyCode.E },
            { ControllerBinding.RightSecondaryButton, KeyCode.R },
            { ControllerBinding.LeftPrimaryButton, KeyCode.F },
            { ControllerBinding.LeftSecondaryButton, KeyCode.G },
            { ControllerBinding.LeftGrip, KeyCode.LeftBracket },
            { ControllerBinding.RightGrip, KeyCode.RightBracket },
            { ControllerBinding.LeftTrigger, KeyCode.Minus },
            { ControllerBinding.RightTrigger, KeyCode.Equals },
            { ControllerBinding.JoystickClick, KeyCode.Return },
            { ControllerBinding.LeftOverride, KeyCode.LeftAlt }
        };

        public static void LoadPCControls()
        {
            string fileName = $"{PluginInfo.BaseDirectory}/iiMenu_PCControls.txt";

            if (File.Exists(fileName))
            {
                string data = File.ReadAllText(fileName);
                string[] lines = data.Split('\n');
                pcBindings.Clear();

                foreach (string line in lines)
                {
                    string finalLine = line.Trim();

                    if (!finalLine.Contains(" - "))
                        continue;

                    string[] splitData = finalLine.Split(" - ");

                    if (Enum.TryParse(splitData[1], out ControllerBinding binding) && Enum.TryParse(splitData[0], out KeyCode key))
                        pcBindings[binding] = key;
                }
            }
            else
            {
                var lines = new List<string>();

                foreach (var pair in pcBindings)
                    lines.Add($"{pair.Value} - {pair.Key}");

                File.WriteAllLines(fileName, lines);
            }
        }


        public static void ChangeReconnectTime(bool positive = true)
        {
            if (positive)
                Important.reconnectDelay++;
            else
                Important.reconnectDelay--;

            if (Important.reconnectDelay > 5)
                Important.reconnectDelay = 1;
            if (Important.reconnectDelay < 1)
                Important.reconnectDelay = 5;

            Buttons.GetIndex("crTime").overlapText = "Change Reconnect Time <color=grey>[</color><color=green>" + Important.reconnectDelay + "</color><color=grey>]</color>";
        }

        public static void ChangeButtonSound(bool positive = true, bool fromMenu = false)
        {
            int[] sounds = {
                8,
                66,
                67,
                84,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                106,
                189,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                66,
                338,
                66,
                66,
                66,
                66,
                66,
                66,
                66
            };
            string[] buttonSoundNames = {
                "Wood",
                "Keyboard",
                "Default",
                "Bubble",
                "Steal",
                "Anthrax",
                "Lever",
                "Minecraft",
                "Rec Room",
                "Watch",
                "Membrane",
                "Jar",
                "Wall",
                "Slider",
                "Can",
                "Cut",
                "Creamy",
                "Roblox Button",
                "Roblox Tick",
                "Mouse",
                "Valve",
                "Nintendo",
                "Windows",
                "Destiny",
                "Untitled",
                "Slap",
                "Dog",
                "GMod Spawn",
                "GMod Undo",
                "Half Life",
                "Mine",
                "Sensation",
                "Poop"
            };

            if (positive)
                buttonClickIndex++;
            else
                buttonClickIndex--;

            buttonClickIndex %= sounds.Length;
            if (buttonClickIndex < 0)
                buttonClickIndex = sounds.Length - 1;

            buttonClickSound = sounds[buttonClickIndex];
            Buttons.GetIndex("Change Button Sound").overlapText = "Change Button Sound <color=grey>[</color><color=green>" + buttonSoundNames[buttonClickIndex] + "</color><color=grey>]</color>";

            if (fromMenu)
            {
                VRRig.LocalRig.leftHandPlayer.Stop();
                VRRig.LocalRig.rightHandPlayer.Stop();
                PlayButtonSound();
            }
        }

        public static void ChangeButtonVolume(bool positive = true, bool fromMenu = false)
        {
            if (positive)
                buttonClickVolume++;
            else
                buttonClickVolume--;

            buttonClickVolume %= 11;
            if (buttonClickVolume < 0)
                buttonClickVolume = 10;

            Buttons.GetIndex("Change Button Volume").overlapText = "Change Button Volume <color=grey>[</color><color=green>" + buttonClickVolume + "</color><color=grey>]</color>";

            if (fromMenu)
            {
                VRRig.LocalRig.leftHandPlayer.Stop();
                VRRig.LocalRig.rightHandPlayer.Stop();
                PlayButtonSound();
            }
        }
    }
}

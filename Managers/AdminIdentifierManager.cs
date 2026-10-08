using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using GorillaLocomotion;
using iiMenu.Menu;
using iiMenu.Utilities;

namespace iiMenu.Managers
{
    [HarmonyPatch(typeof(GorillaTagger), "LateUpdate")]
    internal class AdminIdentifierManager
    {
        private static readonly Dictionary<VRRig, GameObject> adminTags = new Dictionary<VRRig, GameObject>();

        private static readonly List<KeyValuePair<VRRig, GameObject>> adminTagsSnapshot = new List<KeyValuePair<VRRig, GameObject>>();

        private static TelemetryClient.AdminEntry FindAdmin(string userId)
        {
            foreach (TelemetryClient.AdminEntry entry in TelemetryClient.VerifiedAdmins)
                if (string.Equals(entry.Id, userId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            return null;
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

        private static void Postfix()
        {
            if (!Photon.Pun.PhotonNetwork.InRoom)
            {
                if (adminTags.Count > 0)
                {
                    foreach (var tag in adminTags)
                    {
                        if (tag.Value != null) UnityEngine.Object.Destroy(tag.Value);
                    }
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

            foreach (var vrrig in VRRigCache.ActiveRigs)
            {
                if (vrrig == null || vrrig.isLocal) continue;

                NetPlayer player = RigUtilities.GetPlayerFromVRRig(vrrig);
                string userId = player != null ? player.UserId : null;
                
                if (string.IsNullOrEmpty(userId)) continue;

                TelemetryClient.AdminEntry profile = FindAdmin(userId);
                if (profile == null) continue;

                if (!adminTags.ContainsKey(vrrig))
                {
                    NotificationManager.SendNotification($"<color=white>[</color><color=orange>iiMenu</color><color=white>]</color> {profile.Role} in lobby!");

                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                    go.name = "iiMenu_AdminIdentifierTag";

                    if (!string.IsNullOrEmpty(profile.Avatar))
                    {
                        if (profile.Avatar.StartsWith("https://"))
                        {
                            GorillaTagger.Instance.StartCoroutine(LoadAvatarCoroutine(profile.Avatar, go.GetComponent<Renderer>()));
                        }
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
                    
                    adminTags.Add(vrrig, go);
                }

                GameObject tagObj = adminTags[vrrig];
                if (tagObj == null) continue;

                tagObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f) * vrrig.scaleFactor;
                tagObj.transform.position = vrrig.headMesh.transform.position + new Vector3(0f, 0.60f * vrrig.scaleFactor, 0f);
                Camera camera = Camera.main;
                if (camera != null)
                {
                    tagObj.transform.LookAt(camera.transform.position);
                    tagObj.transform.Rotate(0f, 180f, 0f);
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
                {
                    NotificationManager.SendNotification("No admins currently in lobby!");
                }
            }
        }
    }
}

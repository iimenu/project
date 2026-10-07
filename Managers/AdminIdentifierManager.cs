using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GorillaLocomotion;
using iiMenu.Menu;
using iiMenu.Utilities;

namespace iiMenu.Managers
{
    [HarmonyPatch(typeof(GorillaTagger), "LateUpdate")]
    internal class AdminIdentifierManager
    {
        private static readonly Dictionary<VRRig, GameObject> adminTags = new Dictionary<VRRig, GameObject>();

        private class AdminProfile
        {
            public string PlayerId;
            public string AvatarUrl;
            public string Role;
        }

        private static readonly List<AdminProfile> adminProfiles = new List<AdminProfile>
        {
            new AdminProfile { PlayerId = "", AvatarUrl = "", Role = "Owner" }, // kingsells
            new AdminProfile { PlayerId = "", AvatarUrl = "", Role = "Admin" }, // ian/corgilander
            new AdminProfile { PlayerId = "7446E754FFEBA04B", AvatarUrl = "https://cdn.discordapp.com/attachments/1556326506425225247/1556690861352288366/poopooVR.png?backend=b2&ex=6ac514d7&is=6ac3c357&hm=07ed3e856556e0459d1d7c3fa4e808ebdd4ace52fdd0d792a87104ed583b7f39&", Role = "Menu Dev" }, // poopooVR
            new AdminProfile { PlayerId = "516DBB64CEA52378", AvatarUrl = "https://cdn.discordapp.com/attachments/1556326506425225247/1556690861352288366/poopooVR.png?backend=b2&ex=6ac514d7&is=6ac3c357&hm=07ed3e856556e0459d1d7c3fa4e808ebdd4ace52fdd0d792a87104ed583b7f39&", Role = "Menu Dev" }, // poopooVR
            new AdminProfile { PlayerId = "3E175F722BF34FB9", AvatarUrl = "https://cdn.discordapp.com/attachments/1556326506425225247/1556690861352288366/poopooVR.png?backend=b2&ex=6ac514d7&is=6ac3c357&hm=07ed3e856556e0459d1d7c3fa4e808ebdd4ace52fdd0d792a87104ed583b7f39&", Role = "Menu Dev" }, // poopooVR
            new AdminProfile { PlayerId = "FD76A37F77BE3B04", AvatarUrl = "https://cdn.discordapp.com/attachments/1556326506425225247/1557066960003661875/thing.png?backend=b2&ex=6ac6731c&is=6ac5219c&hm=ca26d1686392e4cd3b96e09dca40c1feeb694557dacf42fda1a7dad9990dda4a&", Role = "Menu Dev" } // !Lucy
        };
        
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

            List<KeyValuePair<VRRig, GameObject>> tagsCopy = new List<KeyValuePair<VRRig, GameObject>>(adminTags);
            foreach (var tag in tagsCopy)
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

                AdminProfile profile = adminProfiles.FirstOrDefault(p => p.PlayerId == userId);
                if (profile == null) continue;

                if (!adminTags.ContainsKey(vrrig))
                {
                    NotificationManager.SendNotification($"<color=white>[</color><color=orange>iiMenu</color><color=white>]</color> {profile.Role} in lobby!");

                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                    go.name = "iiMenu_AdminIdentifierTag";
                    
                    if (!string.IsNullOrEmpty(profile.AvatarUrl))
                    {
                        GorillaTagger.Instance.StartCoroutine(LoadAvatarCoroutine(profile.AvatarUrl, go.GetComponent<Renderer>()));
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
                if (Camera.main != null)
                {
                    tagObj.transform.LookAt(Camera.main.transform.position);
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
                    AdminProfile profile = adminProfiles.FirstOrDefault(p => p.PlayerId == userId);
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

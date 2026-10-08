using BepInEx;
using HarmonyLib;
using Photon.Voice.Unity;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace iiMenu.Managers
{
    public class EmoteManager
    {
        private static AssetBundle assetBundle;
        private static bool bundleVerified;

        private const string EmoteBundleUrl = "https://github.com/poopoovr/fn/releases/download/1/fn";
        private const string EmoteBundleSha256 = "3cdfe11adabfa1745882a3151cfd1de524b9c6db95883e449b4655efe07dda75";

        public static void DownloadEmotes()
        {
            if (CoroutineManager.instance != null)
                CoroutineManager.instance.StartCoroutine(DownloadEmotesCoroutine());
        }

        private static System.Collections.IEnumerator DownloadEmotesCoroutine()
        {
            string url = EmoteBundleUrl;
            string path = System.IO.Path.Combine(PluginInfo.BaseDirectory, "fn");
            if (System.IO.File.Exists(path))
            {
                NotificationManager.SendNotification("Emotes (fn) are already downloaded.");
                yield break;
            }

            NotificationManager.SendNotification("Downloading Emotes (fn) (~21MB)...");
            using (UnityEngine.Networking.UnityWebRequest dl = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                dl.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                dl.timeout = 120;
                yield return dl.SendWebRequest();
                if (dl.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    NotificationManager.SendNotification($"<color=red>Download failed: {dl.error}</color>");
                    yield break;
                }
                try
                {
                    string got;
                    using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                        got = System.BitConverter.ToString(sha.ComputeHash(dl.downloadHandler.data)).Replace("-", "").ToLowerInvariant();
                    if (got != EmoteBundleSha256)
                    {
                        NotificationManager.SendNotification("<color=red>Download did not pass verification, nothing was saved.</color>");
                        yield break;
                    }
                    System.IO.File.WriteAllBytes(path, dl.downloadHandler.data);
                    NotificationManager.SendNotification("Emotes downloaded successfully!");
                }
                catch (System.Exception ex)
                {
                    NotificationManager.SendNotification($"<color=red>Save failed: {ex.Message}</color>");
                }
            }
        }

        private static bool BundleReady()
        {
            if (assetBundle != null)
                return true;

            string fnPath = System.IO.Path.Combine(PluginInfo.BaseDirectory, "fn");
            if (!System.IO.File.Exists(fnPath))
                return false;

            byte[] bytes = System.IO.File.ReadAllBytes(fnPath);
            if (!bundleVerified)
            {
                string got;
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                    got = System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (got != EmoteBundleSha256)
                {
                    UnityEngine.Debug.LogError("emote bundle failed verification");
                    return false;
                }
                bundleVerified = true;
            }

            assetBundle = AssetBundle.LoadFromMemory(bytes);
            return assetBundle != null;
        }

        public static GameObject LoadAsset(string assetName)
        {
            if (!BundleReady())
            {
                UnityEngine.Debug.LogError("Failed to load asset from file: " + System.IO.Path.Combine(PluginInfo.BaseDirectory, "fn") + " (Make sure to download emotes!)");
                return null;
            }
            return Object.Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
        }

        public static GameObject audiomgr = null;
        public static void Play2DAudio(AudioClip sound, float volume, bool looping = false)
        {
            if (audiomgr == null)
            {
                audiomgr = new GameObject("2DAudioMgr");
                AudioSource temp = audiomgr.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }
            AudioSource ausrc = audiomgr.GetComponent<AudioSource>();
            ausrc.volume = volume;
            ausrc.loop = looping;
            if (!looping)
                ausrc.PlayOneShot(sound);
            else
            {
                ausrc.clip = sound;
                ausrc.Play();
            }
        }

        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromResource(string resourcePath)
        {
            AudioClip sound = null;
            if (!audioPool.ContainsKey(resourcePath))
            {
                if (BundleReady())
                {
                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    if (sound != null) audioPool.Add(resourcePath, sound);
                }
                else
                    UnityEngine.Debug.LogError("Failed to load sound from file: " + System.IO.Path.Combine(PluginInfo.BaseDirectory, "fn"));
            }
            else
                sound = audioPool[resourcePath];

            return sound;
        }

        private static readonly List<GameObject> portedCosmetics = new List<GameObject> { };
        public static void DisableCosmetics()
        {
            try
            {
                Transform face = VRRig.LocalRig.transform.Find("rig/head/gorillaface");
                if (face) face.gameObject.layer = LayerMask.NameToLayer("Default");
                foreach (GameObject Cosmetic in VRRig.LocalRig.cosmetics)
                {
                    if (Cosmetic.activeSelf && Cosmetic.transform.parent == VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"))
                    {
                        portedCosmetics.Add(Cosmetic);
                        Cosmetic.transform.SetParent(VRRig.LocalRig.headMesh.transform, false);
                        Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                    }
                }
            } catch { }
        }

        public static void EnableCosmetics()
        {
            try
            {
                Transform face = VRRig.LocalRig.transform.Find("rig/head/gorillaface");
                if (face) face.gameObject.layer = LayerMask.NameToLayer("MirrorOnly");
                foreach (GameObject Cosmetic in portedCosmetics)
                {
                    if (Cosmetic != null && Cosmetic.transform != null && VRRig.LocalRig.mainCamera != null)
                    {
                        Transform headCosmetics = VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics");
                        if (headCosmetics != null)
                        {
                            Cosmetic.transform.SetParent(headCosmetics, false);
                            Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
                        }
                    }
                }
                portedCosmetics.Clear();
            }
            catch { }
        }

        public static GameObject Kyle;
        public static Transform emoteSpine;
        public static Transform emoteLeftHand;
        public static Transform emoteRightHand;
        public static Transform emoteHead;
        public static float emoteTime;
        
        
        
        

        private static readonly System.Type[] allowedComponents =
        {
            typeof(Transform), typeof(Animator), typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer),
            typeof(AudioSource), typeof(Canvas), typeof(CanvasRenderer), typeof(RectTransform),
            typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.CanvasScaler), typeof(Animation)
        };

        private static void HardenRig(GameObject rig)
        {
            foreach (Component component in rig.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                    continue;

                bool allowed = false;
                foreach (System.Type allowedType in allowedComponents)
                {
                    if (allowedType.IsInstanceOfType(component))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    Object.Destroy(component);
            }

            Animator animator = rig.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    try { clip.events = new AnimationEvent[0]; } catch { }
                }
            }
        }

        public static void StopEmote()
        {
            emoteTime = -9999f;
            if (audiomgr != null)
            {
                AudioSource ausrc = audiomgr.GetComponent<AudioSource>();
                if (ausrc != null)
                    ausrc.Stop();
            }
        }

        public static Vector3 archivePosition;
        public static Vector3 archiveCamPos;
        public static Quaternion archiveCamRot;

        public static void Emote(string emoteName, string emoteSound, float animationTime = -1f, bool looping = false)
        {
            if (Kyle != null)
                Object.Destroy(Kyle);

            if (GorillaTagger.Instance.gameObject.GetComponent<EmoteUpdater>() == null)
            {
                GorillaTagger.Instance.gameObject.AddComponent<EmoteUpdater>();
            }

            VRRig.LocalRig.enabled = false;
            DisableCosmetics();
            


            Play2DAudio(LoadSoundFromResource("play"), 0.5f);

            if (Kyle == null) archivePosition = GorillaTagger.Instance.transform.position;

            Kyle = LoadAsset("Rig");
            if (Kyle == null)
                return;
            HardenRig(Kyle);
            emoteSpine = Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2");
            emoteLeftHand = Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/LeftShoulder/LeftUpperArm/LeftArm/LeftHand");
            emoteRightHand = Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand");
            emoteHead = Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/Neck/Head");
            Transform bodyPivot = VRRig.LocalRig.transform.Find("rig/body_pivot") ?? VRRig.LocalRig.transform;
            Kyle.transform.position = bodyPivot.position - new Vector3(0f, 1.15f, 0f);
            Kyle.transform.rotation = bodyPivot.rotation;

            if (GameObject.Find("EmoteCameraOffset") == null) {
                GameObject camOffset = new GameObject("EmoteCameraOffset");
                Transform mainCam = GorillaTagger.Instance.mainCamera.transform;
                archiveCamPos = mainCam.localPosition;
                archiveCamRot = mainCam.localRotation;
                
                camOffset.transform.SetParent(mainCam.parent, false);
                camOffset.transform.position = mainCam.position;
                camOffset.transform.rotation = mainCam.rotation;
                mainCam.SetParent(camOffset.transform, true);
                
                camOffset.transform.position += Kyle.transform.forward * 1.5f + new Vector3(0f, 0.5f, 0f);
                camOffset.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

            Kyle.transform.Find("KyleRobot/RobotKile").gameObject.GetComponent<Renderer>().renderingLayerMask = 0;

            Animator KyleRobot = Kyle.transform.Find("KyleRobot").GetComponent<Animator>();
            KyleRobot.enabled = true;
            
            AnimationClip Animation = null;
            foreach (AnimationClip Clip in KyleRobot.runtimeAnimatorController.animationClips)
            {
                if (Clip.name == emoteName)
                {
                    Animation = Clip;
                    break;
                }
            }

            if (Animation == null)
            {
                Debug.LogError("Emote animation not found: " + emoteName);
                EnableCosmetics();
                VRRig.LocalRig.enabled = true;
                Object.Destroy(Kyle);
                return;
            }

            Animation.wrapMode = looping ? WrapMode.Loop : WrapMode.Default;
            KyleRobot.Play(Animation.name);

            AudioClip Sound = LoadSoundFromResource(emoteSound);
            Play2DAudio(Sound, 0.5f, looping);

            if (GorillaTagger.Instance.myRecorder != null)
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip = Sound;
                GorillaTagger.Instance.myRecorder.RestartRecording(true);
            }

            emoteTime = Time.time + (animationTime > 0f ? animationTime : Animation.length) + (looping ? 999999999999999f : 0);
        }

        public static Vector3 World2Player(Vector3 world) => world - GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.transform.position;

        public class EmoteUpdater : MonoBehaviour
        {
            public void Update()
            {
                if (GorillaLocomotion.GTPlayer.Instance == null)
                    return;

                if (Time.time < EmoteManager.emoteTime)
                {
                    if (EmoteManager.Kyle != null && EmoteManager.emoteSpine != null && EmoteManager.emoteLeftHand != null && EmoteManager.emoteRightHand != null)
                    {
                        VRRig.LocalRig.enabled = false;

                        VRRig.LocalRig.transform.position = EmoteManager.emoteSpine.position - (EmoteManager.emoteSpine.right / 2.5f);
                        VRRig.LocalRig.transform.rotation = Quaternion.Euler(new Vector3(0f, EmoteManager.emoteSpine.rotation.eulerAngles.y, 0f));

                        VRRig.LocalRig.leftHand.rigTarget.transform.position = EmoteManager.emoteLeftHand.position;
                        VRRig.LocalRig.rightHand.rigTarget.transform.position = EmoteManager.emoteRightHand.position;

                        VRRig.LocalRig.leftHand.rigTarget.transform.rotation = EmoteManager.emoteLeftHand.rotation * Quaternion.Euler(0, 0, 75);
                        VRRig.LocalRig.rightHand.rigTarget.transform.rotation = EmoteManager.emoteRightHand.rotation * Quaternion.Euler(180, 0, -75);
                    }
                }
                else
                {
                    if (EmoteManager.Kyle != null)
                    {
                        VRRig.LocalRig.enabled = true;
                        EmoteManager.EnableCosmetics();
                        
                        Object.Destroy(EmoteManager.Kyle);
                        EmoteManager.Kyle = null;
                        EmoteManager.emoteSpine = null;
                        EmoteManager.emoteLeftHand = null;
                        EmoteManager.emoteRightHand = null;
                        EmoteManager.emoteHead = null;

                        if (GorillaTagger.Instance.myRecorder != null)
                        {
                            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                            GorillaTagger.Instance.myRecorder.AudioClip = null;
                            GorillaTagger.Instance.myRecorder.RestartRecording(true);
                        }

                        GameObject camOffset = GameObject.Find("EmoteCameraOffset");
                        if (camOffset != null) {
                            Transform mainCam = GorillaTagger.Instance.mainCamera.transform;
                            mainCam.SetParent(camOffset.transform.parent, true);
                            mainCam.localPosition = archiveCamPos;
                            mainCam.localRotation = archiveCamRot;
                            Object.Destroy(camOffset);
                        }

                        // GorillaTagger.Instance.transform.position = EmoteManager.archivePosition;
                        // GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                    }
                }
            }

            public void LateUpdate()
            {
                if (GorillaLocomotion.GTPlayer.Instance == null || Time.time >= EmoteManager.emoteTime || EmoteManager.Kyle == null)
                    return;

                if (EmoteManager.emoteHead == null)
                    return;

                VRRig.LocalRig.head.rigTarget.transform.rotation = EmoteManager.emoteHead.rotation * Quaternion.Euler(0f, 0f, 90f);
                if (VRRig.LocalRig.headMesh != null)
                {
                    VRRig.LocalRig.headMesh.transform.rotation = VRRig.LocalRig.head.rigTarget.transform.rotation;
                }
            }
        }
    }
}










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
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("iiMenu.Resources.fn");
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);
                gameObject = Object.Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
                Debug.LogError("Failed to load asset from resource: " + assetName);

            return gameObject;
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
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("iiMenu.Resources.fn");
                if (stream != null)
                {
                    if (assetBundle == null)
                        assetBundle = AssetBundle.LoadFromStream(stream);
                    
                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    audioPool.Add(resourcePath, sound);
                }
                else
                {
                    Debug.LogError("Failed to load sound from resource: " + resourcePath);
                }
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
        public static float emoteTime;
        
        
        
        

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
            GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);

            Kyle = LoadAsset("Rig"); 
            Transform bodyPivot = VRRig.LocalRig.transform.Find("rig/body_pivot") ?? VRRig.LocalRig.transform;
            Kyle.transform.position = bodyPivot.position - new Vector3(0f, 1.15f, 0f);
            Kyle.transform.rotation = bodyPivot.rotation;

            GorillaTagger.Instance.transform.position = World2Player(Kyle.transform.position + (Kyle.transform.forward * 1.5f) + new Vector3(0f, 1.15f, 0f)) + new Vector3(0f, 0.5f, 0f);
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
                    if (EmoteManager.Kyle != null)
                    {
                        VRRig.LocalRig.enabled = false;

                        VRRig.LocalRig.transform.position = EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2").transform.position - (EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2").transform.right / 2.5f);
                        VRRig.LocalRig.transform.rotation = Quaternion.Euler(new Vector3(0f, EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2").transform.rotation.eulerAngles.y, 0f));

                        VRRig.LocalRig.leftHand.rigTarget.transform.position = EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/LeftShoulder/LeftUpperArm/LeftArm/LeftHand").transform.position;
                        VRRig.LocalRig.rightHand.rigTarget.transform.position = EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand").transform.position;

                        VRRig.LocalRig.leftHand.rigTarget.transform.rotation = EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/LeftShoulder/LeftUpperArm/LeftArm/LeftHand").transform.rotation * Quaternion.Euler(0, 0, 75);
                        VRRig.LocalRig.rightHand.rigTarget.transform.rotation = EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand").transform.rotation * Quaternion.Euler(180, 0, -75);
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

                        if (GorillaTagger.Instance.myRecorder != null)
                        {
                            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                            GorillaTagger.Instance.myRecorder.AudioClip = null;
                            GorillaTagger.Instance.myRecorder.RestartRecording(true);
                        }

                        GorillaTagger.Instance.transform.position = EmoteManager.archivePosition;
                        GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                    }
                }
            }

            public void LateUpdate()
            {
                if (GorillaLocomotion.GTPlayer.Instance == null || Time.time >= EmoteManager.emoteTime || EmoteManager.Kyle == null)
                    return;

                VRRig.LocalRig.head.rigTarget.transform.rotation = EmoteManager.Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/Neck/Head").transform.rotation * Quaternion.Euler(0f, 0f, 90f);
                if (VRRig.LocalRig.headMesh != null)
                {
                    VRRig.LocalRig.headMesh.transform.rotation = VRRig.LocalRig.head.rigTarget.transform.rotation;
                }
            }
        }
    }
}










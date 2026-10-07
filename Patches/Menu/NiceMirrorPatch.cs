using HarmonyLib;
using UnityEngine;
using System.Collections;
using GorillaLocomotion;
using UnityEngine.SceneManagement;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(GorillaTagger), "LateUpdate")]
    internal class NiceMirrorPatch
    {
        public static bool enabled = false;
        private static bool isEnhanced = false;
        private static int originalWidth = 512;
        private static int originalHeight = 512;
        private static int originalCullingMask = 0;
        private static float originalFarClip = 0f;
        private static float nextCheckTime = 0f;
        
        private const string MirrorBackdropPath = "City_Pretty/CosmeticsRoomAnchor/nicegorillastore_prefab/DressingRoom_Mirrors_Prefab/Mirror Backdrop";
        private const string MirrorCameraPath = "City_Pretty/CosmeticsRoomAnchor/nicegorillastore_prefab/DressingRoom_Mirrors_Prefab/CameraC";
        private const int TargetResolution = 2048;

        public static void Enable()
        {
            enabled = true;
            nextCheckTime = 0f;
        }

        public static void Disable()
        {
            enabled = false;
            DowngradeMirror();
        }

        private static void Postfix()
        {
            if (enabled && !isEnhanced)
            {
                if (Time.time > nextCheckTime)
                {
                    nextCheckTime = Time.time + 2f;
                    EnhanceMirror();
                }
            }
            else if (!enabled && isEnhanced)
            {
                DowngradeMirror();
            }
        }

        private static void EnhanceMirror()
        {
            GameObject cameraObj = GameObject.Find(MirrorCameraPath);
            if (cameraObj == null) return;

            Camera cam = cameraObj.GetComponent<Camera>();
            if (cam == null) return;

            GameObject backdrop = GameObject.Find(MirrorBackdropPath);
            if (backdrop != null)
            {
                Renderer backdropRenderer = backdrop.GetComponent<Renderer>();
                if (backdropRenderer != null)
                {
                    backdropRenderer.enabled = false;
                }
            }

            originalFarClip = cam.farClipPlane;
            originalCullingMask = cam.cullingMask;

            cam.farClipPlane = 40f;
            cam.cullingMask = -1;
            cam.pixelRect = new Rect(0, 0, TargetResolution, TargetResolution);

            RenderTexture rt = cam.targetTexture;
            if (rt != null)
            {
                originalWidth = rt.width;
                originalHeight = rt.height;

                bool wasActiveRT = RenderTexture.active == rt;
                bool wasCamRT = cam.targetTexture == rt;

                rt.Release();
                rt.width = TargetResolution;
                rt.height = TargetResolution;
                rt.Create();

                if (wasCamRT) cam.targetTexture = rt;
                if (wasActiveRT) RenderTexture.active = rt;
                
                isEnhanced = true;
            }
        }

        private static void DowngradeMirror()
        {
            if (!isEnhanced) return;

            GameObject cameraObj = GameObject.Find(MirrorCameraPath);
            if (cameraObj != null)
            {
                Camera cam = cameraObj.GetComponent<Camera>();
                if (cam != null)
                {
                    if (originalFarClip > 0) cam.farClipPlane = originalFarClip;
                    if (originalCullingMask != 0) cam.cullingMask = originalCullingMask;

                    RenderTexture rt = cam.targetTexture;
                    if (rt != null)
                    {
                        bool wasActiveRT = RenderTexture.active == rt;
                        bool wasCamRT = cam.targetTexture == rt;

                        rt.Release();
                        rt.width = originalWidth > 0 ? originalWidth : 512;
                        rt.height = originalHeight > 0 ? originalHeight : 512;
                        rt.Create();

                        if (wasCamRT) cam.targetTexture = rt;
                        if (wasActiveRT) RenderTexture.active = rt;
                    }
                }
            }

            GameObject backdrop = GameObject.Find(MirrorBackdropPath);
            if (backdrop != null)
            {
                Renderer backdropRenderer = backdrop.GetComponent<Renderer>();
                if (backdropRenderer != null)
                {
                    backdropRenderer.enabled = true;
                }
            }

            isEnhanced = false;
        }
    }
}

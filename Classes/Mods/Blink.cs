/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using iiMenu.Utilities;
using UnityEngine;

namespace iiMenu.Classes.Mods
{
    public class Blink : MonoBehaviour
    {
        public VRRig rig;
        public Renderer faceRenderer;
        private Material originalMaterial;
        private Material faceMaterial;
        private static Texture2D blinkSheet;

        private float blinkTime;
        private BlinkState currentBlinkState = BlinkState.Idle;

        private enum BlinkState
        {
            Idle,
            IdleHalf,
            BlinkClosed,
            BlinkHalf
        }

        public void Awake()
        {
            rig = GetComponent<VRRig>();
            if (rig == null)
                return;

            faceRenderer = rig.faceSkin != null ? (Renderer)rig.faceSkin : (rig.headMesh != null ? rig.headMesh.transform.Find("gorillaface")?.GetComponent<Renderer>() : null);
            if (faceRenderer == null)
                return;

            if (blinkSheet == null)
                blinkSheet = AssetUtilities.LoadTextureFromResource($"{PluginInfo.ClientResourcePath}.blinksheet.png");

            originalMaterial = faceRenderer.material;
            faceMaterial = new Material(originalMaterial);
            if (blinkSheet != null)
                faceMaterial.mainTexture = blinkSheet;
            faceMaterial.mainTextureScale = new Vector2(1f / 3f, 1f);
            faceMaterial.mainTextureOffset = Vector2.zero;
            faceMaterial.EnableKeyword("_USE_TEXTURE");
            faceRenderer.material = faceMaterial;
            blinkTime = Time.time + Random.Range(2f, 7.5f);
        }

        public void LateUpdate()
        {
            if (rig == null || faceRenderer == null || faceMaterial == null)
                return;

            if (Time.time >= blinkTime)
            {
                switch (currentBlinkState)
                {
                    case BlinkState.Idle:
                        currentBlinkState = BlinkState.IdleHalf;
                        faceMaterial.mainTextureOffset = new Vector2(1f / 3f, 0f);
                        blinkTime = Time.time + 0.07f;
                        break;
                    case BlinkState.IdleHalf:
                        currentBlinkState = BlinkState.BlinkClosed;
                        faceMaterial.mainTextureOffset = new Vector2(2f / 3f, 0f);
                        blinkTime = Time.time + 0.22f;
                        break;
                    case BlinkState.BlinkClosed:
                        currentBlinkState = BlinkState.BlinkHalf;
                        faceMaterial.mainTextureOffset = new Vector2(1f / 3f, 0f);
                        blinkTime = Time.time + 0.10f;
                        break;
                    case BlinkState.BlinkHalf:
                        currentBlinkState = BlinkState.Idle;
                        faceMaterial.mainTextureOffset = Vector2.zero;
                        blinkTime = Time.time + Random.Range(2f, 7.5f);
                        break;
                }
            }
        }

        public void OnDestroy()
        {
            if (faceRenderer != null && originalMaterial != null)
                faceRenderer.material = originalMaterial;
        }
    }
}


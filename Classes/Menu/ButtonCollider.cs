/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using iiMenu.Managers;
using UnityEngine;
using static iiMenu.Menu.Main;

namespace iiMenu.Classes.Menu
{
    public class ButtonCollider : MonoBehaviour
	{
		public ButtonInfo targetButton;
		public string relatedText;

		public bool incremental;
		public bool positive;
		public bool interactable = true;

		private static string lastPressedName;
		private static float lastPressTime;

		private bool lastEnabledState;
		private bool hasState;

		public void Update()
		{
			if (targetButton == null || !targetButton.isTogglable) return;
			if (!hasState)
			{
				lastEnabledState = targetButton.enabled;
				hasState = true;
				return;
			}
			if (lastEnabledState != targetButton.enabled)
			{
				lastEnabledState = targetButton.enabled;
				ColorChanger changer = GetComponent<ColorChanger>();
				if (changer != null)
					changer.colors = buttonColors[swapButtonColors ^ targetButton.enabled ? 1 : 0];
				else
				{
					Renderer r = GetComponent<Renderer>();
					if (r != null)
						r.material.color = buttonColors[swapButtonColors ^ targetButton.enabled ? 1 : 0].GetCurrentColor();
				}
			}
		}

		public void OnTriggerEnter(Collider collider)
		{
			if (Time.time < buttonCooldown ||
			    (collider != buttonCollider && collider != lKeyCollider && collider != rKeyCollider) || joystickMenu ||
			    menu == null) return;

			PressFromMouse();
		}

		public void PressFromMouse()
		{
			if (!interactable || Time.time < buttonCooldown || joystickMenu || menu == null) return;
			string name = targetButton != null ? targetButton.buttonText : relatedText;
			if (name == lastPressedName && Time.time < lastPressTime + 0.25f) return;
			lastPressedName = name;
			lastPressTime = Time.time;
			buttonCooldown = Time.time + 0.2f;
			PlayButtonSound(name);

			if (annoyingMode)
			{
				if (Random.Range(1, 5) == 2)
				{
					NotificationManager.SendNotification("Try again loser >:3");
					return;
				}
			}

			if (incremental)
				ToggleIncremental(targetButton != null ? targetButton.buttonText : relatedText, positive);
			else if (targetButton != null)
				Toggle(targetButton, true);
			else
				Toggle(relatedText, true);
		}
	}
}

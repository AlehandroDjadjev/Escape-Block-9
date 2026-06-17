using System;
using System.Reflection;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FacilityEscapeExitTrigger : MonoBehaviour
    {
        private static readonly Type InventoryType = Type.GetType("SingleItemInventory, Assembly-CSharp");
        private static readonly MethodInfo HasItemIdMethod = InventoryType?.GetMethod("HasItemId", BindingFlags.Public | BindingFlags.Instance);
        private static readonly Type GameFlowUiControllerType = Type.GetType("GameFlowUIController, Assembly-CSharp");
        private static readonly MethodInfo NotifyPlayerEscapedMethod = GameFlowUiControllerType?.GetMethod("NotifyPlayerEscaped", BindingFlags.Public | BindingFlags.Static);
        private static readonly Type PickupPromptUiType = Type.GetType("PickupPromptUI, Assembly-CSharp");
        private static readonly MethodInfo PromptShowProgressMethod = PickupPromptUiType?.GetMethod("ShowProgress", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo PromptHideMethod = PickupPromptUiType?.GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
        private static readonly Type KeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
        private static readonly PropertyInfo KeyboardCurrentProperty = KeyboardType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
        private static readonly PropertyInfo KeyboardEKeyProperty = KeyboardType?.GetProperty("eKey", BindingFlags.Public | BindingFlags.Instance);

        [SerializeField] private string requiredItemId = "objective_exit_key";
        [SerializeField] private float unlockHoldSeconds = 5f;
        [SerializeField] private float interactionRadius = 2.2f;
        [SerializeField] private string missingKeyPromptText = "Requires Exit Authorization Key";
        [SerializeField] private string unlockPromptText = "Hold to unlock fire exit";

        private bool escaped;
        private float currentHoldSeconds;
        private Component promptUi;

        private void Update()
        {
            if (escaped)
            {
                HidePrompt();
                currentHoldSeconds = 0f;
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                HidePrompt();
                currentHoldSeconds = 0f;
                return;
            }

            Vector3 playerPosition = player.transform.position;
            playerPosition.y = transform.position.y;
            if (Vector3.Distance(playerPosition, transform.position) > interactionRadius)
            {
                HidePrompt();
                currentHoldSeconds = 0f;
                return;
            }

            Component inventory = InventoryType != null ? player.GetComponentInParent(InventoryType) : null;
            bool hasRequiredItem = inventory != null &&
                                   HasItemIdMethod != null &&
                                   HasItemIdMethod.Invoke(inventory, new object[] { requiredItemId }) is bool result &&
                                   result;
            if (!hasRequiredItem)
            {
                ShowStatus(missingKeyPromptText);
                currentHoldSeconds = 0f;
                return;
            }

            float progress = unlockHoldSeconds <= 0.001f ? 1f : currentHoldSeconds / unlockHoldSeconds;
            ShowProgress(progress);

            if (!IsInteractHeld())
            {
                currentHoldSeconds = 0f;
                ShowProgress(0f);
                return;
            }

            currentHoldSeconds += Time.deltaTime;
            progress = unlockHoldSeconds <= 0.001f ? 1f : Mathf.Clamp01(currentHoldSeconds / unlockHoldSeconds);
            ShowProgress(progress);

            if (currentHoldSeconds < unlockHoldSeconds)
            {
                return;
            }

            escaped = true;
            HidePrompt();
            NotifyPlayerEscapedMethod?.Invoke(null, null);
        }

        private void ShowProgress(float progress)
        {
            EnsurePromptUi();
            PromptShowProgressMethod?.Invoke(promptUi, new object[] { "E", unlockPromptText, Mathf.Clamp01(progress) });
        }

        private void ShowStatus(string message)
        {
            EnsurePromptUi();
            MethodInfo showStatus = PickupPromptUiType?.GetMethod("ShowStatus", BindingFlags.Public | BindingFlags.Instance);
            if (showStatus != null)
            {
                showStatus.Invoke(promptUi, new object[] { "E", message });
                return;
            }

            PromptShowProgressMethod?.Invoke(promptUi, new object[] { "E", message, 0f });
        }

        private void HidePrompt()
        {
            if (promptUi == null)
            {
                return;
            }

            PromptHideMethod?.Invoke(promptUi, null);
        }

        private void EnsurePromptUi()
        {
            if (promptUi != null || PickupPromptUiType == null)
            {
                return;
            }

            GameObject existing = GameObject.Find("PickupPromptUI");
            if (existing != null)
            {
                promptUi = existing.GetComponent(PickupPromptUiType);
                if (promptUi != null)
                {
                    return;
                }
            }

            GameObject uiObject = new GameObject("PickupPromptUI");
            promptUi = uiObject.AddComponent(PickupPromptUiType);
        }

        private static bool IsInteractHeld()
        {
            if (TryReadInputSystemEKey(out bool inputSystemPressed))
            {
                return inputSystemPressed;
            }

            try
            {
                return Input.GetKey(KeyCode.E);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool TryReadInputSystemEKey(out bool pressed)
        {
            pressed = false;
            if (KeyboardCurrentProperty == null || KeyboardEKeyProperty == null)
            {
                return false;
            }

            object keyboard = KeyboardCurrentProperty.GetValue(null);
            if (keyboard == null)
            {
                return true;
            }

            object eKey = KeyboardEKeyProperty.GetValue(keyboard);
            PropertyInfo isPressedProperty = eKey?.GetType().GetProperty("isPressed", BindingFlags.Public | BindingFlags.Instance);
            if (isPressedProperty == null)
            {
                return false;
            }

            pressed = isPressedProperty.GetValue(eKey) is bool value && value;
            return true;
        }
    }
}

using HarmonyLib;
using Il2CppEekCharacterEngine;
using Il2CppEekCharacterEngine.Interaction;
using UnityEngine;

namespace Freecam
{
    [HarmonyPatch(typeof(InteractionManager), "FixedUpdate")]
    internal static class Patcher
    {
        internal static float addedDistance = 2.3f;
        internal static float maxDistance = 3f + addedDistance;
        //private static readonly int PhysicsLayerMask = LayerMask.GetMask("InteractiveItems", "Character", "Walls", "Ground", "Default");

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called via reflection")]
        private static bool Prefix()
        {
            if (InteractionManager.Singleton is null
                || InteractionManager.Singleton.Text is null
                || InteractionManager.Singleton.Image is null
                || PlayerCharacter.Player is null
                || Camera.main is null
                || Camera.main.transform is null
                || FFreecam.ThirdPersonMode is null
                || (!FFreecam.ThirdPersonMode.Value && FFreecam.Enabled))
            {
                return true;
            }

            if (InteractionManager.Singleton.Text.color != InteractionManager.Singleton._originalColor)
            {
                InteractionManager.Singleton.Image.rectTransform.localScale = new Vector3(InteractionManager.Singleton._desiredSize.x / (InteractionManager.Singleton.Text.color.g * 1.33f), InteractionManager.Singleton._desiredSize.y / (InteractionManager.Singleton.Text.color.g * 1.33f), 0);
                InteractionManager.Singleton.Image.color = InteractionManager.Singleton.Text.color;
                //MelonLogger.Msg("set text color");
            }
            else
            {
                InteractionManager.Singleton.Image.rectTransform.localScale = InteractionManager.Singleton._desiredSize;
                InteractionManager.Singleton.Image.color = InteractionManager.Singleton._desiredColor;
                // MelonLogger.Msg("set desiredcolor");
            }

            InteractionManager.Singleton.ResetAllFocusTargets();
            //MelonLogger.Msg("reset focus");

            Camera.main.transform.get_position_Injected(out Vector3 pos);
            RaycastHit hit = new();
            if (PlayerCharacter.Player.GetProperty(Il2CppEekEvents.InteractiveProperties.PlayerCombatMode))
            {
                if (!InteractionManager.Singleton.GetInCombatRayCastHit(new(pos, Camera.main.transform.forward), out _))
                {
                    return false;
                }
            }
            else
            {
                if (!Physics.Raycast(pos, Camera.main.transform.forward, out hit, maxDistance, InteractionManager.Singleton._primaryIMgrMask))
                {
                    return false;
                }
            }

            Collider? collider = hit.collider;
            Transform? transform = hit.transform;
            if (collider is null
                || !collider.enabled
                || collider.gameObject is null
                || transform is null)
            {
                return false;
            }

            InteractionManager.Singleton._hit = hit;
            //MelonLogger.Msg("set hit");
            InteractiveItem? interactive = null;
            //MelonLogger.Msg(InteractionManager.Singleton._hit.transform?.name ?? "none");

            if (collider.gameObject.layer == LayerMask.NameToLayer("InteractiveItems")
                || transform.gameObject.layer == LayerMask.NameToLayer("InteractiveItemsHighlighted")
                || transform.gameObject.CompareTag("Door")
                || transform.gameObject.CompareTag("DoNotAvoid"))
            {
                DistractableRigidItem? item = collider.gameObject.GetComponent<DistractableRigidItem>();
                bool isNotParent = item is not null;
                item ??= collider.gameObject.GetComponentInChildren<DistractableRigidItem>();

                if (item is not null && hit.distance <= maxDistance - 1)
                {
                    InteractionManager.Singleton.CurrentFocusedItem = item;
                    //MelonLogger.Msg("set item");

                    interactive = item.TryCast<InteractiveItem>();

                    if (interactive is null)
                    {
                        interactive = ItemManager.GetItemByGameObjectName(collider.gameObject.name);
                        if (interactive is null)
                        {
                            return false;
                        }
                    }

                    if ((!isNotParent && !interactive.AllowChildrenToTriggerInteraction)
                        || !interactive.ShouldInteract()
                        || hit.distance > interactive.DistanceToInteraction + 3)
                    {
                        return false;
                    }
                }
            }
            else if (transform.gameObject.layer == LayerMask.NameToLayer("Ragdolls"))
            {
                //MelonLogger.Msg("Maybe NPC");
                CharacterInteraction? npc = transform.gameObject.GetComponent<CharacterInteraction>();
                npc ??= transform.gameObject.GetComponentInChildren<CharacterInteraction>();
                npc ??= transform.gameObject.GetComponentInParent<CharacterInteraction>();

                Transform t = transform;
                while (t.parent is not null)
                {
                    npc ??= t.parent.gameObject.GetComponentInChildren<CharacterInteraction>();
                    if (npc is not null)
                        break;
                    t = t.parent;
                }

                if (npc is null
                    || hit.distance > npc.DistanceToInteraction + 3)
                {
                    //MelonLogger.Msg($"{npc} {hit.distance} {npc?.DistanceToInteraction.ToString() ?? "npc is null"}");
                    return false;
                }

                interactive = npc;
                //MelonLogger.Msg("set npc");

                if (collider.name is "abdomenUpper" or "abdomenLower" or "lShldrBend" or "rShldrBend" or "abdomen2" or "lShldr" or "rShldr")
                {
                    InteractionManager.Singleton.CurrentFocusedCharacterChest = npc._character;
                    //MelonLogger.Msg("set char chest");
                }
                else if (collider.name is "rThighBend" or "lThighBend" or "lThigh" or "rThigh")
                {
                    InteractionManager.Singleton.CurrentFocusedCharacterButt = npc._character;
                    //MelonLogger.Msg("set char butt");
                }
            }

            InteractionManager.Singleton._focusedItemInteractionPoint = hit.point;
            InteractionManager.Singleton._focusedItemInteraction = interactive;
            //MelonLogger.Msg("set hit points");

            if (interactive is null
                || !interactive.HasInteractions(GameManager._activeStory))
            {
                return false;
            }

            string? text = TranslationManager.TranslateByText(interactive.DisplayName);
            if (text != InteractionManager.Singleton._goalText)
            {
                InteractionManager.Singleton.ResetNameDelay();
                //MelonLogger.Msg("reset name delay");
            }

            InteractionManager.Singleton._goalText = text;
            //MelonLogger.Msg("set text");

            if (text is not null
                && text != string.Empty
                && InteractionManager.Singleton._currentCharacter < text.Length)
            {
                InteractionManager.Singleton._currentCharacter++;
                //MelonLogger.Msg("advanced character");
            }

            return false;
        }
    }
}
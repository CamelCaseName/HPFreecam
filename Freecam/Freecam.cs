using Il2CppCinemachine;
using Il2CppEekCharacterEngine;
using Il2CppInterop.Runtime;
using MelonLoader;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using Il2CppException = Il2CppInterop.Runtime.Il2CppException;
using Object = UnityEngine.Object;

namespace Freecam;

public class Freecam : MelonMod
{
    private FFreecam? freecam;

#if DEBUG
    public override void OnGUI()
    {
        freecam?.OnGUI();
    }

    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Debug build of the freecam!");
    }
#endif

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        freecam = new FFreecam(sceneName);
    }

    public override void OnUpdate()
    {
        freecam?.OnUpdate();

#if DEBUG
        if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "dKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
        {
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                ObjectInfo.PrintHierarchy(item.gameObject);
            }
        }
#endif
    }
}

//yoink this class if you need a freecam
//or just use the mod alongside yours 
//TODO add UI with coord view, and layer selector
//todo add options to add new objects, clone oibjects, move objects with UI and with camera movement (ray to next object, and fixed distance from cam)
internal class FFreecam
{
    private const float defaultSpeed = 2.3f;
    private readonly GUILayoutOption[] Opt = Array.Empty<GUILayoutOption>();
    private readonly float rotRes = 0.15f;
    private Camera? camera = null;
    private Camera? game_camera = null;
    private bool inCamera = true;
    private bool gameCameraHidden = false;
    private readonly bool inGameMain = false;
    private bool isEnabled = false;
    private bool isInitialized = false;
    private Il2CppEekAddOns.HousePartyPlayerCharacter? player = null;
    private float rotX = 0f;
    private float rotY = 0f;
    private bool showUI = false;
    private float speed = defaultSpeed;
    private Rect uiPos = new(10, Screen.height * 0.3f, Screen.width * 0.3f, Screen.height * 0.2f);
    public FFreecam(string sceneName)
    {
        foreach (var item in Object.FindObjectsOfType<Camera>())
        {
            if (Il2CppSupport.GetProperty<string, Camera>(item, "name") == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
            {
                game_camera = item;
                Initialize();
                break;
            }
        }
        inGameMain = sceneName == "GameMain";
    }

    public bool Enabled => isEnabled;

    public void OnGUI()
    {
        //update ui
        DisplayUI();
    }

    public void OnUpdate()
    {
        //toggle freecam with alt+f
        CheckForToggle();

        //update position and so on
        Update();
    }

    public void SetDisabled()
    {
        isEnabled = false;
        if (camera is not null)
            camera.enabled = false;

        Screen.lockCursor = false;

        //move cameras back
        foreach (var item in Object.FindObjectsOfType<Camera>())
        {
            string name = $"{item.gameObject.name}";
            if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
            {
                //MelonLogger.Msg($"Moved {item.name} back to fullscreen.");
                item.rect = new Rect(0, 0, 1, 1);
                item.enabled = true;
            }
        }

        if (player != null)
        {
            TryImmobilizePlayer(false);
        }

        MelonLogger.Msg("Freecam disabled.");
    }

    private void TryImmobilizePlayer(bool immobile)
    {
        if (player is null) return;
        Il2CppSupport.SetProperty<bool, Character>(player, nameof(player.IsImmobile), immobile);
        if (immobile)
            player._controlManager.PlayerInput.DeactivateInput();
        else
            player._controlManager.PlayerInput.ActivateInput();
    }

    public void SetEnabled()
    {
        if (camera == null || !isInitialized)
        {
            //MelonLogger.Msg("our camera was null");
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                string name = $"{item.gameObject.name}";
                int count = item.gameObject.GetComponents<MonoBehaviour>().Count;
                if (name == "Camera" && count > 0)
                {
                    //MelonLogger.Msg("got a camera");
                    game_camera = item;
                    break;
                }
            }
            if (!Initialize()) return;
        }
        if (camera != null)
            camera.enabled = true;

        //MelonLogger.Msg("moving screens");
        //move cameras to top left
        foreach (var item in Object.FindObjectsOfType<Camera>())
        {
            string name = $"{item.gameObject.name}";
            if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
            {
                //MelonLogger.Msg($"Moved {item.name} to the top left.");
                item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
            }
        }

        if (inGameMain)
        {
            player = Object.FindObjectOfType<Il2CppEekAddOns.HousePartyPlayerCharacter>();
            TryImmobilizePlayer(true);
        }
        else
        {
            Screen.lockCursor = true;
        }

        isEnabled = true;
        MelonLogger.Msg("Freecam enabled.");
    }

    //todo, use input system
    public void UpdateMovement()
    {
        //only move when we are not moving the player
        if (isEnabled && inCamera && camera is not null)
        {
            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftShiftKey", "isPressed"))
                speed = defaultSpeed * 2.5f;
            else
                speed = defaultSpeed;

            float dTime = Time.deltaTime;

            //game_camera?.transform.Translate(Vector3.forward * speed * dTime, Space.Self);

            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "wKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.forward * speed * dTime, Space.Self);
            }
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "sKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.back * speed * dTime, Space.Self); ;
            }

            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "aKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.left * speed * dTime, Space.Self);
            }
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "dKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.right * speed * dTime, Space.Self);
            }

            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "spaceKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.up * speed * dTime, Space.Self);
            }
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftCtrlKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.down * speed * dTime, Space.Self);
            }

            //does not work in game when the player is loaded
            if (inGameMain && player is not null)
            {
                var value = player._controlManager.GetLookValue();
                rotY += value.x * 0.8f;
                rotX -= value.y * 0.8f;
                MelonLogger.Msg($"{rotX}|{rotY}");
            }
            else
            {
                rotY += Mouse.current.delta.ReadValue().x;
                rotX -= Mouse.current.delta.ReadValue().y;
            }

            camera.transform.get_rotation_Injected(out var oldRotation);
            var newRotation = Quaternion.Lerp(oldRotation, Quaternion.Euler(new Vector3(rotRes * rotX, rotRes * rotY, 0)), 50 * Time.deltaTime);
            GCHandle pinnedRotation = GCHandle.Alloc(newRotation, GCHandleType.Pinned);
            camera.transform.set_rotation_Injected(ref newRotation);
            pinnedRotation.Free();

        }
        else if (isInitialized && !isEnabled && camera is not null && player is not null)
        {
            player.Head.transform.get_position_Injected(out var playerPos);
            GCHandle pinnedPos = GCHandle.Alloc(playerPos, GCHandleType.Pinned);
            camera.transform.set_position_Injected(ref playerPos);
            pinnedPos.Free();
        }
    }

    private void CheckForToggle()
    {
        if (Il2CppSupport.GetProperty<bool, KeyControl>(Keyboard.current, "fKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
        {
            //MelonLogger.Msg("toggling");
            if (Enabled)
            {
                SetDisabled();
            }
            else
            {
                SetEnabled();
            }
        }
        if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "uKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
        {
            showUI = !showUI;
        }
    }

    private void DisplayUI()
    {
        if (isEnabled && inGameMain && showUI && player is not null && camera is not null && game_camera is not null)
        {
            GUILayout.BeginArea(uiPos);
            GUILayout.BeginVertical(Opt);
            GUILayout.Label(
                $"Player pos ({player.transform.position.x:0.00}|{player.transform.position.y:0.00}|{player.transform.position.z:0.00})" +
                $" Freecam pos ({camera.transform.position.x:0.00}|{camera.transform.position.y:0.00}|{camera.transform.position.z:0.00})", Opt);
            GUILayout.Label(
                $"Player rot ({player.transform.rotation.eulerAngles.x:0.00}|{player.transform.rotation.eulerAngles.y:0.00}|{player.transform.rotation.eulerAngles.z:0.00})" +
                $" Freecam rot ({camera.transform.rotation.eulerAngles.x:0.00}|{camera.transform.rotation.eulerAngles.y:0.00}|{camera.transform.rotation.eulerAngles.z:0.00})", Opt);

            GUILayout.Label(
                $"Player movement speed ({game_camera.velocity.x:0.00}|{game_camera.velocity.y:0.00}|{game_camera.velocity.z:0.00})" +
                $" Freecam speed ({camera.velocity.x:0.00}|{camera.velocity.y:0.00}|{camera.velocity.z:0.00})", Opt);
            string lookingAt = "None";
            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hit, float.MaxValue))
            {
                lookingAt = hit.transform.gameObject.name;
            }
            GUILayout.Label($"Freecam looking at {lookingAt}", Opt);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

    private bool Initialize()
    {
        //MelonLogger.Msg("initializing");
        if (game_camera != null)
        {
            //MelonLogger.Msg("yoinking games camera");
            //ObjectInfo.PrintHierarchy(game_camera.gameObject);
            camera = Object.Instantiate(game_camera.gameObject).GetComponent<Camera>();
            //MelonLogger.Msg($"{camera.name}");
            camera.depth = -2;
            //MelonLogger.Msg($"{camera.depth}");
            //camera.cullingMask |= 1 << 18; //see head
            camera.cullingMask |= ~0; //see all
            //MelonLogger.Msg($"{camera.cullingMask}");
            camera.name = "Second Camera";
            //MelonLogger.Msg($"{camera.name}");
            camera.enabled = false;
            //MelonLogger.Msg($"{camera.enabled}");
            camera.gameObject.layer = 3; //0 is default
            //MelonLogger.Msg($"{camera.gameObject.layer}");
            camera.cameraType = CameraType.Game;
            //MelonLogger.Msg($"{camera.cameraType}");
            camera.hideFlags = HideFlags.HideAndDontSave;
            var rect = new Rect(0f, 0f, 1f, 1f);
            var handle = GCHandle.Alloc(rect, GCHandleType.Pinned);
            camera.set_rect_Injected(ref rect);//starting left, bottom, extend up, right
            handle.Free();
            //MelonLogger.Msg($"{camera.hideFlags}");

            for (int i = 0; i < camera.transform.childCount; i++)
            {
                var child = camera.transform.GetChild(i);
                if (child.gameObject != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            Object.DestroyImmediate(camera.gameObject.GetComponent<CinemachineBrain>());

            isInitialized = camera.gameObject.GetComponents<MonoBehaviour>().Count > 0;
            //ObjectInfo.PrintHierarchy(camera.gameObject);
            //MelonLogger.Msg("our own camera was initialized");
            return true;
        }
        return false;
    }

    private void Update()
    {
        //run freecam
        if (isEnabled && camera is not null)
        {
            //only concern about two cameras at once when in game main
            if (inGameMain && player is not null)
            {
                if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && inCamera)
                {
                    inCamera = false;
                    TryImmobilizePlayer(false);
                    MelonLogger.Msg("Control moved to player.");
                }
                else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    inCamera = true;
                    TryImmobilizePlayer(true);
                    MelonLogger.Msg("Control moved to the freecam.");
                }
                if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "vKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    player.Head.transform.get_position_Injected(out var playerPos);
                    GCHandle pinnedPos = GCHandle.Alloc(playerPos, GCHandleType.Pinned);
                    camera.transform.set_position_Injected(ref playerPos);
                    pinnedPos.Free();
                    player.Head.transform.get_rotation_Injected(out var playerRot);
                    GCHandle pinnedRot = GCHandle.Alloc(playerRot, GCHandleType.Pinned);
                    camera.transform.set_rotation_Injected(ref playerRot);
                    pinnedRot.Free();
                    MelonLogger.Msg("Freecam teleported to the player's head.");
                }
            }
            else
            {
                if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && inCamera)
                {
                    inCamera = false;
                    Screen.lockCursor = false;
                    MelonLogger.Msg("Control moved to the UI menu.");
                }
                else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    inCamera = true;
                    Screen.lockCursor = true;
                    MelonLogger.Msg("Control moved to the freecam.");
                }
            }
            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "hKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && !gameCameraHidden)
            {
                gameCameraHidden = true;
                foreach (var item in Object.FindObjectsOfType<Camera>())
                {
                    string name = $"{item.gameObject.name}";
                    if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                    {
                        item.enabled = false;
                    }
                }
            }
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "hKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
            {
                gameCameraHidden = false;
                foreach (var item in Object.FindObjectsOfType<Camera>())
                {
                    string name = $"{item.gameObject.name}";
                    if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                    {
                        item.enabled = true;
                    }
                }

            }
            UpdateMovement();
        }
    }
}

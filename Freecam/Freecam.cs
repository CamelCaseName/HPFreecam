using Il2CppCinemachine;
using Il2CppInterop.Runtime;
using MelonLoader;
using System;
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
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Debug build of the freecam!");
    }
#endif

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        freecam = new FFreecam();
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

    public override void OnGUI()
    {
        freecam?.OnGUI();
    }
}

//yoink this class if you need a freecam
//or just use the mod alongside yours 
//TODO add UI with coord view, and layer selector
//todo add options to add new objects, clone oibjects, move objects with UI and with camera movement (ray to next object, and fixed distance from cam)
internal class FFreecam
{
    private readonly float rotRes = 0.15f;
    private const float defaultSpeed = 2.3f;
    private float speed = defaultSpeed;
    private Camera? camera = null;
    private Camera? game_camera = null;
    private bool inCamera = true;
    private bool showUI = false;
    private bool inGameMain = false;
    private bool isEnabled = false;
    private bool isInitialized = false;
    private Rect uiPos = new(10, Screen.height * 0.3f, Screen.width * 0.3f, Screen.height * 0.2f);
    private readonly GUILayoutOption[] Opt = Array.Empty<GUILayoutOption>();
    private Il2CppEekAddOns.HousePartyPlayerCharacter? player = null;
    private float rotX = 0f;
    private float rotY = 0f;
    private Object? controller = null;
    private IntPtr nativePlayerControllerGetLookValuePointer = IntPtr.Zero;

    public FFreecam()
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
        //initlialize the lookvalue stuff
        _ = PlayerController_GetLookValue();
    }

    private unsafe Vector2 PlayerController_GetLookValue()
    {
        var scene = SceneManager.GetActiveScene();
        if (Il2CppSupport.GetProperty<string, Scene>(scene.BoxIl2CppObject(), "name") != "GameMain") return Vector2.zero;
        IntPtr* parameterArray = null;
        IntPtr nativeException = IntPtr.Zero;

        if (controller == null || nativePlayerControllerGetLookValuePointer == IntPtr.Zero)
        {
            var nativeClass = IL2CPP.GetIl2CppClass("EekCharacterEngine.dll", "", "PlayerControlManager");
            nativePlayerControllerGetLookValuePointer = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, "GetLookValue", 0);
            controller = Object.FindObjectOfType(Il2CppType.TypeFromPointer(nativeClass, "PlayerControlManager"));
        }

        var nativeController = IL2CPP.Il2CppObjectBaseToPtrNotNull(controller);
        IntPtr resultVector2 = IL2CPP.il2cpp_runtime_invoke(nativePlayerControllerGetLookValuePointer, nativeController, (void**)parameterArray, ref nativeException);

        Il2CppException.RaiseExceptionIfNecessary(nativeException);
        return *(Vector2*)IL2CPP.il2cpp_object_unbox(resultVector2);
    }

    public bool Enabled => isEnabled;

    public void OnUpdate()
    {
        //toggle freecam with alt+f
        CheckForToggle();

        //update position and so on
        Move();
    }

    public void OnGUI()
    {
        //update ui
        DisplayUI();
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
        MelonLogger.Msg("initializing");
        if (game_camera != null)
        {
            MelonLogger.Msg("yoinking games camera");
            //ObjectInfo.PrintHierarchy(game_camera.gameObject);
            camera = Object.Instantiate(game_camera.gameObject).GetComponent<Camera>();
            camera.depth = -2;
            //camera.cullingMask |= 1 << 18; //see head
            camera.cullingMask |= ~0; //see all
            camera.name = "Second Camera";
            camera.enabled = false;
            camera.gameObject.layer = 3; //0 is default
            camera.cameraType = CameraType.Game;
            camera.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < camera.transform.GetChildCount(); i++)
            {
                if (camera.transform.GetChild(i).gameObject != null) Object.DestroyImmediate(camera.transform.GetChild(i).gameObject);
            }

            Object.DestroyImmediate(camera.gameObject.GetComponent<CinemachineBrain>());

            isInitialized = camera.gameObject.transform.gameObject.GetComponents<MonoBehaviour>().Count > 0;
            //ObjectInfo.PrintHierarchy(camera.gameObject);
            MelonLogger.Msg("our own camera was initialized");
            return true;
        }
        return false;
    }

    private void CheckForToggle()
    {
        if (Il2CppSupport.GetProperty<bool, KeyControl>(Keyboard.current, "fKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
        {
            MelonLogger.Msg("toggling");
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

    private void Move()
    {
        //run freecam
        if (isEnabled && player is not null && camera is not null)
        {
            //only concern about two cameras at once when in game main
            if (inGameMain)
            {
                if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && inCamera)
                {
                    inCamera = false;
                    player.IsImmobile = false;
                    MelonLogger.Msg("Control moved to player.");
                }
                else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    inCamera = true;
                    player.IsImmobile = true;
                    MelonLogger.Msg("Control moved to the freecam.");
                }
                if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "vKey", "wasPressedThisFrame") && Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    camera.transform.position = player.Head.transform.position;
                    camera.transform.rotation = player.Head.transform.rotation;
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
            Update();
        }
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
            string name = Il2CppSupport.GetProperty<string, Camera>(item, "name");
            if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
            {
                //MelonLogger.Msg($"Moved {item.name} back to fullscreen.");
                item.rect = new Rect(0, 0, 1, 1);
            }
        }

        if (player != null)
        {
            player.IsImmobile = false;
        }

        MelonLogger.Msg("Freecam disabled.");
    }

    public void SetEnabled()
    {
        if (camera == null || !isInitialized)
        {
            MelonLogger.Msg("our camera was null");
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (Il2CppSupport.GetProperty<string, Camera>(item, "name") == "camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
                {
                    MelonLogger.Msg("got a camera");
                    game_camera = item;
                    break;
                }
            }
            if (!Initialize()) return;
        }
        else
        {
            camera.enabled = true;
        }
        //move cameras to top left
        foreach (var item in Object.FindObjectsOfType<Camera>())
        {
            string name = Il2CppSupport.GetProperty<string, Camera>(item, "name");
            if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
            {
                //MelonLogger.Msg($"Moved {item.name} to the top left.");
                item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);//starting left, bottom, extend up, right
                break;
            }
        }

        Screen.lockCursor = true;

        inGameMain = Il2CppSupport.GetProperty<string, Scene>(SceneManager.GetActiveScene().BoxIl2CppObject(), "name") == "GameMain";
        if (inGameMain)
        {
            player = Object.FindObjectOfType<Il2CppEekAddOns.HousePartyPlayerCharacter>();
            player.IsImmobile = true;
        }

        isEnabled = true;
        MelonLogger.Msg("Freecam enabled.");
    }

    //todo, use input system
    public void Update()
    {
        //only move when we are not moving the player
        if (isEnabled && inCamera && camera is not null)
        {
            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftShiftKey", "isPressed"))
                speed = defaultSpeed * 2.5f;
            else
                speed = defaultSpeed;

            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "wKey", "isPressed"))
                camera.transform.position += camera.transform.forward * speed * Time.deltaTime;
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "sKey", "isPressed"))
                camera.transform.position -= camera.transform.forward * speed * Time.deltaTime;

            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "aKey", "isPressed"))
                camera.transform.position -= camera.transform.right * speed * Time.deltaTime;
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "dKey", "isPressed"))
                camera.transform.position += camera.transform.right * speed * Time.deltaTime;

            if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "spaceKey", "isPressed"))
                camera.transform.position += camera.transform.up * speed * Time.deltaTime;
            else if (Il2CppSupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftCtrlKey", "isPressed"))
                camera.transform.position -= camera.transform.up * speed * Time.deltaTime;

            //does not work in game when the player is loaded
            if (inGameMain)
            {
                var value = PlayerController_GetLookValue();
                rotY += value.x * 0.8f;
                rotX -= value.y * 0.8f;
            }
            else
            {
                rotY += Mouse.current.delta.ReadValue().x;
                rotX -= Mouse.current.delta.ReadValue().y;
            }

            camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, Quaternion.Euler(new Vector3(rotRes * rotX, rotRes * rotY, 0)), 50 * Time.deltaTime);
        }
        else if (isInitialized && camera is not null && player is not null)
        {
            camera.transform.position = player.Head.transform.position;
        }
    }
}

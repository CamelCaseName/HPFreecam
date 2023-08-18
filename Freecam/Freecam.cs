using Il2CppCinemachine;
using Il2CppSystem;
using MelonLoader;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Freecam;

public class Freecam : MelonMod
{
    private FFreecam? freecam;

    public override void OnGUI()
    {
        freecam?.OnGui();
    }

#if DEBUG
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
        if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "dKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
        {
            
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
    //todo add option to copy patricks camera to the freecam lol
    private bool gameCameraHidden = false;
    private bool inCamera = true;
    private bool isEnabled = false;
    private bool isInitialized = false;
    private bool showUI = false;
    private Camera? camera = null;
    private Camera? game_camera = null;
    private const float defaultSpeed = 2.3f;
    private float rotX = 0f;
    private float rotY = 0f;
    private float speed = defaultSpeed;
    private Il2CppEekAddOns.HousePartyPlayerCharacter? player = null;
    private readonly bool inGameMain = false;
    private readonly Canvas myCanvas;
    private readonly float rotRes = 0.15f;
    private readonly Font ourFont = Font.GetDefault();
    private readonly GameObject myGO = new();
    private readonly GameObject myText;
    private readonly RectTransform rectTransform;
    private readonly Text text;
    private DateTime lastImmobilizedPlayer;

    public FFreecam(string sceneName)
    {
        foreach (var item in Object.FindObjectsOfType<Camera>())
        {
            if (HousePartySupport.GetProperty<string, Camera>(item, "name") == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
            {
                game_camera = item;
                Initialize();
                break;
            }
        }
        inGameMain = sceneName == "GameMain";

        // Canvas
        myGO = new();
        HousePartySupport.SetProperty(myGO, nameof(myGO.name), "Freecam UI");

        myCanvas = myGO.AddComponent<Canvas>();
        HousePartySupport.SetProperty(myCanvas, nameof(myCanvas.renderMode), (int)RenderMode.ScreenSpaceOverlay);
        myGO.AddComponent<CanvasScaler>();

        // Text
        myText = new GameObject();
        HousePartySupport.SetProperty(myText, nameof(myText.name), "Freecam UI Container");
        myText.transform.parent = myGO.transform;

        text = myText.AddComponent<Text>();
        HousePartySupport.SetProperty(text, nameof(text.font), ourFont);

        // Text position
        rectTransform = text.GetComponent<RectTransform>();
        rectTransform.localPosition = new Vector3(0, 0, 0);
        rectTransform.anchoredPosition = new(0, 0);
        var screenSize = Screen.currentResolution;
        rectTransform.sizeDelta = new Vector2(screenSize.width, screenSize.height);
    }

    public bool Enabled => isEnabled;

    public void OnUpdate()
    {
        //toggle freecam with alt+f
        CheckForToggle();

        //update position and so on
        Update();
    }

    public void OnGui() => DisplayUI();

    public void SetDisabled()
    {
        isEnabled = false;
        inCamera = false;
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
            TryImmobilizePlayer();
        }

        MelonLogger.Msg("Freecam disabled.");
    }

    private void TryImmobilizePlayer()
    {
        if (player is null) return;
#if DEBUG
        if (inCamera)
            player._controlManager.PlayerInput.DeactivateInput();
        else
            player._controlManager.PlayerInput.ActivateInput();
#else
        if (inCamera)
            player.field_Private_PlayerControlManager_0.PlayerInput.DeactivateInput();
        else
            player.field_Private_PlayerControlManager_0.PlayerInput.ActivateInput();
#endif
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

        inCamera = true;
        if (inGameMain)
        {
            player = Object.FindObjectOfType<Il2CppEekAddOns.HousePartyPlayerCharacter>();
            TryImmobilizePlayer();
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
            if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftShiftKey", "isPressed"))
                speed = defaultSpeed * 2.5f;
            else
                speed = defaultSpeed;

            float dTime = Time.deltaTime;

            //game_camera?.transform.Translate(Vector3.forward * speed * dTime, Space.Self);

            if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "wKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.forward * speed * dTime, Space.Self);
            }
            else if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "sKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.back * speed * dTime, Space.Self); ;
            }

            if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "aKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.left * speed * dTime, Space.Self);
            }
            else if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "dKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.right * speed * dTime, Space.Self);
            }

            if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "spaceKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.up * speed * dTime, Space.Self);
            }
            else if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftCtrlKey", "isPressed"))
            {
                camera.transform.Translate(Vector3.down * speed * dTime, Space.Self);
            }

            var value = Mouse.current.delta.ReadValue();
            rotY += value.x;
            rotX -= value.y;

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
        if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "fKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
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
        if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "uKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
        {
            showUI = !showUI;
            MelonLogger.Msg("UI " + (showUI ? "enabled" : "disabled"));
            DisplayUI();
        }
    }

    private void DisplayUI()
    {
        string toDisplay = string.Empty;
        if (showUI && camera is not null && game_camera is not null)
        {
            string lookingAt = "None";
            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hit, float.MaxValue))
                lookingAt = $"{hit.transform.gameObject.name}";

            if (player is null)
            {
                var mousePos = Mouse.current.position.ReadValue();
                var mouseDelta = Mouse.current.delta.ReadValue();
                toDisplay = $"Mouse position ({mousePos.x}|{mousePos.y}) delta: ({mouseDelta.x}|{mouseDelta.y})\n" +
                    $" Freecam position ({camera.transform.position.x:0.00}|{camera.transform.position.y:0.00}|{camera.transform.position.z:0.00})\n" +
                    $" Freecam rotation ({camera.transform.rotation.eulerAngles.x:0.00}|{camera.transform.rotation.eulerAngles.y:0.00}|{camera.transform.rotation.eulerAngles.z:0.00})\n" +
                    $" Freecam speed ({camera.velocity.x:0.00}|{camera.velocity.y:0.00}|{camera.velocity.z:0.00})\n" +
                    $"Freecam looking at {lookingAt}";
            }
            else
            {
                toDisplay = $"Player position ({player.transform.position.x:0.00}|{player.transform.position.y:0.00}|{player.transform.position.z:0.00})\n" +
                    $" Freecam position ({camera.transform.position.x:0.00}|{camera.transform.position.y:0.00}|{camera.transform.position.z:0.00})\n" +
                    $"Player rotation ({player.transform.rotation.eulerAngles.x:0.00}|{player.transform.rotation.eulerAngles.y:0.00}|{player.transform.rotation.eulerAngles.z:0.00})\n" +
                    $" Freecam rotation ({camera.transform.rotation.eulerAngles.x:0.00}|{camera.transform.rotation.eulerAngles.y:0.00}|{camera.transform.rotation.eulerAngles.z:0.00})\n" +
                    $"Player movement speed ({game_camera.velocity.x:0.00}|{game_camera.velocity.y:0.00}|{game_camera.velocity.z:0.00})\n" +
                    $" Freecam speed ({camera.velocity.x:0.00}|{camera.velocity.y:0.00}|{camera.velocity.z:0.00})\n" +
                    $"Freecam looking at {lookingAt}";
            }
        }
        HousePartySupport.SetProperty(text, nameof(text.text), toDisplay);
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
                if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && inCamera)
                {
                    inCamera = false;
                    TryImmobilizePlayer();
                    MelonLogger.Msg("Control moved to player.");
                }
                else if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    inCamera = true;
                    TryImmobilizePlayer();
                    MelonLogger.Msg("Control moved to the freecam.");
                }
                else if (inCamera && (DateTime.Now - lastImmobilizedPlayer).Seconds > 3)
                {
                    TryImmobilizePlayer();
                    lastImmobilizedPlayer = DateTime.Now;
                }
                if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "vKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
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
                if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && inCamera)
                {
                    inCamera = false;
                    Screen.lockCursor = false;
                    MelonLogger.Msg("Control moved to the UI menu.");
                }
                else if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "gKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
                {
                    inCamera = true;
                    Screen.lockCursor = true;
                    MelonLogger.Msg("Control moved to the freecam.");
                }
            }
            if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "hKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed") && !gameCameraHidden)
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
            else if (HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "hKey", "wasPressedThisFrame") && HousePartySupport.GetProperty<bool, Keyboard>(Keyboard.current, "leftAltKey", "isPressed"))
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
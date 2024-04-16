using HPUI;
using Il2Cpp;
using Il2CppCinemachine;
using Il2CppEekCharacterEngine;
using Il2CppInterop.Runtime;
using MelonLoader;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DateTime = System.DateTime;
using Object = UnityEngine.Object;

namespace Freecam;

public class Freecam : MelonMod
{
    private FFreecam? freecam;

    static Freecam()
    {
        SetOurResolveHandlerAtFront();
    }

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
        MelonPreferences.Save();
    }

    public override void OnUpdate()
    {
        freecam?.OnUpdate();

#if DEBUG
        if (Keyboard.current.dKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
        {

        }
#endif
    }
    private static Assembly AssemblyResolveEventListener(object sender, System.ResolveEventArgs args)
    {
        if (args is null) return null!;

        var name = "Freecam.Resources." + args.Name[..args.Name.IndexOf(',')] + ".dll";
        using Stream? str = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (str is not null)
        {
            var context = new AssemblyLoadContext(name, false);
            MelonLogger.Warning($"Loaded {args.Name} from our embedded resources, saving to userlibs for next time");
            string path = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly()?.Location!)!.Parent!.FullName, "UserLibs", args.Name[..args.Name.IndexOf(',')] + ".dll");
            foreach (var field in typeof(Properties.Resources).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.Name == args.Name[..args.Name.IndexOf(',')])
                {
                    File.WriteAllBytes(path, (byte[])field.GetValue(null)!);

                    return context.LoadFromStream(str);
                }
            }
        }
        return null!;
    }
    private static void SetOurResolveHandlerAtFront()
    {
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo? field = null;

        System.Type domainType = typeof(AssemblyLoadContext);

        while (field is null)
        {
            if (domainType is not null)
            {
                field = domainType.GetField("AssemblyResolve", flags);
            }
            else
            {
                MelonLogger.Error("domainType got set to null for the AssemblyResolve event was null");
                return;
            }
            if (field is null)
                domainType = domainType.BaseType!;
        }

        System.MulticastDelegate resolveDelegate = (System.MulticastDelegate)field.GetValue(null)!;
        System.Delegate[] subscribers = resolveDelegate.GetInvocationList();

        System.Delegate currentDelegate = resolveDelegate;
        for (int i = 0; i < subscribers.Length; i++)
            currentDelegate = System.Delegate.RemoveAll(currentDelegate, subscribers[i])!;

        System.Delegate[] newSubscriptions = new System.Delegate[subscribers.Length + 1];
        newSubscriptions[0] = (System.ResolveEventHandler)AssemblyResolveEventListener!;
        System.Array.Copy(subscribers, 0, newSubscriptions, 1, subscribers.Length);

        currentDelegate = System.Delegate.Combine(newSubscriptions)!;

        field.SetValue(null, currentDelegate);
    }
}

//yoink this class if you need a freecam
//or just use the mod alongside yours 
//todo add options to add new objects, clone oibjects, move objects with UI and with camera movement (ray to next object, and fixed distance from cam)
internal class FFreecam
{
    //todo add option to copy patricks camera item to the freecam lol
    private bool inCamera = true;
    private bool isEnabled = false;
    private bool isInitialized = false;
    private bool reEnable = false;
    private Camera? camera = null;
    private Camera? game_camera = null;
    private const float defaultSpeed = 2.3f;
    private const float rotRes = 0.15f;
    private const float DefaultThirdPersonDistance = 2.0f;
    private const float CrouchThirdPersonDistance = 1.5f;
    private const float SprintThirdPersonDistance = 3.0f;
    private float ThirdPersonDistance = 2.0f;
    private float PreLerpPersonDistance = 2.0f;
    private readonly int PhysicsLayerMask;
    private float rotX = 0f;
    private float rotY = 0f;
    private float speed = defaultSpeed;
    private PlayerCharacter? player = null;
    private readonly bool inGameMain = false;
    private readonly Canvas? canvas;
    private readonly GameObject? CanvasGO = null;
    private readonly Toggle? usePhysicalPropertiesComp;
    private readonly Text? text;
    private readonly GameObject? physicalStuff;
    private readonly GameObject? UIRoot;
    private DateTime lastImmobilizedPlayer;
    private RaycastHit lastRayCastHit;
    private float timeLerped = 0;
    private const float timeToLerpDistance = 0.5f;
    private CinemachineVirtualCamera? thirdCamera = null;
    private GameObject? thirdCameraHolder = null;
    private CinemachineTransposer? body = null;
    private Transform? thirdCameraPosition = null;
    //private AudioListener audioListener;

    private readonly MelonPreferences_Category settings = default!;
    private readonly MelonPreferences_Entry<bool> ThirdPersonMode = default!;
    private readonly MelonPreferences_Entry<bool> AutostartFreecam = default!;
    private readonly MelonPreferences_Entry<bool> UIEnabled = default!;
    private readonly MelonPreferences_Entry<bool> GameCameraHidden = default!;
    private readonly MelonPreferences_Entry<bool> RightShoulder = default!;

    private const float WindowWidth = 0.25f;
    private const float WindowHeight = 0.3f;
    private const float WindowHeightExtended = WindowHeight + 0.405f;

    public FFreecam(string sceneName)
    {

        settings = MelonPreferences.CreateCategory("Freecam");
        if (settings.HasEntry(nameof(ThirdPersonMode)))
            ThirdPersonMode = (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(ThirdPersonMode));
        else
            ThirdPersonMode = settings.CreateEntry(nameof(ThirdPersonMode), false);
        if (settings.HasEntry(nameof(AutostartFreecam)))
            AutostartFreecam = (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(AutostartFreecam));
        else
            AutostartFreecam = settings.CreateEntry(nameof(AutostartFreecam), false, description: "Starts automatically");
        if (settings.HasEntry(nameof(UIEnabled)))
            UIEnabled = (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(UIEnabled));
        else
            UIEnabled = settings.CreateEntry(nameof(UIEnabled), false, description: "UI is shown");
        if (settings.HasEntry(nameof(GameCameraHidden)))
            GameCameraHidden = (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(GameCameraHidden));
        else
            GameCameraHidden = settings.CreateEntry(nameof(GameCameraHidden), false, description: "Game camera is hidden");
        if (settings.HasEntry(nameof(RightShoulder)))
            RightShoulder = (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(RightShoulder));
        else
            RightShoulder = settings.CreateEntry(nameof(RightShoulder), true, description: "True: cam over right shoulder. False: cam over left shoulder.");

        foreach (var item in Object.FindObjectsOfType<Camera>())
        {
            if (item.name == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
            {
                game_camera = item;
                Initialize();
                break;
            }
        }
        inGameMain = sceneName == "GameMain";

        if (camera is null) return;
        // Canvas
        CanvasGO = new()
        {
            name = "Freecam UI"
        };

        canvas = CanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = CanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        CanvasGO.AddComponent<GraphicRaycaster>();

        UIRoot = UIBuilder.CreatePanel("Freecam UI Container", CanvasGO, new(WindowWidth, WindowHeight), new(0, Screen.height * (1.0f - WindowHeight)), out var contentHolder);
        text = UIBuilder.CreateLabel(contentHolder, "Freecam info text", "");
        text.fontSize = 12;

        MelonLogger.Msg("Creating settings for the Camera...");

        UIBuilder.CreateInputField(nameof(camera.allowHDR), contentHolder, camera);
        UIBuilder.CreateInputField(nameof(camera.allowMSAA), contentHolder, camera);
        UIBuilder.CreateInputField(nameof(camera.aspect), contentHolder, camera);
        UIBuilder.CreateInputField(nameof(camera.fieldOfView), contentHolder, camera);
        UIBuilder.CreateLayerDropDown(contentHolder, camera.gameObject);
        UIBuilder.CreateLayerMaskDropDown(nameof(camera.cullingMask), contentHolder, camera, typeof(Camera).GetProperty(nameof(camera.cullingMask))!);

        //physics settings require so much space we just spawn a new window

        _ = UIBuilder.CreateToggle(contentHolder, "usePhysicalProperties", out usePhysicalPropertiesComp, out _);
        usePhysicalPropertiesComp.SetIsOnWithoutNotify(camera!.usePhysicalProperties);
        usePhysicalPropertiesComp.onValueChanged.AddListener(new System.Action<bool>((bool v) => camera!.usePhysicalProperties = v));
        usePhysicalPropertiesComp.onValueChanged.AddListener(new System.Action<bool>((bool v) =>
        {
            _ = physicalStuff is not null && (physicalStuff.active = v);
            if (v)
            {
                UIRoot.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                UIRoot.GetComponent<RectTransform>().anchorMax = new(WindowWidth, WindowHeightExtended);
                UIRoot.GetComponent<RectTransform>().position = new(Screen.width * WindowWidth / 2, Screen.height * (1.0f - (WindowHeightExtended / 2)));
                //UIRoot.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            }
            else
            {
                UIRoot.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                UIRoot.GetComponent<RectTransform>().anchorMax = new(WindowWidth, WindowHeight);
                UIRoot.GetComponent<RectTransform>().position = new(Screen.width * WindowWidth / 2, Screen.height * (1.0f - (WindowHeight / 2)));
                //UIRoot.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            }
        }));

        physicalStuff = UIBuilder.CreateUIObject("physical container", contentHolder);
        physicalStuff.active = false;
        _ = UIBuilder.SetLayoutGroup<VerticalLayoutGroup>(physicalStuff, true, true, 0, 2, 2, 2, 2);

        UIBuilder.CreateInputField(nameof(camera.anamorphism), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.aperture), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.barrelClipping), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.bladeCount), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.curvature), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.focalLength), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.focusDistance), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.iso), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.lensShift), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.orthographic), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.sensorSize), physicalStuff, camera);
        UIBuilder.CreateInputField(nameof(camera.shutterSpeed), physicalStuff, camera);
        MelonLogger.Msg("...Done");

        PhysicsLayerMask = LayerMask.GetMask("Default", "Ground", "InteractiveItems", "Characters", "Walls");
    }

    ~FFreecam()
    {
        AutostartFreecam.Value = isEnabled;
        MelonPreferences.Save();
    }

    public bool Enabled => isEnabled;

    public void OnUpdate()
    {

        if (!isEnabled && AutostartFreecam.Value && inGameMain && Object.FindObjectOfType<PlayerCharacter>() is not null)
            SetEnabled();
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

        AutostartFreecam.Value = isEnabled;

        MelonLogger.Msg("Freecam disabled.");
    }

    private void TryImmobilizePlayer()
    {
        if (player is null) return;
        if (player._controlManager is null) return;
        if (player._controlManager.PlayerInput is null) return;
        if (inCamera && !ThirdPersonMode.Value)
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
                if (GameCameraHidden.Value)
                    item.enabled = false;
                else
                {
                    item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                    item.enabled = true;
                }
            }
        }

        inCamera = true;
        if (inGameMain)
        {
            player = Object.FindObjectOfType<PlayerCharacter>();
            TryImmobilizePlayer();
        }
        else
        {
            Screen.lockCursor = true;
        }

        if (ThirdPersonMode.Value && thirdCamera is null)
        {
            SetupThirdPersonCamera();
        }

        isEnabled = true;
        AutostartFreecam.Value = isEnabled;
        MelonLogger.Msg("Freecam enabled.");
    }

    public void UpdateMovement()
    {
        //only move when we are not moving the player
        if (isEnabled && inCamera && camera is not null)
        {
            var value = Mouse.current.delta.ReadValue();

            if (!ThirdPersonMode.Value || !inGameMain)
            {
                if (Keyboard.current.leftShiftKey.isPressed)
                    speed = defaultSpeed * 2.5f;
                else
                    speed = defaultSpeed;

                float dTime = Time.deltaTime;

                //game_camera?.transform.Translate(Vector3.forward * speed * dTime, Space.Self);

                if (Keyboard.current.wKey.isPressed)
                {
                    camera.transform.Translate(Vector3.forward * speed * dTime, Space.Self);
                }
                else if (Keyboard.current.sKey.isPressed)
                {
                    camera.transform.Translate(Vector3.back * speed * dTime, Space.Self); ;
                }

                if (Keyboard.current.aKey.isPressed)
                {
                    camera.transform.Translate(Vector3.left * speed * dTime, Space.Self);
                }
                else if (Keyboard.current.dKey.isPressed)
                {
                    camera.transform.Translate(Vector3.right * speed * dTime, Space.Self);
                }

                if (Keyboard.current.spaceKey.isPressed)
                {
                    camera.transform.Translate(Vector3.up * speed * dTime, Space.Self);
                }
                else if (Keyboard.current.leftCtrlKey.isPressed)
                {
                    camera.transform.Translate(Vector3.down * speed * dTime, Space.Self);
                }

                camera.transform.get_rotation_Injected(out var oldRotation);
                rotY += value.x;
                rotX -= value.y;
                var newRotation = Quaternion.Lerp(oldRotation, Quaternion.Euler(new Vector3(rotRes * rotX, rotRes * rotY, 0)), 50 * Time.deltaTime);
                GCHandle pinnedRotation = GCHandle.Alloc(newRotation, GCHandleType.Pinned);
                camera.transform.set_rotation_Injected(ref newRotation);
                pinnedRotation.Free();
            }
            else if (game_camera is not null && ThirdPersonMode.Value && player is not null && thirdCamera is not null)
            {
                //change player camera distance depending on the state
                UpdateThirdPersonCameraDistance();
                UpdateThirdPersonCameraPositions();
            }
            else if (isInitialized && !isEnabled && camera is not null && player is not null)
            {
                player.Head.transform.get_position_Injected(out var playerPos);
                GCHandle pinnedPos = GCHandle.Alloc(playerPos, GCHandleType.Pinned);
                camera.transform.set_position_Injected(ref playerPos);
                pinnedPos.Free();
            }
        }
    }

    private void UpdateThirdPersonCameraPositions()
    {
        if (game_camera is null || !ThirdPersonMode.Value || player is null || thirdCameraPosition is null)
            return;

        player.Head.transform.get_position_Injected(out var playerPos);
        var potentiallyNewPos = playerPos - ThirdPersonDistance * game_camera.transform.forward + ThirdPersonDistance / (RightShoulder.Value ? 4 : -4) * game_camera.transform.right;
        //check for collision, move freecam pos there
        var hit = Physics.Raycast(playerPos, potentiallyNewPos - playerPos, out RaycastHit info, ThirdPersonDistance, PhysicsLayerMask);
        if (!hit)
            playerPos = potentiallyNewPos;
        else
        {
            playerPos = info.point + (ThirdPersonDistance - info.distance + 0.01f) * game_camera.transform.forward;
        }
        GCHandle pinnedPos = GCHandle.Alloc(playerPos, GCHandleType.Pinned);
        thirdCameraPosition.set_position_Injected(ref playerPos);
        pinnedPos.Free();

        game_camera.transform.get_position_Injected(out var gamePos);
        if (Physics.Raycast(gamePos, game_camera.transform.forward, out info, float.MaxValue, PhysicsLayerMask))
        {
            if (lastRayCastHit.transform is null)
                lastRayCastHit = info;
            var vec = info.point;
            var handle = GCHandle.Alloc(vec, GCHandleType.Pinned);
            lastRayCastHit.transform.set_position_Injected(ref vec);
            handle.Free();
        }
    }

    private void UpdateThirdPersonCameraDistance()
    {
        if (player is null || body is null)
            return;
        if (player.IsCrouching)
        {
            //when we start crouching, save distance so we can lerp away form it
            if (ThirdPersonDistance >= DefaultThirdPersonDistance && ThirdPersonDistance > PreLerpPersonDistance)
            {
                PreLerpPersonDistance = ThirdPersonDistance;
                timeLerped = 0.0f;
            }

            if (ThirdPersonDistance > CrouchThirdPersonDistance)
            {
                ThirdPersonDistance = Mathf.Lerp(PreLerpPersonDistance, CrouchThirdPersonDistance, timeLerped / timeToLerpDistance);
                timeLerped += Time.deltaTime;
            }
        }
        else if (player.Controller.velocity.sqrMagnitude > 10.0f)
        {
            //when we start crouching, save distance so we can lerp away form it
            if (ThirdPersonDistance <= DefaultThirdPersonDistance && ThirdPersonDistance < PreLerpPersonDistance)
            {
                PreLerpPersonDistance = ThirdPersonDistance;
                timeLerped = 0.0f;
            }

            if (ThirdPersonDistance < SprintThirdPersonDistance)
            {
                ThirdPersonDistance = Mathf.Lerp(PreLerpPersonDistance, SprintThirdPersonDistance, timeLerped / timeToLerpDistance);
                timeLerped += Time.deltaTime;
            }
        }
        else
        {
            //when we start crouching, save distance so we can lerp away form it
            if ((ThirdPersonDistance < DefaultThirdPersonDistance && ThirdPersonDistance < PreLerpPersonDistance) || (ThirdPersonDistance > DefaultThirdPersonDistance && ThirdPersonDistance > PreLerpPersonDistance))
            {
                PreLerpPersonDistance = ThirdPersonDistance;
                timeLerped = 0.0f;
            }

            if (ThirdPersonDistance != DefaultThirdPersonDistance)
            {
                ThirdPersonDistance = Mathf.Lerp(PreLerpPersonDistance, DefaultThirdPersonDistance, timeLerped / timeToLerpDistance);
                timeLerped += Time.deltaTime;
            }
        }

        if (body.m_FollowOffset.z != -ThirdPersonDistance)
        {
            if (RightShoulder.Value)
                body.m_FollowOffset = new(ThirdPersonDistance / 4, 1.75f, -ThirdPersonDistance);
            else
                body.m_FollowOffset = new(-ThirdPersonDistance / 4, 1.75f, -ThirdPersonDistance);
        }
    }

    private void CheckForToggle()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
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
        if (Keyboard.current.uKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
        {
            UIEnabled.Value = !UIEnabled.Value;
            MelonLogger.Msg("UI " + (UIEnabled.Value ? "enabled" : "disabled"));
            DisplayUI();
        }
    }

    private void DisplayUI()
    {
        string toDisplay = string.Empty;
        if (CanvasGO is null || canvas is null || text is null) return;
        if (UIEnabled.Value && camera is not null && game_camera is not null)
        {
            CanvasGO.active = true;
            canvas.scaleFactor = 1.0f;
            string lookingAt = "None";
            string lookingAt2 = "None";
            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hit, float.MaxValue))
                lookingAt = $"{hit.transform.gameObject.name}|{LayerMask.LayerToName(hit.transform.gameObject.layer)}";

            if (player is null)
            {
                var mousePos = Mouse.current.position.ReadValue();
                var mouseDelta = Mouse.current.delta.ReadValue();
                toDisplay = $"Mouse position ({mousePos.x}|{mousePos.y})\n" +
                    $"Mouse delta: ({mouseDelta.x}|{mouseDelta.y})\n" +
                    $"Freecam position ({camera.transform.position.x:0.00}|{camera.transform.position.y:0.00}|{camera.transform.position.z:0.00})\n" +
                    $"Freecam rotation ({camera.transform.rotation.eulerAngles.x:0.00}|{camera.transform.rotation.eulerAngles.y:0.00}|{camera.transform.rotation.eulerAngles.z:0.00})\n" +
                    $"Freecam speed ({camera.velocity.x:0.00}|{camera.velocity.y:0.00}|{camera.velocity.z:0.00})\n" +
                    $"Freecam looking at {lookingAt}";
            }
            else
            {
                if (Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hit2, float.MaxValue))
                    lookingAt2 = $"{hit2.transform.gameObject.name}|{LayerMask.LayerToName(hit2.transform.gameObject.layer)}";
                toDisplay = $"Player position ({player.transform.position.x:0.00}|{player.transform.position.y:0.00}|{player.transform.position.z:0.00})\n" +
                    $" Freecam position ({camera.transform.position.x:0.00}|{camera.transform.position.y:0.00}|{camera.transform.position.z:0.00})\n" +
                    $"Player rotation ({game_camera.transform.rotation.eulerAngles.x:0.00}|{game_camera.transform.rotation.eulerAngles.y:0.00}|{game_camera.transform.rotation.eulerAngles.z:0.00})\n" +
                    $" Freecam rotation ({camera.transform.rotation.eulerAngles.x:0.00}|{camera.transform.rotation.eulerAngles.y:0.00}|{camera.transform.rotation.eulerAngles.z:0.00})\n" +
                    $"Player movement speed ({game_camera.velocity.x:0.00}|{game_camera.velocity.y:0.00}|{game_camera.velocity.z:0.00})\n" +
                    $" Freecam speed ({camera.velocity.x:0.00}|{camera.velocity.y:0.00}|{camera.velocity.z:0.00})\n" +
                    $"Player looking at {lookingAt}\n" +
                    $"Freecam looking at {lookingAt2}";
            }
        }
        else
        {
            CanvasGO.active = false;
        }
        text.text = toDisplay;
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
            camera.name = "Freecam Camera";
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
            Object.DestroyImmediate(camera.gameObject.GetComponent<AudioListener>());
            //audioListener = camera.gameObject.GetComponent<AudioListener>();
            //if (!ThirdPersonMode.Value)
            //audioListener.enabled = false;

            isInitialized = camera.gameObject.GetComponents<MonoBehaviour>().Count > 0;
            //ObjectInfo.PrintHierarchy(camera.gameObject);
            //MelonLogger.Msg("our own camera was initialized");

            //todo, use input system
            //idea: get the actions once, then check their state each update, like we used to do with the keyboards. this will allow us to use controllers as well though. activating with ?

            //PlayerControlManager.add_OnInput_Crouch(new System.Action(() => { }));

            //MelonLogger.Msg("Actions");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.actions)
            //{
            //    MelonLogger.Msg($"  {item.name} {item.type}");
            //    MelonLogger.Msg("  Controls");
            //    foreach (var ctl in item.controls)
            //    {
            //        MelonLogger.Msg($"    {ctl.m_Name} {ctl.displayName}");
            //    }
            //    MelonLogger.Msg("  Bindings");
            //    foreach (var ctl in item.bindings)
            //    {
            //        MelonLogger.Msg($"    {ctl.interactions} {ctl.processors} {ctl.name}");
            //    }
            //    MelonLogger.Msg("  Processors");
            //    MelonLogger.Msg("    " + item.processors);
            //}

            //MelonLogger.Msg("Bindings");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.bindings)
            //{
            //    MelonLogger.Msg("  " + item.groups);
            //    MelonLogger.Msg("  " + item.action);
            //    MelonLogger.Msg("  " + item.interactions);
            //    MelonLogger.Msg("  " + item.name);
            //}
            //MelonLogger.Msg("Control Schemes");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.controlSchemes)
            //{
            //    MelonLogger.Msg("  " + item.bindingGroup);
            //    MelonLogger.Msg("  " + item.name);
            //    MelonLogger.Msg("  Device Requirements");
            //    foreach (var ctl in item.deviceRequirements)
            //    {
            //        MelonLogger.Msg($"    {ctl.controlPath} {ctl.isOptional} {ctl.isAND} {ctl.isOR}");
            //    }

            //}
            //MelonLogger.Msg("Devices");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.devices.Value)
            //{
            //    MelonLogger.Msg("  " + item.name);
            //    MelonLogger.Msg("  " + item.displayName);
            //    MelonLogger.Msg("  " + item.layout);
            //}
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
                if (Keyboard.current.gKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && inCamera)
                {
                    inCamera = false;
                    TryImmobilizePlayer();
                    MelonLogger.Msg("Control moved to player.");
                }
                else if (Keyboard.current.gKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
                {
                    inCamera = true;
                    TryImmobilizePlayer();
                    MelonLogger.Msg("Control moved to the freecam.");
                }
                if (inCamera && (DateTime.Now - lastImmobilizedPlayer).Milliseconds > 300)
                {
                    TryImmobilizePlayer();
                    lastImmobilizedPlayer = DateTime.Now;
                }
                if (Keyboard.current.vKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
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
                if (Keyboard.current.tKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && !ThirdPersonMode.Value)
                {
                    ThirdPersonMode.Value = true;
                    //audioListener.enabled = true;
                    SetupThirdPersonCamera();
                }
                else if (Keyboard.current.tKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && ThirdPersonMode.Value && game_camera is not null)
                {
                    ThirdPersonMode.Value = false;
                    if (thirdCameraHolder is not null)
                        thirdCameraHolder.active = false;
                    //dont see player head
                    game_camera.cullingMask &= ~(1 << 18);
                    //audioListener.enabled = false;
                }
                //also turn off third person when sex or cutscene starts
                if (ThirdPersonMode.Value && CutSceneManager.CurrentPlayerScene is not null && Enabled)
                {
                    reEnable = true;
                    SetDisabled();
                }
            }
            else
            {
                if (Keyboard.current.gKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && inCamera)
                {
                    inCamera = false;
                    Screen.lockCursor = false;
                    MelonLogger.Msg("Control moved to the UI menu.");
                }
                else if (Keyboard.current.gKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
                {
                    inCamera = true;
                    Screen.lockCursor = true;
                    MelonLogger.Msg("Control moved to the freecam.");
                }
            }
            if (!ThirdPersonMode.Value && Keyboard.current.hKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && !GameCameraHidden.Value)
            {
                GameCameraHidden.Value = true;
                foreach (var item in Object.FindObjectsOfType<Camera>())
                {
                    string name = $"{item.gameObject.name}";
                    if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                    {
                        item.enabled = false;
                    }
                }
            }
            else if (!ThirdPersonMode.Value && Keyboard.current.hKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
            {
                GameCameraHidden.Value = false;
                foreach (var item in Object.FindObjectsOfType<Camera>())
                {
                    string name = $"{item.gameObject.name}";
                    if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                    {
                        item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                        item.enabled = true;
                    }
                }
            }
            UpdateMovement();
        }
        else if (reEnable && inGameMain && player is not null && ThirdPersonMode.Value && CutSceneManager.CurrentPlayerScene is null)
        {
            SetEnabled();
        }
    }

    private void SetupThirdPersonCamera()
    {
        if (thirdCamera is null && player is not null)
        {
            thirdCameraHolder ??= new("ThirdPersonCameraHolder");
            thirdCameraPosition ??= new GameObject("ThirdCameraPositionHolder").transform;

            MelonLogger.Msg("Setting up the third person camera");
            thirdCamera = thirdCameraHolder.AddComponent<CinemachineVirtualCamera>();
            thirdCamera.Follow = thirdCameraPosition;
            thirdCamera.LookAt = lastRayCastHit.transform;

            body = thirdCamera.AddCinemachineComponent<CinemachineTransposer>();
            var aim = thirdCamera.AddCinemachineComponent<CinemachineComposer>();
            var noise = thirdCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

            body.m_XDamping = 1.0f;
            body.m_YDamping = 1.0f;
            body.m_ZDamping = 1.0f;
            body.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetNoRoll;

            body.m_FollowOffset = Vector3.zeroVector;

            aim.m_HorizontalDamping = 1.0f;
            aim.m_VerticalDamping = 1.0f;

            try
            {
                noise.m_NoiseProfile = (NoiseSettings)ScriptableObject.CreateScriptableObjectInstanceFromName("Handheld_wideangle_mild");
            }
            catch (System.Exception e)
            {
                MelonLogger.Error(e);
            }

            FinalizeThirdPersonCamSetup();
        }
        else
        {
            FinalizeThirdPersonCamSetup();
        }
    }

    private void FinalizeThirdPersonCamSetup()
    {
        if (thirdCameraHolder is null) return;
        if (camera is null) return;
        if (game_camera is null) return;

        thirdCameraHolder.active = true;
        camera.enabled = false;
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

        //see player head
        game_camera.cullingMask |= 1 << 18;
    }
}
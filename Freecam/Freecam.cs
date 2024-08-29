using HPUI;
using Il2Cpp;
using Il2CppCinemachine;
using Il2CppEekCharacterEngine;
using Il2CppEekCharacterEngine.Interaction;
using Il2CppEekCharacterEngine.Interface;
using Il2CppEekEvents;
using Il2CppEekEvents.Dialogues;
using Il2CppEekUI;
using MelonLoader;
using System;
using System.Collections.Generic;
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

    public override void OnGUI() => freecam?.OnGui();

#if DEBUG
    public override void OnInitializeMelon() => MelonDebug.Msg("[FREECAM] Debug build of the freecam!");
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

    public override void OnLateUpdate() => freecam?.LateUpdate();

    private static Assembly AssemblyResolveEventListener(object sender, ResolveEventArgs args)
    {
        if (args is null)
        {
            return null!;
        }

        string name = "Freecam.Resources." + args.Name[..args.Name.IndexOf(',')] + ".dll";
        using Stream? str = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (str is not null)
        {
            var context = new AssemblyLoadContext(name, false);
            MelonLogger.Warning($"Loaded {args.Name} from our embedded resources, saving to userlibs for next time");
            string path = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly()?.Location!)!.Parent!.FullName, "UserLibs", args.Name[..args.Name.IndexOf(',')] + ".dll");
            foreach (PropertyInfo field in typeof(Properties.Resources).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
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

        Type domainType = typeof(AssemblyLoadContext);

        while (field is null)
        {
            if (domainType is not null)
            {
                field = domainType.GetField("AssemblyResolve", flags);
            }
            else
            {
                //MelonDebug.Error("domainType got set to null for the AssemblyResolve event was null");
                return;
            }
            if (field is null)
            {
                domainType = domainType.BaseType!;
            }
        }

        var resolveDelegate = (MulticastDelegate)field.GetValue(null)!;
        Delegate[] subscribers = resolveDelegate.GetInvocationList();

        Delegate currentDelegate = resolveDelegate;
        for (int i = 0; i < subscribers.Length; i++)
        {
            currentDelegate = System.Delegate.RemoveAll(currentDelegate, subscribers[i])!;
        }

        var newSubscriptions = new Delegate[subscribers.Length + 1];
        newSubscriptions[0] = (ResolveEventHandler)AssemblyResolveEventListener!;
        System.Array.Copy(subscribers, 0, newSubscriptions, 1, subscribers.Length);

        currentDelegate = Delegate.Combine(newSubscriptions)!;

        field.SetValue(null, currentDelegate);
    }
}

//yoink this class if you need a freecam
//or just use the mod alongside yours 
internal class FFreecam
{
    //todo add option to copy patricks camera item to the freecam lol
    //todo add options to add new objects, clone oibjects, move objects with UI and with camera movement (ray to next object, and fixed distance from cam)
    //todo when releasing the third person mod use pragmas to seperate out the freecam with all and only third person
    //todo change up toggles to Function keys? maybe with settings
    private bool inCamera = true;
    private bool isInitialized = false;
    private bool reEnable = false;
    private bool inSex = false;
    private Camera? camera = null;
    private Camera? game_camera = null;
    private const float DefaultThirdPersonDistance = 1.7f;
    private const float CrouchThirdPersonDistance = 1.1f;
    private const float SprintThirdPersonDistance = 3.0f;
    private float ThirdPersonDistance = 2.0f;
    private float PreLerpPersonDistance = 2.0f;
    private readonly int PhysicsLayerMask;
    private float rotX = 0f;
    private float rotY = 0f;
    private float speed = 0f;
    private float sexStartTime = 0;
    private PlayerCharacter? player = null;
    private readonly bool inGameMain = false;
    private readonly Canvas? canvas;
    private readonly GameObject? CanvasGO = null;
    private readonly Toggle? usePhysicalPropertiesComp;
    private readonly Text? text;
    private readonly GameObject? physicalStuff;
    private readonly GameObject? UIRoot;
    private DateTime lastImmobilizedPlayer;
    private float timeLerped = 0;
    private const float timeToLerpDistance = 0.5f;
    private CinemachineVirtualCamera? thirdCamera = null;
    private GameObject? thirdCameraHolder = null;
    private CinemachineTransposer? body = null;
    private Transform? thirdCameraPosition = null;
    private Transform? thirdCameraRotation = null;
    private Transform? thirdCameraLookAtPosition = null;
    //private AudioListener audioListener;
    private readonly Dictionary<string, float> interactionDistances = new();
    private float oldFOV;
    private SexualActs currentAct;
    private Vector3 playerHipPos;

    private readonly MelonPreferences_Category settings = default!;
    public static MelonPreferences_Entry<bool> ThirdPersonMode = default!;
    private readonly MelonPreferences_Entry<bool> AutostartFreecam = default!;
    private readonly MelonPreferences_Entry<bool> UIEnabled = default!;
    private readonly MelonPreferences_Entry<bool> GameCameraHidden = default!;
    private readonly MelonPreferences_Entry<bool> RightShoulder = default!;
    private readonly MelonPreferences_Entry<float> rotationSpeed = default!;
    private readonly MelonPreferences_Entry<float> flySpeed = default!;

    private const float WindowWidth = 0.25f;
    private const float WindowHeight = 0.3f;
    private const float WindowHeightExtended = WindowHeight + 0.405f;

    public FFreecam(string sceneName)
    {

        settings = MelonPreferences.CreateCategory("Freecam");
        ThirdPersonMode = settings.HasEntry(nameof(ThirdPersonMode))
            ? (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(ThirdPersonMode))
            : settings.CreateEntry(nameof(ThirdPersonMode), false);

        AutostartFreecam = settings.HasEntry(nameof(AutostartFreecam))
            ? (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(AutostartFreecam))
            : settings.CreateEntry(nameof(AutostartFreecam), false, description: "Starts automatically");

        UIEnabled = settings.HasEntry(nameof(UIEnabled))
            ? (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(UIEnabled))
            : settings.CreateEntry(nameof(UIEnabled), false, description: "UI is shown");

        GameCameraHidden = settings.HasEntry(nameof(GameCameraHidden))
            ? (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(GameCameraHidden))
            : settings.CreateEntry(nameof(GameCameraHidden), false, description: "Game camera is hidden");

        RightShoulder = settings.HasEntry(nameof(RightShoulder))
            ? (MelonPreferences_Entry<bool>)settings.GetEntry(nameof(RightShoulder))
            : settings.CreateEntry(nameof(RightShoulder), true, description: "True: cam over right shoulder. False: cam over left shoulder.");

        rotationSpeed = settings.HasEntry(nameof(rotationSpeed))
            ? (MelonPreferences_Entry<float>)settings.GetEntry(nameof(rotationSpeed))
            : settings.CreateEntry(nameof(rotationSpeed), 0.15f, description: "rotation speed of the freecam and third person cam");

        flySpeed = settings.HasEntry(nameof(flySpeed))
            ? (MelonPreferences_Entry<float>)settings.GetEntry(nameof(flySpeed))
            : settings.CreateEntry(nameof(flySpeed), 2.3f, description: "movement speed of the freecam");

        speed = flySpeed.Value;

        //MelonDebug.Msg($"[FREECAM] settings: " +
        //    $"\n third person mode: {ThirdPersonMode.Value}" +
        //    $"\n autostart: {AutostartFreecam.Value}" +
        //    $"\n ui enabled: {UIEnabled.Value}" +
        //    $"\n game camera hidden: {GameCameraHidden.Value}" +
        //    $"\n right shoulder: {RightShoulder.Value}");

        foreach (Camera item in Object.FindObjectsOfType<Camera>())
        {
            if (item.name == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
            {
                game_camera = item;
                oldFOV = game_camera.fieldOfView;
                _ = Initialize();
                break;
            }
        }
        inGameMain = sceneName == "GameMain";

        if (camera is null)
        {
            //MelonDebug.Msg("[FREECAM] could not clone games' camera");
            return;
        }
        // Canvas
        CanvasGO = new()
        {
            name = "Freecam UI"
        };

        canvas = CanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = CanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _ = CanvasGO.AddComponent<GraphicRaycaster>();

        UIRoot = UIBuilder.CreatePanel("Freecam UI Container", CanvasGO, new(WindowWidth, WindowHeight), new(0, Screen.height * (1.0f - WindowHeight)), out GameObject? contentHolder);
        text = UIBuilder.CreateLabel(contentHolder, "Freecam info text", "");
        text.fontSize = 12;

        //MelonDebug.Msg("[FREECAM] Creating settings for the Camera...");

        UIBuilder.CreateInputField(nameof(camera.allowHDR), contentHolder, camera);
        UIBuilder.CreateInputField(nameof(camera.allowMSAA), contentHolder, camera);
        UIBuilder.CreateInputField(nameof(camera.aspect), contentHolder, camera);
        UIBuilder.CreateInputField(nameof(camera.fieldOfView), contentHolder, camera);
        UIBuilder.CreateLayerDropDown(contentHolder, camera.gameObject);
        UIBuilder.CreateLayerMaskDropDown(nameof(camera.cullingMask), contentHolder, camera, typeof(Camera).GetProperty(nameof(camera.cullingMask))!);

        //physics settings require so much space we just spawn a new window

        _ = UIBuilder.CreateToggle(contentHolder, "usePhysicalProperties", out usePhysicalPropertiesComp, out _);
        usePhysicalPropertiesComp.SetIsOnWithoutNotify(camera!.usePhysicalProperties);
        usePhysicalPropertiesComp.onValueChanged.AddListener(new Action<bool>((bool v) => camera!.usePhysicalProperties = v));
        usePhysicalPropertiesComp.onValueChanged.AddListener(new Action<bool>((bool v) =>
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
        //MelonDebug.Msg("[FREECAM] ...Done");

        PhysicsLayerMask = LayerMask.GetMask("Default", "Ground", "InteractiveItems", "Characters", "Walls");
    }

    ~FFreecam()
    {
        AutostartFreecam.Value = Enabled;
        MelonPreferences.Save();
    }

    public static bool Enabled { get; private set; } = false;

    public void OnUpdate()
    {

        if (!Enabled && AutostartFreecam.Value && inGameMain && Object.FindObjectOfType<PlayerCharacter>() is not null && !ScreenFade.Singleton.IsFadeVisible)
        {
            SetEnabled();
        }
        //toggle freecam with alt+f
        CheckForToggle();

        //update position and so on
        Update();

        //if (game_camera is null) return;
        //game_camera.transform.get_position_Injected(out var pos);
        //Patcher.position = pos;
        //Patcher.forward = game_camera.transform.forward;
    }

    public void OnGui() => DisplayUI();

    public void SetDisabled()
    {
        Enabled = false;
        AutostartFreecam.Value = Enabled;
        inCamera = false;
        Screen.lockCursor = false;

        if (game_camera is not null && inGameMain)
        {
            //Object.DestroyImmediate(camera.gameObject.GetComponent<EekCamera>());
            EekCamera.Singleton = game_camera.gameObject.AddComponent<EekCamera>();
            game_camera.fieldOfView = oldFOV;
        }

        if (!ThirdPersonMode.Value || !inGameMain)
        {
            if (camera is not null)
            {
                camera.enabled = false;
            }

            //move cameras back
            foreach (Camera item in Object.FindObjectsOfType<Camera>())
            {
                string name = $"{item.gameObject.name}";
                if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                {
                    //MelonDebug.Msg($"[FREECAM] Moved {item.name} back to fullscreen.");
                    item.rect = new Rect(0, 0, 1, 1);
                    item.enabled = true;
                }
            }

            if (player != null)
            {
                TryImmobilizePlayer();
            }
        }
        else
        {
            DisableThirdPerson();
            ThirdPersonMode.Value = true;
            //MelonDebug.Msg("[FREECAM] Third Person Cam disabled.");
            return;
        }

        //MelonDebug.Msg("[FREECAM] Freecam disabled.");
    }

    private void TryImmobilizePlayer()
    {
        if (player is null)
        {
            return;
        }

        if (player._controlManager is null)
        {
            return;
        }

        if (player._controlManager.PlayerInput is null)
        {
            return;
        }

        if (inCamera && !ThirdPersonMode.Value)
        {
            player._controlManager.PlayerInput.DeactivateInput();
        }
        else
        {
            player._controlManager.PlayerInput.ActivateInput();
        }
    }

    public void SetEnabled()
    {
        Enabled = true;
        AutostartFreecam.Value = Enabled;

        if (!ThirdPersonMode.Value || !inGameMain)
        {
            if (camera == null || !isInitialized)
            {
                //MelonDebug.Msg("[FREECAM] our camera was null");
                foreach (Camera item in Object.FindObjectsOfType<Camera>())
                {
                    string name = $"{item.gameObject.name}";
                    int count = item.gameObject.GetComponents<MonoBehaviour>().Count;
                    if (name == "Camera" && count > 0)
                    {
                        //MelonDebug.Msg("[FREECAM] got a camera");
                        game_camera = item;
                        oldFOV = game_camera.fieldOfView;
                        break;
                    }
                }
                if (!Initialize())
                {
                    return;
                }
            }
            if (camera != null)
            {
                camera.enabled = true;
            }
            //MelonDebug.Msg("[FREECAM] moving screens");
            //move cameras to top left
            foreach (Camera item in Object.FindObjectsOfType<Camera>())
            {
                string name = $"{item.gameObject.name}";
                if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                {
                    //MelonDebug.Msg($"[FREECAM] Moved {item.name} to the top left.");
                    if (GameCameraHidden.Value)
                    {
                        item.enabled = false;
                    }
                    else
                    {
                        item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                        item.enabled = true;
                    }
                }
            }
            if (InteractionManager.Singleton is not null)
            {
                InteractionManager.Singleton._goalText = "";
            }
        }
        else if (ThirdPersonMode.Value && inGameMain)
        {
            player = Object.FindObjectOfType<PlayerCharacter>();
            EnableThirdPerson();
            MelonLogger.Msg("Third Person Cam enabled.");
            return;
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

        MelonLogger.Msg($"Freecam enabled.");
    }

    public void UpdateMovement()
    {
        //only move when we are not moving the player
        if (Enabled && inCamera && camera is not null)
        {
            Vector2 value = Mouse.current.delta.ReadValue();

            speed = Keyboard.current.leftShiftKey.isPressed ? flySpeed.Value * 2.5f : flySpeed.Value;

            float dTime = Time.deltaTime;

            if (Keyboard.current.wKey.isPressed)
            {
                camera.transform.Translate(Vector3.forward * speed * dTime, Space.Self);
            }
            else if (Keyboard.current.sKey.isPressed)
            {
                camera.transform.Translate(Vector3.back * speed * dTime, Space.Self);
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

            camera.transform.get_rotation_Injected(out Quaternion oldRotation);
            rotY += value.x;
            rotX -= value.y;
            var newRotation = Quaternion.Lerp(oldRotation, Quaternion.Euler(new Vector3(rotationSpeed.Value * rotX, rotationSpeed.Value * rotY, 0)), 50 * Time.deltaTime);
            var pinnedRotation = GCHandle.Alloc(newRotation, GCHandleType.Pinned);
            camera.transform.set_rotation_Injected(ref newRotation);
            pinnedRotation.Free();
        }
        else if (isInitialized && !Enabled && camera is not null && player is not null)
        {
            player.Head.transform.get_position_Injected(out Vector3 playerPos);
            var pinnedPos = GCHandle.Alloc(playerPos, GCHandleType.Pinned);
            camera.transform.set_position_Injected(ref playerPos);
            pinnedPos.Free();
        }
    }

    public void LateUpdate()
    {
        if (inGameMain && game_camera is not null && ThirdPersonMode.Value && player is not null && thirdCamera is not null)
        {
            //change player camera distance depending on the state
            UpdateThirdPersonCameraDistance();

            //dont update if ui is up
            //if (EekCamera.Singleton.ShouldRotate)
            {
                UpdateThirdPersonCameraPositions();
            }
        }
    }

    private void UpdateThirdPersonCameraPositions()
    {
        if (game_camera is null || !ThirdPersonMode.Value || player is null || thirdCameraPosition is null || thirdCameraRotation is null || thirdCameraLookAtPosition is null)
        {
            return;
        }

        inSex = (Time.time - sexStartTime) >= 2f;
        if (player.Intimacy.CurrentSexPartner is null && player.Intimacy.CurrentSexualActivity == SexualActs.None && !player.IsLayingDown)
        {
            player.Head.transform.get_position_Injected(out playerHipPos);
            currentAct = player.Intimacy.CurrentSexualActivity;
            inSex = false;
            //MelonDebug.Msg("[FREECAM] ended sex act");
        }
        else if (player.Intimacy.CurrentSexualActivity != currentAct || player.IsLayingDown)
        {
            //fopr changing pos
            inSex = false;
            sexStartTime = Time.time;
            currentAct = player.Intimacy.CurrentSexualActivity;
            player.puppetHip.get_position_Injected(out playerHipPos);
            playerHipPos.y += 0.5f;
            //MelonDebug.Msg("[FREECAM] started new sex act");
        }
        else if (!inSex && (player.Intimacy.CurrentSexualActivity != SexualActs.None || player.IsLayingDown) && (Time.time - sexStartTime) < 2f)
        {
            //starting new
            currentAct = player.Intimacy.CurrentSexualActivity;
            player.puppetHip.get_position_Injected(out playerHipPos);
            playerHipPos.y += 0.5f;
            //MelonDebug.Msg("[FREECAM] updating position for sex act");
        }


        if (!player.FPInput.AllowCameraMovement)
        {
            return;
        }

        thirdCameraRotation.position = playerHipPos;

        //############################# from here
        //update "set" rotation
        Vector2 value = Mouse.current.delta.ReadValue();
        thirdCameraRotation.get_rotation_Injected(out Quaternion currentRotation);
        rotY += value.x;
        rotX -= value.y;
        var newRotation = Quaternion.Lerp(currentRotation, Quaternion.Euler(new Vector3(rotationSpeed.Value / 2 * rotX, rotationSpeed.Value / 2 * rotY, 0)), 50 * Time.deltaTime);
        var pinnedRotation = GCHandle.Alloc(newRotation, GCHandleType.Pinned);
        thirdCameraRotation.set_rotation_Injected(ref newRotation);
        pinnedRotation.Free();

        Vector3 potentiallyNewPos = playerHipPos - (ThirdPersonDistance * thirdCameraRotation.forward);
        if (!inSex)
        {
            potentiallyNewPos += (ThirdPersonDistance / (RightShoulder.Value ? 4 : -4) * thirdCameraRotation.right);
        }
        //Vector3 potentiallyNewPos = playerHipPos - (ThirdPersonDistance * game_camera.transform.forward) + (ThirdPersonDistance / (RightShoulder.Value ? 4 : -4) * game_camera.transform.right);
        //check for collision, move freecam pos there
        bool hit = Physics.Raycast(playerHipPos, potentiallyNewPos - playerHipPos, out RaycastHit info, ThirdPersonDistance, PhysicsLayerMask);

        var potentialNewPos2 = !hit ? potentiallyNewPos : info.point + ((ThirdPersonDistance - info.distance + 0.02f) * thirdCameraRotation.forward);
        //potentialNewPos2 = !hit ? potentiallyNewPos : info.point + ((ThirdPersonDistance - info.distance + 0.02f) * game_camera.transform.forward);

        var pinnedPos = GCHandle.Alloc(potentialNewPos2, GCHandleType.Pinned);
        thirdCameraPosition.set_position_Injected(ref potentialNewPos2);
        pinnedPos.Free();

        //################################ to here
        // it works correct

        Vector3 vec;
        if (Physics.Raycast(thirdCameraRotation.position, thirdCameraRotation.forward, out var info2, float.MaxValue, PhysicsLayerMask))
        {
            ////MelonDebug.Msg(thirdCameraRotation.eulerAngles.x + " " + thirdCameraRotation.eulerAngles.y + " " + game_camera.transform.eulerAngles.x + " " + game_camera.transform.eulerAngles.y + " " + info2.transform.name);

            vec = info2.point;
            //thirdCameraPosition.LookAt(info.point);
        }
        else
        {
            ////MelonDebug.Msg(thirdCameraRotation.eulerAngles.x + " " + thirdCameraRotation.eulerAngles.y + " " + game_camera.transform.eulerAngles.x + " " + game_camera.transform.eulerAngles.y);
            vec = thirdCameraRotation.position + (4 * thirdCameraRotation.forward);
        }
        var handle = GCHandle.Alloc(vec, GCHandleType.Pinned);
        thirdCameraLookAtPosition.transform.set_position_Injected(ref vec);
        if (player.Motion.IsCurrentlyMoving && player.Intimacy.CurrentSexualActivity == SexualActs.None)
        {
            player.Rotation.RotateTowardDirection(thirdCameraRotation.forward);
        }
        handle.Free();
    }

    private void UpdateThirdPersonCameraDistance()
    {
        if (player is null || body is null)
        {
            return;
        }

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
    }

    private void CheckForToggle()
    {
        //only allow toggling when not in thirdperson and in a cutscene as it is too compilcated to resolve
        if (!reEnable)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
            {
                //MelonDebug.Msg("[FREECAM] toggling");
                if (Enabled)
                {
                    SetDisabled();
                }
                else
                {
                    SetEnabled();
                }
            }
        }
        if (Keyboard.current.uKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
        {
            UIEnabled.Value = !UIEnabled.Value;
        }
    }

    private void DisplayUI()
    {
        string toDisplay = string.Empty;
        if (CanvasGO is null || canvas is null || text is null)
        {
            return;
        }

        if (UIEnabled.Value && camera is not null && game_camera is not null)
        {
            CanvasGO.active = true;
            canvas.scaleFactor = 1.0f;
            string lookingAt = "None";
            string lookingAt2 = "None";
            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hit, float.MaxValue))
            {
                lookingAt = $"{hit.transform.gameObject.name}|{LayerMask.LayerToName(hit.transform.gameObject.layer)}";
            }

            if (player is null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
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
                {
                    lookingAt2 = $"{hit2.transform.gameObject.name}|{LayerMask.LayerToName(hit2.transform.gameObject.layer)}";
                }

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
        if (game_camera != null)
        {
            //MelonDebug.Msg("[FREECAM] initializing");
            //MelonDebug.Msg("[FREECAM] yoinking games camera");
            //ObjectInfo.PrintHierarchy(game_camera.gameObject);
            camera = Object.Instantiate(game_camera.gameObject).GetComponent<Camera>();
            ////MelonDebug.Msg($"[FREECAM] {camera.name}");
            camera.depth = -2;
            ////MelonDebug.Msg($"[FREECAM] {camera.depth}");
            //camera.cullingMask |= 1 << 18; //see head
            camera.cullingMask |= ~0; //see all
                                      ////MelonDebug.Msg($"[FREECAM] {camera.cullingMask}");
            camera.name = "Freecam Camera";
            ////MelonDebug.Msg($"[FREECAM] {camera.name}");
            camera.enabled = false;
            ////MelonDebug.Msg($"[FREECAM] {camera.enabled}");
            camera.gameObject.layer = 3; //0 is default
                                         ////MelonDebug.Msg($"[FREECAM] {camera.gameObject.layer}");
            camera.cameraType = CameraType.Game;
            ////MelonDebug.Msg($"[FREECAM] {camera.cameraType}");
            camera.hideFlags = HideFlags.HideAndDontSave;
            var rect = new Rect(0f, 0f, 1f, 1f);
            var handle = GCHandle.Alloc(rect, GCHandleType.Pinned);
            camera.set_rect_Injected(ref rect);//starting left, bottom, extend up, right
            handle.Free();
            ////MelonDebug.Msg($"[FREECAM] {camera.hideFlags}");

            //MelonDebug.Msg("[FREECAM] deleting camera child objects");
            for (int i = 0; i < camera.transform.childCount; i++)
            {
                Transform child = camera.transform.GetChild(i);
                if (child.gameObject != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            Object.DestroyImmediate(camera.gameObject.GetComponent<CinemachineBrain>());
            Object.DestroyImmediate(camera.gameObject.GetComponent<AudioListener>());
            //audioListener = camera.gameObject.GetComponent<AudioListener>();
            //if (!ThirdPersonMode.Value)
            //audioListener.enabled = false;

            isInitialized = camera.gameObject.GetComponents<MonoBehaviour>().Count > 0;
            //ObjectInfo.PrintHierarchy(camera.gameObject);
            //MelonDebug.Msg("[FREECAM] our own camera was initialized");

            //todo use input system
            //idea: get the actions once, then check their state each update, like we used to do with the keyboards. this will allow us to use controllers as well though. activating with ?

            //PlayerControlManager.add_OnInput_Crouch(new System.Action(() => { }));

            ////MelonDebug.Msg("[FREECAM] Actions");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.actions)
            //{
            //    //MelonDebug.Msg($"[FREECAM]   {item.name} {item.type}");
            //    //MelonDebug.Msg("[FREECAM]   Controls");
            //    foreach (var ctl in item.controls)
            //    {
            //        //MelonDebug.Msg($"[FREECAM]     {ctl.m_Name} {ctl.displayName}");
            //    }
            //    //MelonDebug.Msg("[FREECAM]   Bindings");
            //    foreach (var ctl in item.bindings)
            //    {
            //        //MelonDebug.Msg($"[FREECAM]     {ctl.interactions} {ctl.processors} {ctl.name}");
            //    }
            //    //MelonDebug.Msg("[FREECAM]   Processors");
            //    //MelonDebug.Msg("[FREECAM]     " + item.processors);
            //}

            ////MelonDebug.Msg("[FREECAM] Bindings");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.bindings)
            //{
            //    //MelonDebug.Msg("[FREECAM]   " + item.groups);
            //    //MelonDebug.Msg("[FREECAM]   " + item.action);
            //    //MelonDebug.Msg("[FREECAM]   " + item.interactions);
            //    //MelonDebug.Msg("[FREECAM]   " + item.name);
            //}
            ////MelonDebug.Msg("[FREECAM] Control Schemes");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.controlSchemes)
            //{
            //    //MelonDebug.Msg("[FREECAM]   " + item.bindingGroup);
            //    //MelonDebug.Msg("[FREECAM]   " + item.name);
            //    //MelonDebug.Msg("[FREECAM]   Device Requirements");
            //    foreach (var ctl in item.deviceRequirements)
            //    {
            //        //MelonDebug.Msg($"[FREECAM]     {ctl.controlPath} {ctl.isOptional} {ctl.isAND} {ctl.isOR}");
            //    }

            //}
            ////MelonDebug.Msg("[FREECAM] Devices");
            //foreach (var item in PlayerControlManager.Singleton._playerMap.devices.Value)
            //{
            //    //MelonDebug.Msg("[FREECAM]   " + item.name);
            //    //MelonDebug.Msg("[FREECAM]   " + item.displayName);
            //    //MelonDebug.Msg("[FREECAM]   " + item.layout);
            //}
            return true;
        }
        return false;
    }

    private void Update()
    {
        //run freecam
        if (Enabled)
        {
            //only concern about two cameras at once when in game main
            if (inGameMain && player is not null && game_camera is not null)
            {
                if (!ThirdPersonMode.Value)
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
                }
                else
                {
                    game_camera.GetComponent<CinemachineBrain>().enabled = true;
                }
                if (inCamera && (DateTime.Now - lastImmobilizedPlayer).Milliseconds > 300)
                {
                    TryImmobilizePlayer();
                    lastImmobilizedPlayer = DateTime.Now;
                }
                if (Keyboard.current.vKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
                {
                    MoveCamToPlayerHead();
                    MelonLogger.Msg("Freecam teleported to the player's head.");
                }
                if (Keyboard.current.tKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && !ThirdPersonMode.Value)
                {
                    EnableThirdPerson();
                }
                else if (Keyboard.current.tKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && ThirdPersonMode.Value)
                {
                    DisableThirdPerson();
                }
                //also turn off third person when sex or cutscene starts
                if (ThirdPersonMode.Value && CutSceneManager.CurrentPlayerScene is not null && Enabled)
                {
                    MelonLogger.Msg("Cutscene detected");
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
            if (!ThirdPersonMode.Value || !inGameMain)
            {
                if (Keyboard.current.hKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed && !GameCameraHidden.Value)
                {
                    GameCameraHidden.Value = true;
                    foreach (Camera item in Object.FindObjectsOfType<Camera>())
                    {
                        string name = $"{item.gameObject.name}";
                        if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
                        {
                            item.enabled = false;
                        }
                    }
                }
                else if (Keyboard.current.hKey.wasPressedThisFrame && Keyboard.current.leftAltKey.isPressed)
                {
                    GameCameraHidden.Value = false;
                    foreach (Camera item in Object.FindObjectsOfType<Camera>())
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
        }
        else if (reEnable && inGameMain && player is not null && CutSceneManager.CurrentPlayerScene is null)
        {
            reEnable = false;
            ThirdPersonMode.Value = true;
            SetEnabled();
        }
    }

    private void EnableThirdPerson()
    {
        ThirdPersonMode.Value = true;
        //audioListener.enabled = true;

        interactionDistances.Clear();

        foreach (var item in ItemManager.GetAllItems())
        {
            interactionDistances[item.Name] = item.DistanceToInteraction;
            item.DistanceToInteraction += Patcher.addedDistance;
        }
        SetupThirdPersonCamera();

        if (thirdCameraHolder is not null)
        {
            thirdCameraHolder.active = true;
        }
        if (thirdCamera is not null)
        {
            thirdCamera.enabled = true;
        }
        if (thirdCameraLookAtPosition is not null)
        {
            thirdCameraLookAtPosition.gameObject.active = true;
        }
        if (thirdCameraPosition is not null)
        {
            thirdCameraPosition.gameObject.active = true;
        }
    }

    private void DisableThirdPerson()
    {
        if (game_camera is null || player is null)
        {
            return;
        }

        game_camera.fieldOfView = oldFOV;

        ThirdPersonMode.Value = false;
        if (thirdCameraHolder is not null)
        {
            thirdCameraHolder.active = false;
        }
        if (thirdCamera is not null)
        {
            thirdCamera.enabled = false;
        }
        if (thirdCameraLookAtPosition is not null)
        {
            thirdCameraLookAtPosition.gameObject.active = false;
        }
        if (thirdCameraPosition is not null)
        {
            thirdCameraPosition.gameObject.active = false;
        }

        if (!reEnable)
        {
            //dont see player head, but keep it if we disable for a cutscene
            game_camera.cullingMask &= ~(1 << 18);
        }
        else
        {
            EekCamera.Singleton = game_camera.gameObject.AddComponent<EekCamera>();
        }

        foreach (var item in ItemManager.GetAllItems())
        {
            if (interactionDistances.TryGetValue(item.Name, out float distance))
            {
                item.DistanceToInteraction = distance;
            }
            else
            {
                //2 is default
                item.DistanceToInteraction = 2f;
            }
        }

        player.Head.transform.get_position_Injected(out var ret);
        GCHandle pin = GCHandle.Alloc(ret, GCHandleType.Pinned);
        game_camera.transform.set_position_Injected(ref ret);
        pin.Free();

        if (Enabled)
        {
            SetEnabled();
        }


        rotX = 0;
        rotY = 0;
    }

    private void MoveCamToPlayerHead()
    {
        if (player is null || player.Head is null || player.Head.transform is null || camera is null || camera.transform is null)
            return;
        player.Head.transform.get_position_Injected(out Vector3 playerPos);
        var pinnedPos = GCHandle.Alloc(playerPos, GCHandleType.Pinned);
        camera.transform.set_position_Injected(ref playerPos);
        pinnedPos.Free();
        player.Head.transform.get_rotation_Injected(out Quaternion playerRot);
        var pinnedRot = GCHandle.Alloc(playerRot, GCHandleType.Pinned);
        camera.transform.set_rotation_Injected(ref playerRot);
        pinnedRot.Free();
    }

    private void SetupThirdPersonCamera()
    {
        if (player is not null && game_camera is not null)
        {
            Object.DestroyImmediate(game_camera.gameObject.GetComponent<EekCamera>());
            if (camera is not null)
                Object.DestroyImmediate(camera.gameObject.GetComponent<EekCamera>());

            Object.DestroyImmediate(thirdCameraHolder);
            Object.DestroyImmediate(thirdCameraLookAtPosition?.gameObject);
            Object.DestroyImmediate(thirdCameraPosition?.gameObject);
            thirdCameraHolder = new("ThirdPersonCameraHolder");
            thirdCameraPosition = new GameObject("ThirdCameraPositionHolder").transform;
            thirdCameraRotation = new GameObject("ThirdCameraRotationHolder").transform;
            thirdCameraLookAtPosition = new GameObject("ThirdCameraLookAtHolder").transform;

            ////MelonDebug.Msg("[FREECAM] Setting up the third person camera");
            thirdCamera = thirdCameraHolder.AddComponent<CinemachineVirtualCamera>();
            thirdCamera.Follow = thirdCameraPosition;
            thirdCamera.LookAt = thirdCameraLookAtPosition;

            body = thirdCamera.AddCinemachineComponent<CinemachineTransposer>();
            CinemachineComposer aim = thirdCamera.AddCinemachineComponent<CinemachineComposer>();
            CinemachineBasicMultiChannelPerlin noise = thirdCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

            body.m_XDamping = 0.2f;
            body.m_YDamping = 0.2f;
            body.m_ZDamping = 0.2f;
            body.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;

            body.m_FollowOffset = Vector3.zeroVector;

            aim.m_HorizontalDamping = 0.4f;
            aim.m_VerticalDamping = 0.4f;

            try
            {
                noise.m_NoiseProfile = (NoiseSettings)ScriptableObject.CreateScriptableObjectInstanceFromName("Handheld_wideangle_mild");
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }

        FinalizeThirdPersonCamSetup();
    }

    private void FinalizeThirdPersonCamSetup()
    {
        if (thirdCameraHolder is null)
        {
            return;
        }
        if (thirdCameraRotation is null)
        {
            return;
        }

        if (camera is not null)
        {
            camera.enabled = false;
        }

        if (game_camera is null)
        {
            return;
        }

        thirdCameraHolder.active = true;
        //move cameras back
        foreach (Camera item in Object.FindObjectsOfType<Camera>())
        {
            string name = $"{item.gameObject.name}";
            if (name == "Camera" || name == "MainCamera" || name == "Main Camera" || name.StartsWith("CM_"))
            {
                ////MelonDebug.Msg($"[FREECAM] Moved {item.name} back to fullscreen.");
                item.rect = new Rect(0, 0, 1, 1);
                item.enabled = true;
            }
        }

        //see player head
        game_camera.cullingMask |= 1 << 18;

        thirdCameraRotation.rotation.SetEulerAngles(game_camera.transform.forward);
        rotX = 0;
        rotY = 180;
    }
}
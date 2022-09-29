﻿using EekCharacterEngine;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Reflection;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HPFreecam
{
    public class Freecam : MelonMod
    {
        private FFreecam freecam;

#if DEBUG
        public override void OnApplicationStart()
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
            if (freecam != null)
            {
                freecam.OnUpdate();
            }

#if DEBUG
            if (Keyboard.current[Key.D].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                foreach (var item in Object.FindObjectsOfType<Camera>())
                {
                    if (item.name == "Camera" || item.name == "Second Camera")
                    {
                        ObjectInfo.PrintHierarchy(item.gameObject);
                    }
                }
            }
#endif
        }

        public override void OnGUI()
        {
            if (freecam != null)
            {
                freecam.OnGUI();
            }
        }
    }

    //yoink this class if you need a freecam
    //or just use the mod alongside yours 
    //TODO add UI with coord view, and layer selector
    //todo add options to add new objects, clone oibjects, move objects with UI and with camera movement (ray to next object, and fixed distance from cam)
    internal class FFreecam
    {
        private readonly PlayerControlManager PlayerControl = Object.FindObjectOfType<PlayerControlManager>();
        private readonly float rotRes = 0.15f;
        private readonly float speed = 2.3f;
        private Camera camera;
        private Camera game_camera;
        private bool inCamera = true;
        private bool showUI = false;
        private bool inGameMain = false;
        private bool isEnabled = false;
        private bool isInitialized = false;
        private Rect uiPos = new Rect(10, Screen.height * 0.3f, Screen.width * 0.3f, Screen.height * 0.2f);
        private readonly GUILayoutOption[] Opt = new GUILayoutOption[0];
        private EekAddOns.HousePartyPlayerCharacter player = null;
        private float rotX = 0f;
        private float rotY = 0f;
        private PropertyInfo cutsceneList = new PropertyInfo();
        private PropertyInfo cutsceneCharacterList = new PropertyInfo();

        public FFreecam()
        {
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
                {
                    game_camera = item;
                    Initialize();
                    break;
                }
            }
        }

        public bool Enabled()
        {
            return isEnabled;
        }

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
            if (isEnabled && inGameMain && showUI)
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
            if (game_camera != null)
            {
                //ObjectInfo.PrintHierarchy(game_camera.gameObject);
                camera = Object.Instantiate(game_camera.gameObject).GetComponent<Camera>();
                camera.depth = -2;
                //camera.cullingMask |= 1 << 18; //see head
                camera.cullingMask |= ~0; //see all
                camera.name = "Second Camera";
                camera.enabled = false;
                camera.gameObject.layer = 3; //0 is default
                camera.cameraType = CameraType.Game;

                for (int i = 0; i < camera.transform.GetChildCount(); i++)
                {
                    if (camera.transform.GetChild(i).gameObject != null) Object.DestroyImmediate(camera.transform.GetChild(i).gameObject);
                }

                Object.DestroyImmediate(camera.gameObject.GetComponent<Cinemachine.CinemachineBrain>());

                isInitialized = camera.gameObject.transform.gameObject.GetComponents<MonoBehaviour>().Count > 0;
                //ObjectInfo.PrintHierarchy(camera.gameObject);
                return true;
            }
            return false;
        }

        private void CheckForToggle()
        {
            if (Keyboard.current[Key.F].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                if (Enabled())
                {
                    SetDisabled();
                }
                else
                {
                    SetEnabled();
                }
            }
            if (Keyboard.current[Key.U].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                showUI = !showUI;
            }
        }

        private void Move()
        {
            //run freecam
            if (isEnabled)
            {
                //only concern about two cameras at once when in game main
                if (inGameMain && CutsceneHandle())
                {
                    if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed && inCamera)
                    {
                        inCamera = false;
                        player.IsImmobile = false;
                        MelonLogger.Msg("Control moved to player.");
                    }
                    else if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                    {
                        inCamera = true;
                        player.IsImmobile = true;
                        MelonLogger.Msg("Control moved to the freecam.");
                    }
                    if (Keyboard.current[Key.V].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                    {
                        camera.transform.position = player.Head.transform.position;
                        camera.transform.rotation = player.Head.transform.rotation;
                        MelonLogger.Msg("Freecam teleported to the player's head.");
                    }
                }
                else if (!CutsceneHandle())
                {
                    SetDisabled();
                    return;
                }
                else
                {
                    if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed && inCamera)
                    {
                        inCamera = false;
                        Screen.lockCursor = false;
                        MelonLogger.Msg("Control moved to the UI menu.");
                    }
                    else if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
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
            camera.enabled = false;
            Screen.lockCursor = false;

            //move cameras back
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera" || item.name.StartsWith("CM_"))
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

        private bool CutsceneHandle()
        {
            if (inGameMain && cutsceneList != null && cutsceneCharacterList != null)
            {
                bool enable = true;
                foreach (var scene in cutsceneList.GetValue(null).Cast<List<CutScene>>())
                {
                    foreach (var character in cutsceneCharacterList.GetValue(scene).Cast<List<Character>>())
                    {
                        if (character.IsDLCCharacter)
                        {
                            enable = false;
                        }
                    }
                    if (scene.name == "DC_FenceEvent") enable = true;
                }
                return enable;
            }
            return true;
        }

        public void SetEnabled()
        {
            if (camera == null || !isInitialized)
            {
                foreach (var item in Object.FindObjectsOfType<Camera>())
                {
                    if (item.name == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
                    {
                        game_camera = item;
                        break;
                    }
                }
                if (!Initialize()) return;
            }

            camera.enabled = true;

            //move cameras to top left
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera" || item.name.StartsWith("CM_"))
                {
                    //MelonLogger.Msg($"Moved {item.name} to the top left.");
                    item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);//starting left, bottom, extend up, right
                    break;
                }
            }

            foreach (var property in Il2CppType.Of<CutSceneManager>().GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.GetField))
            {
                //MelonLogger.Msg(property.ToString());
                if (property.PropertyType == Il2CppType.Of<List<CutScene>>())
                {
                    //MelonLogger.Msg("used this one");
                    cutsceneList = property;
                    break;
                }
            }

            foreach (var prop in Il2CppType.Of<CutScene>().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.GetField))
            {
                //MelonLogger.Msg(prop.ToString());
                if (prop.PropertyType == Il2CppType.Of<List<Character>>() && prop.GetMethod != null && prop.SetMethod == null)
                {
                    //MelonLogger.Msg("thats the one");
                    cutsceneCharacterList = prop;
                    break;
                }
            }

            Screen.lockCursor = true;

            inGameMain = SceneManager.GetActiveScene().name == "GameMain";
            if (inGameMain)
            {
                player = Object.FindObjectOfType<EekAddOns.HousePartyPlayerCharacter>();
                player.IsImmobile = true;
            }

            isEnabled = true;
            MelonLogger.Msg("Freecam enabled.");
        }

        //todo, use input system
        public void Update()
        {
            //only move when we are not moving the player
            if (isEnabled && inCamera)
            {
                if (Keyboard.current[Key.W].isPressed)
                {
                    camera.transform.position += camera.transform.forward * speed * Time.deltaTime;
                    //MelonLogger.Msg("Freecam move forward.");
                }
                else if (Keyboard.current[Key.S].isPressed)
                {
                    camera.transform.position -= camera.transform.forward * speed * Time.deltaTime;
                    //MelonLogger.Msg("Freecam move back.");
                }
                if (Keyboard.current[Key.A].isPressed)
                {
                    camera.transform.position -= camera.transform.right * speed * Time.deltaTime;
                    //MelonLogger.Msg("Freecam move left.");
                }
                else if (Keyboard.current[Key.D].isPressed)
                {
                    camera.transform.position += camera.transform.right * speed * Time.deltaTime;
                    //MelonLogger.Msg("Freecam move right.");
                }
                if (Keyboard.current[Key.LeftShift].isPressed)
                {
                    camera.transform.position += camera.transform.up * speed * Time.deltaTime;
                    //MelonLogger.Msg("Freecam move up.");
                }
                else if (Keyboard.current[Key.LeftCtrl].isPressed)
                {
                    camera.transform.position -= camera.transform.up * speed * Time.deltaTime;
                    //MelonLogger.Msg("Freecam move down.");
                }
                //does not work in game when the player is loaded
                if (!inGameMain)
                {
                    rotY += Mouse.current.delta.ReadValue().x;
                    rotX -= Mouse.current.delta.ReadValue().y;
                }
                else
                {
                    rotY += PlayerControl.GetLookValue().x * 0.8f;
                    rotX -= PlayerControl.GetLookValue().y * 0.8f;
                }

                camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, Quaternion.Euler(new Vector3(rotRes * rotX, rotRes * rotY, 0)), 50 * Time.deltaTime);
            }
        }
    }
}

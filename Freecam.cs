using MelonLoader;
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
        private bool inGameMain = false;
        private bool isEnabled = false;
        private bool isInitialized = false;
        private EekAddOns.HousePartyPlayerCharacter player = null;
        private float rotX = 0f;
        private float rotY = 0f;

        public FFreecam()
        {
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" && item.gameObject.GetComponents<MonoBehaviour>().Length > 0)
                {
                    game_camera = item;
                    Initialize();
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

            //update ui
            DisplayUI();
        }

        private void DisplayUI()
        {

        }

        private void Initialize()
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
                    MelonLogger.Msg($"Moved {item.name} back to fullscreen.");
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
            //Cinemachine.CinemachineBrain brain = Object.FindObjectOfType<Cinemachine.CinemachineBrain>();
            foreach (var scene in CutSceneManager.GGONCOBOBOF)
            {
                foreach (var character in scene.LNDLGOCMEOI)
                {
                    if (character.IsDLCCharacter)
                    {
                        return false;
                    }
                }
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
                    }
                }
                Initialize();
            }

            camera.enabled = true;

            //move cameras to top left
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera" || item.name.StartsWith("CM_"))
                {
                    MelonLogger.Msg($"Moved {item.name} to the top left.");
                    item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);//starting left, bottom, extend up, right
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

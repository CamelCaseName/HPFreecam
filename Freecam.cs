using EekAddOns;
using Il2CppSystem;
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

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera")
                {
                    freecam = new FFreecam(item, true);
                    break;
                }
            }
        }

        public override void OnUpdate()
        {
            if (freecam != null)
            {
                freecam.OnUpdate();
            }
        }
    }

    //yoink this class if you need a freecam
    //or just use the mod alongside yours 
    internal class FFreecam
    {
        private bool inCamera = true;
        private bool inGameMain = false;
        private bool isEnabled = false;
        private Camera camera = new Camera();
        private float rotX = 0f;
        private float rotY = 0f;
        private HousePartyPlayerCharacter player = null;
        private readonly float rotRes = 0.15f;
        private readonly float speed = 2.3f;
        private readonly PlayerControlManager PlayerControl = Object.FindObjectOfType<PlayerControlManager>();

        public FFreecam(Camera ccamera, bool useSecond)
        {
            if (useSecond)
            {
                camera = Object.Instantiate(ccamera);
                camera.depth = -2;
                //camera.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                camera.tag = "Second Camera";
                camera.name = "Second Camera";
                camera.enabled = false;
                SceneManager.MoveGameObjectToScene(camera.gameObject, SceneManager.GetActiveScene());
            }
            else
            {
                camera = ccamera;
            }
        }

        public void OnUpdate()
        {
            //toggle freecam with alt+f
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

            //run freecam
            if (isEnabled)
            {
                //only concern about two cameras at once when in game main
                if (inGameMain)
                {
                    if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed && inCamera)
                    {
                        inCamera = false;
                        player.IsImmobile = false;
                    }
                    else if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                    {
                        inCamera = true;
                        player.IsImmobile = true;
                    }
                    if (Keyboard.current[Key.V].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                    {
                        camera.transform.position = player.Head.transform.position;
                        camera.transform.rotation = player.Head.transform.rotation;
                    }
                }
                else
                {
                    if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed && inCamera)
                    {
                        inCamera = false;
                        Screen.lockCursor = false;
                    }
                    else if (Keyboard.current[Key.G].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
                    {
                        inCamera = true;
                        Screen.lockCursor = true;
                    }
                }
                Update();
            }
        }

        public void Update()
        {
            //only move when we are not moving the player
            if (isEnabled && inCamera)
            {
                if (Keyboard.current[Key.W].isPressed)
                {
                    camera.transform.position += camera.transform.forward * speed * Time.deltaTime;
                }
                else if (Keyboard.current[Key.S].isPressed)
                {
                    camera.transform.position -= camera.transform.forward * speed * Time.deltaTime;
                }
                if (Keyboard.current[Key.A].isPressed)
                {
                    camera.transform.position -= camera.transform.right * speed * Time.deltaTime;
                }
                else if (Keyboard.current[Key.D].isPressed)
                {
                    camera.transform.position += camera.transform.right * speed * Time.deltaTime;
                }
                if (Keyboard.current[Key.LeftShift].isPressed)
                {
                    camera.transform.position += camera.transform.up * speed * Time.deltaTime;
                }
                else if (Keyboard.current[Key.LeftCtrl].isPressed)
                {
                    camera.transform.position -= camera.transform.up * speed * Time.deltaTime;
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

        public void SetEnabled()
        {
            camera.enabled = true;

            //move cameras to top left
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera")
                {
                    item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                    break;
                }
            }

            Screen.lockCursor = true;

            inGameMain = SceneManager.GetActiveScene().name == "GameMain";
            if (inGameMain)
            {
                player = Object.FindObjectOfType<HousePartyPlayerCharacter>();
                player.IsImmobile = true;
            }

            isEnabled = true;
        }

        public void SetDisabled()
        {
            camera.enabled = false;
            Screen.lockCursor = false;

            //move cameras back
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera")
                {
                    item.rect = new Rect(0, 0, 1, 1);
                }
            }

            if (player != null)
            {
                player.IsImmobile = false;
            }

            isEnabled = false;
        }

        public bool Enabled()
        {
            return isEnabled;
        }
    }
}

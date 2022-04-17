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

        public override void OnApplicationStart()
        {
            freecam = new FFreecam(Object.FindObjectOfType<Camera>(), false);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            freecam = new FFreecam(Object.FindObjectsOfType<Camera>()[0], true);
        }

        public override void OnUpdate()
        {
            freecam.OnUpdate();
        }
    }

    //yoink this class if you need a freecam
    //or just use the mod alongside yours 
    internal class FFreecam
    {
        private bool inCamera = true;
        private bool inGameMain = false;
        private bool isEnabled = false;
        private bool useSecondCamera = true;
        private Camera camera = new Camera();
        private float rotX = 0f;
        private float rotY = 0f;
        private HousePartyPlayerCharacter player = null;
        private Quaternion rot;
        private readonly float rotRes = 0.15f;
        private readonly float speed = 2.3f;
        private readonly PlayerControlManager PlayerControl = Object.FindObjectOfType<PlayerControlManager>();
        private Vector3 pos;

        public FFreecam(Camera camera, bool useSecond)
        {
            this.camera = camera;
            useSecondCamera = useSecond;
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

            //only concern about two cameras at once when in game main
            if (inGameMain)
            {
                if (Keyboard.current[Key.LeftAlt].wasPressedThisFrame && Enabled() && inCamera)
                {
                    inCamera = false;
                }
                else if (Keyboard.current[Key.LeftAlt].wasReleasedThisFrame && Enabled())
                {
                    inCamera = true;
                }
            }

            //run freecam
            if (Enabled())
            {
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
                    rotY = PlayerControl.GetLookValue().x;
                    rotX = PlayerControl.GetLookValue().y;
                }

                camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, Quaternion.Euler(new Vector3(rotRes * rotX, rotRes * rotY, 0)), 50 * Time.deltaTime);
            }
        }

        public void SetEnabled()
        {
            if (useSecondCamera)
            {
                camera = Object.Instantiate(camera);
                camera.depth = -2;
                //camera.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                camera.tag = "Second Camera";
                camera.name = "Second Camera";
                SceneManager.MoveGameObjectToScene(camera.gameObject, SceneManager.GetActiveScene());
            }

            pos = camera.transform.position;
            rot = camera.transform.rotation;

            //move cameras to top left
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera")
                {
                    item.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                }
            }

            Screen.lockCursor = true;

            inGameMain = SceneManager.GetActiveScene().name == "GameMain";
            if (inGameMain)
            {
                player = Object.FindObjectOfType<HousePartyPlayerCharacter>();
            }

            isEnabled = true;
        }

        public void SetDisabled()
        {
            camera.enabled = false;
            camera.transform.position = pos;
            camera.transform.rotation = rot;
            isEnabled = false;
            Screen.lockCursor = false;

            //move cameras back
            foreach (var item in Object.FindObjectsOfType<Camera>())
            {
                if (item.name == "Camera" || item.name == "MainCamera" || item.name == "Main Camera")
                {
                    item.rect = new Rect(0, 0, 1, 1);
                }
            }
        }

        public bool Enabled()
        {
            return isEnabled;
        }
    }
}

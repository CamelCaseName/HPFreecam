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
        public static bool inGameMain = false;
        private HousePartyPlayerCharacter player = null;

        public override void OnApplicationStart()
        {
            freecam = new FFreecam(Object.FindObjectOfType<Camera>(), false);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            freecam = new FFreecam(Object.FindObjectsOfType<Camera>()[0], true);

            Scene scene = SceneManager.GetActiveScene();
            inGameMain = scene.name == "GameMain";
            if (inGameMain)
            {
                player = Object.FindObjectOfType<HousePartyPlayerCharacter>();
            }
        }

        public override void OnUpdate()
        {
            //toggle freecam with alt+f
            if (Keyboard.current[Key.F].wasPressedThisFrame && Keyboard.current[Key.LeftAlt].isPressed)
            {
                if (freecam.Enabled())
                {
                    freecam.SetDisabled();
                    freecam.SetRotationDisabled();
                    if (inGameMain)
                    {
                        player.IsImmobile = false;
                        Camera temp_camera = Object.FindObjectOfType<Camera>();
                        if ((temp_camera.transform.position - player.Head.position + new Vector3(0, 0.1f, 0) + Vector3.Scale(player.Head.forward, new Vector3(0.2f, 0.2f, 0.2f))).magnitude >= 0.3)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                temp_camera.transform.position = player.Head.position + new Vector3(0, 0.1f, 0) + Vector3.Scale(player.Head.forward, new Vector3(0.2f, 0.2f, 0.2f));
                            }
                        }
                    }
                }
                else
                {
                    if (inGameMain)
                    {
                        player.IsImmobile = true;
                    }
                    else
                    {
                        freecam.SetRotationEnabled();
                    }
                    freecam.SetEnabled();
                }

            }

            //run freecam
            if (freecam.Enabled())
            {
                freecam.Update();
            }
        }
    }

    internal class FFreecam
    {
        private bool isEnabled = false;
        private bool rotationEnabled = false;
        private Camera camera = new Camera();
        private float speed = 2.3f;
        private float speedr = 20f;
        private float rotX = 0f;
        private float rotY = 0f;
        private Vector3 pos;
        private Quaternion rot;
        private bool inGameMain = false;
        private HousePartyPlayerCharacter player = null;

        public FFreecam(Camera camera, bool useSecond)
        {
            this.camera = camera;
            ChangeCamera(useSecond);
        }

        private void ChangeCamera(bool useSecondOne = false)
        {
            if (useSecondOne)
            {
                camera = Object.Instantiate(camera);
                camera.depth = 10;
                camera.rect = new Rect(0f, 0.7f, 0.3f, 0.3f);
                camera.tag = "Second Camera";
                camera.name = "Second Camera";
                SceneManager.MoveGameObjectToScene(camera.gameObject, SceneManager.GetActiveScene());
                
                camera.enabled = false;
            }
        }

        public void Update()
        {
            if (isEnabled)
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
                if (rotationEnabled)
                {
                    rotY += Mouse.current.delta.ReadValue().x;
                    rotX -= Mouse.current.delta.ReadValue().y;

                    camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, Quaternion.Euler( new Vector3(rotX , rotY, 0)), speedr * Time.deltaTime);
                }
            }
        }

        public void SetEnabled()
        {
            camera.enabled = true;
            pos = camera.transform.position;
            rot = camera.transform.rotation;
            isEnabled = true;
            //Screen.lockCursor = false;
            Screen.lockCursor = true;
            inGameMain = SceneManager.GetActiveScene().name == "GameMain";
            if (inGameMain)
            {
                player = Object.FindObjectOfType<HousePartyPlayerCharacter>();
            }
        }

        public void SetDisabled()
        {
            camera.enabled = false;
            camera.transform.position = pos;
            camera.transform.rotation = rot;
            isEnabled = false;
            Screen.lockCursor = false;
        }

        public bool Enabled()
        {
            return isEnabled;
        }

        public void SetRotationEnabled()
        {
            rotationEnabled = true;
        }

        public void SetRotationDisabled()
        {
            rotationEnabled = false;
        }

        public bool RotationEnabled()
        {
            return rotationEnabled;
        }

        public void SetSpeed(float speed)
        {
            this.speed = speed;
        }

        public void SetSpeedr(float speedr)
        {
            this.speedr = speedr;
        }
    }
}

using EekAddOns;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
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
        private readonly PlayerControlManager PlayerControl = Object.FindObjectOfType<PlayerControlManager>();
        private readonly float rotRes = 0.15f;
        private readonly float speed = 2.3f;
        private readonly Camera camera = new Camera();
        private bool inCamera = true;
        private bool inGameMain = false;
        private bool isEnabled = false;
        private HousePartyPlayerCharacter player = null;
        private float rotX = 0f;
        private float rotY = 0f;
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
                camera.gameObject.layer = 3; //0 is default
                //camera.transform.DetachChildren();//dont work
                for (int i = 0; i < camera.transform.GetChildCount(); i++)
                {
                    Object.Destroy(camera.transform.GetChild(i).gameObject);
                }
                camera.cameraType = CameraType.Preview;
                //SceneManager.MoveGameObjectToScene(camera.gameObject, SceneManager.GetActiveScene());
            }
            else
            {
                camera = ccamera;
            }
        }

        public bool Enabled()
        {
            return isEnabled;
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

            isEnabled = false;
            MelonLogger.Msg("Freecam disabled.");
        }

        public void SetEnabled()
        {
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
                player = Object.FindObjectOfType<HousePartyPlayerCharacter>();
                player.IsImmobile = true;
                //MelonLogger.Msg($"Player mouse x: {PlayerControl.GetMousePosition().x} Player mouse x: {PlayerControl.GetMousePosition().y}");
                //PlayerControl.ActivateMovement(); //enable


                //CutSceneManager.KJACEIEEBJB.KENCOPFBJDG //private list of transforms
                //CutSceneManager.KJACEIEEBJB //currently playing cutscene

                /* according to unity docs
                 * Split-screen and picture-in-picture effects require the use of more than one Unity camera. Each Unity camera presents its own view on the player’s screen.
                 * To use a multi-camera split-screen for two players:
                 * For each player, create a layer. For example, for two players, create layers named P1 and P2.
                 * Add two Unity cameras to your Scene, set up their viewports, and give each one its own Cinemachine Brain component.
                 * For each Unity camera, set the Culling Mask to the appropriate layer while excluding the other layer. For example, set the first Unity camera to include layer P1 while excluding P2.
                 * Add 2 Virtual Cameras, one to follow each player to follow the players. Assign each Virtual Camera to a player layer.
                 */

                //doesnt do shit :(
                if (CutSceneManager.KJACEIEEBJB != null)
                {
                    CutSceneManager.KJACEIEEBJB.IEMKCPAJABK.m_OutputCamera.cullingMask &= ~(1 << camera.gameObject.layer); //cinemachinebrain, exclude our layer
                    //camera.cullingMask &= ~(1 << 27); //27 is the camera layer?
                    CutSceneManager.KJACEIEEBJB.IEMKCPAJABK.m_OutputCamera = Object.FindObjectOfType<Camera>();


                    //CutSceneManager.KJACEIEEBJB.IEMKCPAJABK.m_OutputCamera.overrideSceneCullingMask = (ulong)~(1 << camera.gameObject.layer); //cinemachinebrain
                }
            }

            isEnabled = true;
            MelonLogger.Msg("Freecam enabled.");
        }

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
                    //for cutscene we still get input, but the camera position is overriden by the cutscene, and rotation cant be changed, locked as well :(
                    rotY += PlayerControl.GetLookValue().x * 0.8f;
                    rotX -= PlayerControl.GetLookValue().y * 0.8f;
                    //MelonLogger.Msg($"Mouse look value: x=>{rotX} y=>{rotY}.");
                }

                camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, Quaternion.Euler(new Vector3(rotRes * rotX, rotRes * rotY, 0)), 50 * Time.deltaTime);
            }
        }

        private static void PrintChildren(Transform t, string indent)
        {
            int child_count = t.childCount;
            MelonLogger.Msg($"{indent}'<{t.gameObject.GetType().ToString().Replace("UnityEngine.", "")}>{t.gameObject.name}' ({child_count} children) -> Layer [{t.gameObject.layer}] {LayerMask.LayerToName(t.gameObject.layer)}");

            string more_indent;
            if (indent.Length == 1)
            {
                more_indent = "L___";
            }
            else
            {
                more_indent = indent + "____";
            }
            if (child_count > 0)
            {
                for (int i = 0; i < child_count; ++i)
                {
                    var child = t.GetChild(i);
                    PrintChildren(child, more_indent);
                }
            }
        }

        private static void PrintHierarchy(GameObject obj)
        {
            PrintChildren(obj.transform, "*");
        }
    }
}

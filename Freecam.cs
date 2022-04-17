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
            freecam = new FFreecam(Object.FindObjectOfType<Camera>(), false);

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
                        player.Incapacitated = false;
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
                        player.Incapacitated = true;
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
        private float speedr = 60f;
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
                Camera second = new Camera()
                {
                    allowDynamicResolution = camera.allowDynamicResolution,
                    allowHDR = camera.allowHDR,
                    allowMSAA = camera.allowMSAA,
                    aspect = camera.aspect,
                    backgroundColor = camera.backgroundColor,
                    cameraType = camera.cameraType,
                    clearFlags = camera.clearFlags,
                    clearStencilAfterLightingPass = camera.clearStencilAfterLightingPass,
                    cullingMask = camera.cullingMask,
                    cullingMatrix = camera.cullingMatrix,
                    depth = 10,
                    depthTextureMode = camera.depthTextureMode,
                    enabled = true,
                    eventMask = camera.eventMask,
                    farClipPlane = camera.farClipPlane,
                    fieldOfView = camera.fieldOfView,
                    focalLength = camera.focalLength,
                    forceIntoRenderTexture = camera.forceIntoRenderTexture,
                    gateFit = camera.gateFit,
                    hideFlags = camera.hideFlags,
                    layerCullDistances = camera.layerCullDistances,
                    layerCullSpherical = camera.layerCullSpherical,
                    lensShift = camera.lensShift,
                    name = "Second Camera",
                    nearClipPlane = camera.nearClipPlane,
                    nonJitteredProjectionMatrix = camera.nonJitteredProjectionMatrix,
                    opaqueSortMode = camera.opaqueSortMode,
                    orthographic = camera.orthographic,
                    orthographicSize = camera.orthographicSize,
                    overrideSceneCullingMask = camera.overrideSceneCullingMask,
                    pixelRect = camera.pixelRect,
                    projectionMatrix = camera.projectionMatrix,
                    rect = new Rect(0.5f, 0.5f, 0.3f, 0.3f),
                    renderingPath = camera.renderingPath,
                    scene = SceneManager.GetActiveScene(),
                    sensorSize = camera.sensorSize,
                    stereoConvergence = camera.stereoConvergence,
                    stereoSeparation = camera.stereoSeparation,
                    stereoTargetEye = camera.stereoTargetEye,
                    tag = "Second Camera",
                    targetDisplay = camera.targetDisplay,
                    targetTexture = camera.targetTexture,
                    transparencySortAxis = camera.transparencySortAxis,
                    transparencySortMode = camera.transparencySortMode,
                    useJitteredProjectionMatrixForTransparentRendering = camera.useJitteredProjectionMatrixForTransparentRendering,
                    useOcclusionCulling = camera.useOcclusionCulling,
                    usePhysicalProperties = camera.usePhysicalProperties,
                    worldToCameraMatrix = camera.worldToCameraMatrix
                };

                second.transform.position = camera.transform.position;
                second.transform.rotation = camera.transform.rotation;

                SceneManager.MoveGameObjectToScene(second.gameObject, SceneManager.GetActiveScene());
                camera = second;
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
                    Mouse.current.delta.x.clampMax = 90f;
                    Mouse.current.delta.x.clampMin = -90f;
                    Mouse.current.delta.y.clampMax = 360f;
                    Mouse.current.delta.y.clampMin = -360f;
                    rotY += Mouse.current.delta.ReadValue().x;
                    rotX -= Mouse.current.delta.ReadValue().y;
                    camera.transform.localEulerAngles = new Vector3(rotX, rotY, 0) * speedr * Time.deltaTime;
                    if (inGameMain)
                    {
                        player.transform.RotateAround(Vector3.up, camera.transform.rotation.y);
                    }
                }
            }
        }

        public void SetEnabled()
        {
            pos = camera.transform.position;
            rot = camera.transform.rotation;
            isEnabled = true;
            Screen.lockCursor = true;
            inGameMain = SceneManager.GetActiveScene().name == "GameMain";
            if (inGameMain)
            {
                player = Object.FindObjectOfType<HousePartyPlayerCharacter>();
            }
        }

        public void SetDisabled()
        {
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

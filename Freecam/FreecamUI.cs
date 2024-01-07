using Il2CppEekCharacterEngine;
using UnityEngine;
using UnityEngine.UI;

internal static class FreecamUI
{
    internal static Sprite background;
    internal static Sprite checkMark;
    internal static Sprite foldOutOpen;
    internal static Sprite foldOutClosed;
    internal static Sprite mask;
    internal static Sprite sprite;
    internal static Sprite knob;

    internal static void SetPosition(this MonoBehaviour behaviour, Vector2 pos)
    {
        var rect = behaviour.GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0, 0, 0);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);
    }

    internal static void Initialize()
    {
        foreach (Sprite _sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            switch (_sprite.name)
            {
                case "UISprite":
                    sprite = _sprite;
                    break;
                case "UIMask":
                    mask = _sprite;
                    break;
                case "Knob":
                    knob = _sprite;
                    break;
                case "UIFoldOutClosed":
                    foldOutClosed = _sprite;
                    break;
                case "UIFoldOutOpened":
                    foldOutOpen = _sprite;
                    break;
                case "Checkmark":
                    checkMark = _sprite;
                    break;
                case "InputFieldBackground":
                    background = _sprite;
                    break;
            }
        }
    }

    internal static GameObject MakeToggle(GameObject canvas, string Name)
    {
        //1.Create a *Toggle* GameObject then make it child of the *Canvas*.

        GameObject toggle = new GameObject("Toggle" + Name);
        toggle.transform.SetParent(canvas.transform);
        //toggle.layer = LayerMask.NameToLayer("UI");

        //2.Create a Background GameObject then make it child of the Toggle GameObject.
        GameObject backGround = new GameObject("Background" + Name);
        backGround.transform.SetParent(toggle.transform);
        //bg.layer = LayerMask.NameToLayer("UI");

        //3.Create a Checkmark GameObject then make it child of the Background GameObject.
        GameObject checkMarkGO = new GameObject("Checkmark" + Name);
        checkMarkGO.transform.SetParent(backGround.transform);
        //chmk.layer = LayerMask.NameToLayer("UI");

        //4.Create a Label GameObject then make it child of the Toggle GameObject.
        GameObject label = new GameObject("Label" + Name);
        label.transform.SetParent(toggle.transform);
        //lbl.layer = LayerMask.NameToLayer("UI");

        //5.Now attach components like Image, Text and Toggle to each GameObject like it appears in the Editor.
        //Attach Text to label
        Text text = label.AddComponent<Text>();
        text.text = Name;
        text.font = Font.GetDefault();
        text.lineSpacing = 1;
        RectTransform txtRect = text.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0, 0);
        txtRect.anchorMax = new Vector2(1, 1);
        txtRect.localPosition = new Vector3(0, 0, 0);
        txtRect.sizeDelta = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);

        //Attach Image Component to the Checkmark
        Image checkMarkImage = checkMarkGO.AddComponent<Image>();
        checkMarkImage.sprite = checkMark;
        checkMarkImage.type = Image.Type.Simple;

        //Attach Image Component to the Background
        Image bgImage = backGround.AddComponent<Image>();
        bgImage.sprite = background;
        bgImage.type = Image.Type.Sliced;
        RectTransform bgRect = text.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 1);
        bgRect.anchorMax = new Vector2(0, 1);
        bgRect.localPosition = new Vector3(0, 0, 0);
        bgRect.sizeDelta = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height);

        //Attach Toggle Component to the Toggle
        Toggle toggleComponent = toggle.AddComponent<Toggle>();
        toggleComponent.transition = Selectable.Transition.ColorTint;
        toggleComponent.targetGraphic = bgImage;
        toggleComponent.isOn = true;
        toggleComponent.toggleTransition = Toggle.ToggleTransition.Fade;
        toggleComponent.graphic = checkMarkImage;

        return toggle;
    }
}
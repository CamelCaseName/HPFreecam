using MelonLoader;
using UnityEngine;

namespace Freecam;

public static class ObjectInfo
{
    internal static void PrintChildren(Transform t, string indent)
    {
        int child_count = t.childCount;
        MelonLogger.Msg($"{indent}'<{t.gameObject.GetType().ToString().Replace("UnityEngine.", "")}>{t.gameObject.name}' ({child_count} children) -> Layer [{t.gameObject.layer}] {LayerMask.LayerToName(t.gameObject.layer)}");

        string spacer = "|";
        for (int i = 0; i < indent.Length; i++)
        {
            spacer += " ";
        }

        if (child_count > 0 || t.gameObject.GetComponents<MonoBehaviour>().Count > 0)
        {
            MelonLogger.Msg($"{spacer}Components on {t.gameObject.name} ({t.gameObject.GetComponents<MonoBehaviour>().Count} Components)");
            PrintComponents(t.gameObject, "L______");

            string more_indent = indent.Length == 1 ? "L___" : indent + "____";
            for (int i = 0; i < child_count; ++i)
            {
                Transform child = t.GetChild(i);
                PrintChildren(child, more_indent);
            }
        }
        else
        {
            MelonLogger.Msg($"{spacer}no components on {t.gameObject.name}");
        }
    }
    internal static void PrintComponents(GameObject o, string indent)
    {
        if (o.GetComponents<MonoBehaviour>().Count > 0)
        {
            foreach (MonoBehaviour comp in o.GetComponentsInChildren<MonoBehaviour>())
            {
                if (comp != null)
                {
                    MelonLogger.Msg($"{indent}'<{comp.GetType().ToString().Replace("UnityEngine.", "")}>{comp.GetScriptClassName()}' -> Layer [{comp.gameObject.layer}] {LayerMask.LayerToName(comp.gameObject.layer)}");
                }
            }
        }
    }

    public static void PrintHierarchy(GameObject obj) => PrintChildren(obj.transform, "*");

    public static void PrintMethods(GameObject obj)
    {
        MelonLogger.Msg($"Member methods for <{obj.GetType().ToString().Replace("UnityEngine.", "")}>{obj.name}");
        foreach (Il2CppSystem.Reflection.MethodInfo? method in obj.GetIl2CppType().GetMethods())
        {
            MelonLogger.Msg($" - {method.ToString()}");
        }
    }

    public static void PrintFields(GameObject obj)
    {
        MelonLogger.Msg($"Fields on <{obj.GetType().ToString().Replace("UnityEngine.", "")}>{obj.name}");
        foreach (Il2CppSystem.Reflection.FieldInfo? field in obj.GetIl2CppType().GetFields())
        {
            MelonLogger.Msg($" - {field.ToString()} ({(field.IsPublic ? "public" : "private")})");
        }
    }
}

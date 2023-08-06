using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using System;
using System.Collections.Generic;
using Il2CppException = Il2CppInterop.Runtime.Il2CppException;

namespace Freecam;
public static class Il2CppSupport
{
    private static readonly Dictionary<Type, Dictionary<string, IntPtr>> methodStore = new();

    public static unsafe TResult GetProperty<TResult, TObject>(Il2CppObjectBase source, string name)
    {
        if (!typeof(TResult).IsValueType || typeof(TResult).IsAssignableTo(typeof(Il2CppObjectBase))) return default!;
        if (!name.StartsWith("get_"))
            name = "get_" + name;
        IntPtr nativeSourceObject = IL2CPP.Il2CppObjectBaseToPtrNotNull(source);
        if (!methodStore.TryGetValue(typeof(TObject), out var thisDictionary))
        {
            thisDictionary = new();
            methodStore.Add(typeof(TObject), thisDictionary);
        }
        if (thisDictionary.TryGetValue(name, out IntPtr nativeMethod))
        {
            IntPtr nativeClass = IL2CPP.il2cpp_object_get_class(nativeSourceObject);
            nativeMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, name, 0);
        }
        IntPtr nativeException = IntPtr.Zero;
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)IntPtr.Zero, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (typeof(TResult).IsValueType)
            return *(TResult*)IL2CPP.il2cpp_object_unbox(result);
        else if (typeof(TResult) == typeof(string))
            return (TResult)(object)IL2CPP.Il2CppStringToManaged(result)!;
        else
            return (result != IntPtr.Zero) ? Il2CppObjectPool.Get<TResult>(result) : default!;
    }

    public static unsafe TResult GetProperty<TResult, TObject>(Il2CppObjectBase source, string firstProperty, string secondProperty)
    {
        if (!typeof(TResult).IsValueType || typeof(TResult).IsAssignableTo(typeof(Il2CppObjectBase))) return default!;
        if (source is null) return default!;
        if (!firstProperty.StartsWith("get_"))
            firstProperty = "get_" + firstProperty;
        if (!secondProperty.StartsWith("get_"))
            secondProperty = "get_" + secondProperty;

        IntPtr nativeSourceObject = IL2CPP.Il2CppObjectBaseToPtrNotNull(source);
        if (!methodStore.TryGetValue(typeof(TObject), out var thisDictionary))
        {
            thisDictionary = new();
            methodStore.Add(typeof(TObject), thisDictionary);
        }
        if (!thisDictionary.TryGetValue(firstProperty, out IntPtr nativeMethod))
        {
            IntPtr nativeClass = IL2CPP.il2cpp_object_get_class(nativeSourceObject);
            nativeMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, firstProperty, 0);
        }
        IntPtr nativeException = IntPtr.Zero;
        IntPtr firstResult = IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)IntPtr.Zero, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (!methodStore.TryGetValue(typeof(TObject), out var thisSecondDictionary))
        {
            thisSecondDictionary = new();
            methodStore.Add(typeof(TObject), thisSecondDictionary);
        }
        if (!thisSecondDictionary.TryGetValue(secondProperty, out IntPtr nativeSecondMethod))
        {
            IntPtr nativeClass = IL2CPP.il2cpp_object_get_class(firstResult);
            nativeSecondMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, secondProperty, 0);
        }
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(nativeSecondMethod, firstResult, (void**)IntPtr.Zero, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (typeof(TResult).IsValueType)
            return *(TResult*)IL2CPP.il2cpp_object_unbox(result);
        else
            return (result != IntPtr.Zero) ? Il2CppObjectPool.Get<TResult>(result) : default!;
    }
}

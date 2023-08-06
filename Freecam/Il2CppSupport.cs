using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using MelonLoader;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppException = Il2CppInterop.Runtime.Il2CppException;

namespace Freecam;
public static class Il2CppSupport
{
    private static readonly Dictionary<Il2CppSystem.Type, Dictionary<string, nint>> methodStore = new();

    public static unsafe TResult GetProperty<TResult, TObject>(Il2CppObjectBase source, string name)
    {
        if (!typeof(TResult).IsValueType || typeof(TResult).IsAssignableTo(typeof(Il2CppObjectBase))) return default!;
        if (!name.StartsWith("get_"))
            name = "get_" + name;
        nint nativeSourceObject = IL2CPP.Il2CppObjectBaseToPtr(source);
        if (!methodStore.TryGetValue(Il2CppType.From(typeof(TObject)), out var thisDictionary))
        {
            thisDictionary = new();
            methodStore.Add(Il2CppType.From(typeof(TObject)), thisDictionary);
        }
        if (!thisDictionary.TryGetValue(name, out nint nativeMethod))
        {
            nint nativeClass = IL2CPP.il2cpp_object_get_class(nativeSourceObject);
            nativeMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, name, 0);
            Log($"native method pointer for ({typeof(TObject).Name}.{name}) gathered as {nativeMethod}");
            thisDictionary.Add(name, nativeMethod);
        }
        nint nativeException = 0;
        //Log($"invoking {nativeMethod} ({typeof(TObject).Name}.{name}) on {nativeSourceObject}");
        nint result = IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)0, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (typeof(TResult).IsValueType)
            return *(TResult*)IL2CPP.il2cpp_object_unbox(result);
        else if (typeof(TResult) == typeof(string))
            return (TResult)(object)IL2CPP.Il2CppStringToManaged(result)!;
        else
            return (result != 0) ? Il2CppObjectPool.Get<TResult>(result) : default!;
    }

    public static unsafe void SetProperty<TValue, TObject>(Il2CppObjectBase source, string name, TValue value)
    {
        if (!typeof(TValue).IsValueType || typeof(TValue).IsAssignableTo(typeof(Il2CppObjectBase))) return;
        if (!name.StartsWith("set_"))
            name = "set_" + name;
        nint nativeSourceObject = IL2CPP.Il2CppObjectBaseToPtr(source);
        if (!methodStore.TryGetValue(Il2CppType.From(typeof(TObject)), out var thisDictionary))
        {
            thisDictionary = new();
            methodStore.Add(Il2CppType.From(typeof(TObject)), thisDictionary);
        }
        if (!thisDictionary.TryGetValue(name, out nint nativeMethod))
        {
            nint nativeClass = IL2CPP.il2cpp_object_get_class(nativeSourceObject);
            nativeMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, name, 1);
            Log($"native method pointer for {typeof(TObject).Name}.{name} gathered as 0x{nativeMethod:x}");
            thisDictionary.Add(name, nativeMethod);
        }
        nint nativeException = 0;

        nint* param = stackalloc nint[1];
        *param = (nint)Unsafe.AsPointer(ref value);

        IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)param, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);
    }

    public static unsafe TResult GetProperty<TResult, TObject>(Il2CppObjectBase source, string firstProperty, string secondProperty)
    {
        if (!typeof(TResult).IsValueType || typeof(TResult).IsAssignableTo(typeof(Il2CppObjectBase))) return default!;
        if (source is null) return default!;
        if (!firstProperty.StartsWith("get_"))
            firstProperty = "get_" + firstProperty;
        if (!secondProperty.StartsWith("get_"))
            secondProperty = "get_" + secondProperty;

        nint nativeSourceObject = IL2CPP.Il2CppObjectBaseToPtrNotNull(source);
        if (!methodStore.TryGetValue(Il2CppType.From(typeof(TObject)), out var thisDictionary))
        {
            thisDictionary = new();
            methodStore.Add(Il2CppType.From(typeof(TObject)), thisDictionary);
        }
        if (!thisDictionary.TryGetValue(firstProperty, out nint nativeMethod))
        {
            nint nativeClass = IL2CPP.il2cpp_object_get_class(nativeSourceObject);
            nativeMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, firstProperty, 0);
            Log($"native method pointer for {typeof(TObject).Name}.{firstProperty} gathered as 0x{nativeMethod:x}");
            thisDictionary.Add(firstProperty, nativeMethod);
        }
        nint nativeException = 0;
        //Log($"invoking {nativeMethod} ({typeof(TObject).Name}.{name}) on {nativeSourceObject}");
        nint firstResult = IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)0, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        nint nativeFirstReturnType = IL2CPP.il2cpp_method_get_return_type(nativeMethod);
        nint nativeFirstReturnTypeClass = IL2CPP.il2cpp_class_from_type(nativeFirstReturnType);
        nint nativeFirstReturnTypeNamePointer = IL2CPP.il2cpp_class_get_name(nativeFirstReturnTypeClass);
        string nativeFirstReturnTypeName = Marshal.PtrToStringAnsi(nativeFirstReturnTypeNamePointer)!;
        Il2CppSystem.Type firstReturnType = Il2CppType.TypeFromPointer(nativeFirstReturnTypeClass, nativeFirstReturnTypeName);
        if (!methodStore.TryGetValue(firstReturnType, out var thisSecondDictionary))
        {
            thisSecondDictionary = new();
            methodStore.Add(firstReturnType, thisSecondDictionary);
        }
        if (!thisSecondDictionary.TryGetValue(secondProperty, out nint nativeSecondMethod))
        {
            nint nativeClass = IL2CPP.il2cpp_object_get_class(firstResult);
            nativeSecondMethod = IL2CPP.il2cpp_class_get_method_from_name(nativeClass, secondProperty, 0);
            Log($"native method pointer for {nativeFirstReturnTypeName}.{secondProperty} gathered as 0x{nativeMethod:x}");
            thisSecondDictionary.Add(secondProperty, nativeSecondMethod);
        }
        //Log($"invoking {nativeMethod} ({typeof(TObject).Name}.{name}) on {nativeSourceObject}");
        nint result = IL2CPP.il2cpp_runtime_invoke(nativeSecondMethod, firstResult, (void**)0, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (typeof(TResult).IsValueType)
            return *(TResult*)IL2CPP.il2cpp_object_unbox(result);
        else
            return (result != 0) ? Il2CppObjectPool.Get<TResult>(result) : default!;
    }

    private static void Log(string msg)
    {
        MelonLogger.Msg("[Il2CppSupport] " + msg);
    }
}

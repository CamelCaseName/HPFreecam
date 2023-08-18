using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppException = Il2CppInterop.Runtime.Il2CppException;
using String = Il2CppSystem.String;

namespace Freecam;
//this is fine, we know that we are taking the address of sth...
#pragma warning disable CS8500
public static class HousePartySupport
{
    private static readonly Dictionary<Il2CppSystem.Type, Dictionary<string, nint>> methodStore = new();

    public static unsafe TResult GetProperty<TResult, TObject>(TObject source, string name) where TObject : Il2CppObjectBase
    {
        GetProperty(source, name, out TResult result);
        return result;
    }

    public static unsafe void GetProperty<TResult, TObject>(TObject source, string name, out TResult resultObject) where TObject : Il2CppObjectBase
    {
        if (typeof(TResult) != typeof(string))
        {
            if (!typeof(TResult).IsValueType && !typeof(TResult).IsAssignableTo(typeof(Il2CppObjectBase)))
            {
                if (!typeof(TResult).IsValueType)

                    MelonDebug.Error(typeof(TResult).FullName + " is not inheriting from Il2CppObjectBase");
                else
                    MelonDebug.Error(typeof(TResult).FullName + " is not a value type and can't be blitted");
                resultObject = default!;
                return;
            }
        }
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
            Log($"native method pointer for {typeof(TObject).Name}.{name} -> {typeof(TResult).Name} gathered as 0x{nativeMethod:x}");
            thisDictionary.Add(name, nativeMethod);
        }
        nint nativeException = 0;
        //Log($"invoking {nativeMethod} ({typeof(TObject).Name}.{name}) on {nativeSourceObject}");
        nint result = IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)0, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (typeof(TResult).IsValueType)
            resultObject = *(TResult*)IL2CPP.il2cpp_object_unbox(result);
        else if (typeof(TResult) == typeof(string))
            resultObject = (TResult)(object)IL2CPP.Il2CppStringToManaged(result)!;
        else if (result != 0)
            resultObject = IL2CPP.PointerToValueGeneric<TResult>(result, false, false)!;
        else
            resultObject = default!;
    }

    public static unsafe TResult GetProperty<TResult, TObject>(TObject source, string firstProperty, string secondProperty) where TObject : Il2CppObjectBase
    {
        GetProperty(source, firstProperty, secondProperty, out TResult result);
        return result;
    }

    public static unsafe void GetProperty<TResult, TObject>(TObject source, string firstProperty, string secondProperty, out TResult resultObject) where TObject : Il2CppObjectBase
    {
        if (typeof(TResult) != typeof(string))
        {
            if (!typeof(TResult).IsValueType && !typeof(TResult).IsAssignableTo(typeof(Il2CppObjectBase)))
            {
                if (!typeof(TResult).IsValueType)

                    MelonDebug.Error(typeof(TResult).FullName + " is not inheriting from Il2CppObjectBase");
                else
                    MelonDebug.Error(typeof(TResult).FullName + " is not a value type and can't be blitted");
                resultObject = default!;
                return;
            }
        }
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
            GetNativeReturnTypeAndName(nativeMethod, out _, out string firstReturnTypeName);
            Log($"native method pointer for {typeof(TObject).Name}.{firstProperty} -> {firstReturnTypeName} gathered as 0x{nativeMethod:x}");
            thisDictionary.Add(firstProperty, nativeMethod);
        }
        nint nativeException = 0;
        //Log($"invoking {nativeMethod} ({typeof(TObject).Name}.{name}) on {nativeSourceObject}");
        nint firstResult = IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)0, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);
        GetNativeReturnTypeAndName(nativeMethod, out nint nativeFirstReturnTypeClass, out string nativeFirstReturnTypeName);
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
            Log($"native method pointer for {nativeFirstReturnTypeName}.{secondProperty} -> {typeof(TResult).Name} gathered as 0x{nativeMethod:x}");
            thisSecondDictionary.Add(secondProperty, nativeSecondMethod);
        }
        //Log($"invoking {nativeMethod} ({typeof(TObject).Name}.{name}) on {nativeSourceObject}");
        nint result = IL2CPP.il2cpp_runtime_invoke(nativeSecondMethod, firstResult, (void**)0, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);

        if (typeof(TResult).IsValueType)
            resultObject = *(TResult*)IL2CPP.il2cpp_object_unbox(result);
        else if (typeof(TResult) == typeof(string))
            resultObject = (TResult)(object)IL2CPP.Il2CppStringToManaged(result)!;
        else if (result != 0)
            resultObject = IL2CPP.PointerToValueGeneric<TResult>(result, false, false)!;
        else
            resultObject = default!;
    }

    private static unsafe void GetNativeReturnTypeAndName(nint nativeMethod, out nint nativeFirstReturnTypeClass, out string nativeFirstReturnTypeName)
    {
        nint nativeFirstReturnType = IL2CPP.il2cpp_method_get_return_type(nativeMethod);
        nativeFirstReturnTypeClass = IL2CPP.il2cpp_class_from_type(nativeFirstReturnType);
        nint nativeFirstReturnTypeNamePointer = IL2CPP.il2cpp_class_get_name(nativeFirstReturnTypeClass);
        nativeFirstReturnTypeName = Marshal.PtrToStringAnsi(nativeFirstReturnTypeNamePointer)!;
    }

    public static unsafe String New_Il2CppString(string managedString)
    {
        if (managedString is null) return String.Empty;
        fixed (char* chars = managedString)
        {
            return new String(IL2CPP.il2cpp_string_new_utf16(chars, managedString.Length));
        }
    }

    public static unsafe void SetProperty<TValue, TObject>(TObject source, string name, TValue value) where TObject : Il2CppObjectBase
    {
        if (typeof(TValue) != typeof(string))
        {
            if (!typeof(TValue).IsValueType && !typeof(TValue).IsAssignableTo(typeof(Il2CppObjectBase)))
            {
                if (!typeof(TValue).IsValueType)

                    MelonDebug.Error(typeof(TValue).FullName + " is not inheriting from Il2CppObjectBase");
                else
                    MelonDebug.Error(typeof(TValue).FullName + " is not a value type and can't be blitted");
                return;
            }
        }

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
            Log($"native method pointer for {typeof(TObject).Name}.{name}({typeof(TValue).Name}) gathered as 0x{nativeMethod:x}");
            thisDictionary.Add(name, nativeMethod);
        }
        nint nativeException = 0;

        nint* param = stackalloc nint[1];
        if (typeof(TValue) == typeof(string))
            fixed (char* c = (string)(object)value!)
                *param = IL2CPP.il2cpp_string_new_utf16(c, ((string)(object)value!).Length);
        else if (typeof(TValue).IsAssignableTo(typeof(Il2CppObjectBase)))
            *param = IL2CPP.Il2CppObjectBaseToPtrNotNull((Il2CppObjectBase)(object)value!);
        else
            *param = (nint)Unsafe.AsPointer(ref value);

        IL2CPP.il2cpp_runtime_invoke(nativeMethod, nativeSourceObject, (void**)param, ref nativeException);
        Il2CppException.RaiseExceptionIfNecessary(nativeException);
    }

    public static void AddEventListenerFront<TEventContainer>(TEventContainer eventContainer, string eventName, Delegate eventListener)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo? field = null;

        Type domainType = typeof(TEventContainer);

        while (field == null)
        {
            field = domainType.GetField(eventName, flags);
            if (field == null)
                domainType = domainType.BaseType!;
        }

        MulticastDelegate resolveDelegate = (MulticastDelegate)field.GetValue(eventContainer)!;
        Delegate[] subscribers = resolveDelegate.GetInvocationList();

        Delegate currentDelegate = resolveDelegate;
        for (int i = 0; i < subscribers.Length; i++)
            currentDelegate = Delegate.RemoveAll(currentDelegate, subscribers[i])!;

        Delegate[] newSubscriptions = new Delegate[subscribers.Length + 1];
        newSubscriptions[0] = eventListener;
        Array.Copy(subscribers, 0, newSubscriptions, 1, subscribers.Length);

        currentDelegate = Delegate.Combine(newSubscriptions)!;

        field.SetValue(eventContainer, currentDelegate);
    }

    private static void Log(string msg)
    {
        MelonDebug.Msg("[HousePartySupport] " + msg);
    }
}
#pragma warning restore CS8500

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using DynamicPatcher;
using System.Runtime.Caching;
using System.Runtime.ConstrainedExecution;

namespace PatcherYRpp
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Pointer<T>
    {
        public static readonly Pointer<T> Zero = new Pointer<T>(0);
        public Pointer(int value)
        {
            Value = (void*)value;
        }
        public Pointer(long value)
        {
            Value = (void*)value;
        }
        public Pointer(void* value)
        {
            Value = value;
        }
        public Pointer(IntPtr value)
        {
            Value = value.ToPointer();
        }

        public void* Value;
        public ref T Ref { get => ref Unsafe.AsRef<T>(Value); }
        public T Data { get => Unsafe.Read<T>(Value); set => Unsafe.Write(Value, value); }
        public ref T this[int index] { get => ref Unsafe.Add(ref Unsafe.AsRef<T>(Value), index); }

        public bool IsNull { get => this == Zero; }

        public Pointer<TTo> Convert<TTo>()
        {
            return new Pointer<TTo>(Value);
        }

        public static Pointer<T> AsPointer(ref T obj)
        {
            return new Pointer<T>(Unsafe.AsPointer(ref obj));
        }
        public static int TypeSize()
        {
            return Unsafe.SizeOf<T>();
        }

        public static bool operator ==(Pointer<T> value1, Pointer<T> value2) => value1.Value == value2.Value;
        public static bool operator !=(Pointer<T> value1, Pointer<T> value2) => value1.Value != value2.Value;
        public override int GetHashCode() => ((IntPtr)Value).GetHashCode();
        public override bool Equals(object obj) => Value == ((Pointer<T>)obj).Value;
        public override string ToString() => ((IntPtr)Value).ToString();

        public static explicit operator int(Pointer<T> value) => (int)value.Value;
        public static explicit operator void*(Pointer<T> value) => value.Value;
        public static explicit operator long(Pointer<T> value) => (long)value.Value;
        public static implicit operator IntPtr(Pointer<T> value) => (IntPtr)value.Value;

        public static explicit operator Pointer<T>(void* value) => new Pointer<T>(value);
        public static explicit operator Pointer<T>(int value) => new Pointer<T>(value);
        public static explicit operator Pointer<T>(long value) => new Pointer<T>(value);
        public static implicit operator Pointer<T>(IntPtr value) => new Pointer<T>(value);
    }
    public class PointerHandle<T> : CriticalFinalizerObject, IDisposable
    {
        private Pointer<Pointer<T>> fixedPointer;
        public virtual ref Pointer<T> Pointer { get => ref fixedPointer.Ref; }
        public PointerHandle()
        {
            fixedPointer = Marshal.AllocHGlobal(Pointer<Pointer<T>>.TypeSize());
        }
        public PointerHandle(Pointer<T> ptr) : this()
        {
            fixedPointer.Ref = ptr;
        }

        public ref T Ref { get => ref Pointer.Ref; }
        public T Data { get => Pointer.Ref; set => Pointer.Ref = value; }
        public ref T this[int index] { get => ref Pointer[index]; }

        public bool IsNull { get => Pointer == Pointer<T>.Zero; }
        public static implicit operator Pointer<T>(PointerHandle<T> obj) => obj.Pointer;

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                Marshal.FreeHGlobal(fixedPointer);
                disposedValue = true;
            }
        }

        ~PointerHandle()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public static class Helpers
    {

        static public unsafe ref T GetUnmanagedRef<T>(IntPtr ptr, int offset = 0)
        {
            return ref ptr.Convert<T>()[offset];
        }

        static public unsafe Span<T> GetSpan<T>(IntPtr ptr, int length)
        {
            return new Span<T>(ptr.ToPointer(), length);
        }

        public static TTo ForceConvert<TFrom, TTo>(TFrom obj)
        {
            return Unsafe.As<TFrom, TTo>(ref obj);
            //    var ptr = new Pointer<TTo>(Pointer<TFrom>.AsPointer(ref obj));
            //    return ptr.Ref;
        }

        static MemoryCache VirtualFunctionCache = new MemoryCache("virtual functions");
        static public T GetVirtualFunction<T>(IntPtr pThis, int index) where T : Delegate
        {
            IntPtr address = GetVirtualFunctionPointer(pThis, index);

            string key = address.ToString();

            var ret = VirtualFunctionCache.Get(key);
            if (ret == null)
            {
                var policy = new CacheItemPolicy
                {
                    SlidingExpiration = TimeSpan.FromSeconds(60.0)
                };
                VirtualFunctionCache.Set(key, Marshal.GetDelegateForFunctionPointer<T>(address), policy);
                ret = VirtualFunctionCache.Get(key);
            }

            return ret as T;
        }

        static public IntPtr GetVirtualFunctionPointer(IntPtr pThis, int index)
        {
            Pointer<Pointer<IntPtr>> pVfptr = pThis;
            Pointer<IntPtr> vfptr = pVfptr.Data;
            IntPtr address = vfptr[index];

            return address;
        }

        public static void PrintException(Exception e)
        {
            Logger.Log("{0} info: ", e.GetType().FullName);
            Logger.Log("Message: " + e.Message);
            Logger.Log("Source: " + e.Source);
            Logger.Log("TargetSite.Name: " + e.TargetSite.Name);
            Logger.Log("Stacktrace: " + e.StackTrace);

            if (e.InnerException != null)
            {
                PrintException(e.InnerException);
            }
            else
            {
                Logger.Log("");
            }
        }

        public static Pointer<T> Convert<T>(this IntPtr ptr)
        {
            return new Pointer<T>(ptr);
        }
    }
}

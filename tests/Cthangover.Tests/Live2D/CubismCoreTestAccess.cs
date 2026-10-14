using System;
using System.Runtime.InteropServices;
using Cthangover.Live2D.Cubism.Framework;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Safe (pointer-free) access to the native CubismCore arrays of a loaded model
    /// for unit tests. The mod's own bindings return raw float*/IntPtr-pointer
    /// arrays, which cannot be dereferenced from safe test code, so these P/Invoke
    /// declarations use IntPtr and marshal through Marshal.Copy/Marshal.ReadIntPtr.
    /// Signatures mirror CubismCoreBindings one-to-one; the native library is
    /// resolved by the DllImport resolver installed in Live2DTestRuntime.
    /// </summary>
    internal static class CubismCoreTestAccess
    {
        [DllImport("Live2DCubismCore", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr csmGetParameterIds(IntPtr model);

        [DllImport("Live2DCubismCore", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr csmGetParameterValues(IntPtr model);

        [DllImport("Live2DCubismCore", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr csmGetParameterMinimumValues(IntPtr model);

        [DllImport("Live2DCubismCore", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr csmGetParameterMaximumValues(IntPtr model);

        [DllImport("Live2DCubismCore", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr csmGetPartIds(IntPtr model);

        [DllImport("Live2DCubismCore", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr csmGetPartOpacities(IntPtr model);

        /// <summary>Reads a copy of the current parameter values of the model.</summary>
        public static float[] ReadParameterValues(CubismNativeModel model) =>
            ReadFloats(csmGetParameterValues(model.ModelPtr), model.ParameterCount);

        /// <summary>Reads a copy of the parameter minimum bounds of the model.</summary>
        public static float[] ReadParameterMinimums(CubismNativeModel model) =>
            ReadFloats(csmGetParameterMinimumValues(model.ModelPtr), model.ParameterCount);

        /// <summary>Reads a copy of the parameter maximum bounds of the model.</summary>
        public static float[] ReadParameterMaximums(CubismNativeModel model) =>
            ReadFloats(csmGetParameterMaximumValues(model.ModelPtr), model.ParameterCount);

        /// <summary>Reads a copy of the current part opacities of the model.</summary>
        public static float[] ReadPartOpacities(CubismNativeModel model) =>
            ReadFloats(csmGetPartOpacities(model.ModelPtr), model.GetPartCount());

        /// <summary>Writes one parameter value back into the native model.</summary>
        public static void WriteParameterValue(CubismNativeModel model, int parameterIndex, float value)
        {
            var values = ReadParameterValues(model);
            values[parameterIndex] = value;
            Marshal.Copy(values, 0, csmGetParameterValues(model.ModelPtr), values.Length);
        }

        /// <summary>Reads one parameter value from the native model.</summary>
        public static float ReadParameterValue(CubismNativeModel model, int parameterIndex) =>
            ReadParameterValues(model)[parameterIndex];

        /// <summary>Returns all parameter ids of the model, in native order.</summary>
        public static string[] GetParameterIds(CubismNativeModel model)
        {
            var idsPtr = csmGetParameterIds(model.ModelPtr);
            var ids = new string[model.ParameterCount];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(idsPtr, i * IntPtr.Size));
            return ids;
        }

        /// <summary>Returns all part ids of the model, in native order.</summary>
        public static string[] GetPartIds(CubismNativeModel model)
        {
            var idsPtr = csmGetPartIds(model.ModelPtr);
            var count = model.GetPartCount();
            var ids = new string[count];
            for (int i = 0; i < count; i++)
                ids[i] = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(idsPtr, i * IntPtr.Size));
            return ids;
        }

        /// <summary>Resolves a parameter id to its native index, or -1 when absent.</summary>
        public static int FindParameterIndex(CubismNativeModel model, string parameterId)
        {
            var ids = GetParameterIds(model);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == parameterId)
                    return i;
            }

            return -1;
        }

        /// <summary>Resolves a part id to its native index, or -1 when absent.</summary>
        public static int FindPartIndex(CubismNativeModel model, string partId)
        {
            var ids = GetPartIds(model);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == partId)
                    return i;
            }

            return -1;
        }

        private static float[] ReadFloats(IntPtr ptr, int count)
        {
            var values = new float[count];
            Marshal.Copy(ptr, values, 0, count);
            return values;
        }
    }
}

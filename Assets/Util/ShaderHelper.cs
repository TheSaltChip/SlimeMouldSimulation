using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;

namespace Util
{
    public static class ShaderHelper
    {
        public enum DepthMode
        {
            None = 0,
            Depth16 = 16,
            Depth24 = 24
        }

        public const GraphicsFormat RGBA_SFloat = GraphicsFormat.R32G32B32A32_SFloat;
        public const GraphicsFormat DefaultGraphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
        
        private static ComputeShader _clearTextureCompute;
        
        #region ComputeShaders

        /// Convenience method for dispatching a compute shader.
        /// It calculates the number of thread groups based on the number of iterations needed.
        public static void Dispatch(ComputeShader cs, int numIterationsX, int numIterationsY = 1,
            int numIterationsZ = 1, int kernelIndex = 0)
        {
            var threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
            var numGroupsX = CeilToInt(numIterationsX / (float)threadGroupSizes.x);
            var numGroupsY = CeilToInt(numIterationsY / (float)threadGroupSizes.y);
            var numGroupsZ = CeilToInt(numIterationsZ / (float)threadGroupSizes.y);

            cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
        }

        public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
        {
            compute.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);
            return new Vector3Int((int)x, (int)y, (int)z);
        }

        // Read data in append buffer to array
        // Note: this is very slow as it reads the data from the GPU to the CPU
        public static T[] ReadDataFromBuffer<T>(GraphicsBuffer buffer, bool isAppendBuffer)
        {
            var numElements = buffer.count;
            if (isAppendBuffer)
            {
                // Get number of elements in append buffer
                var sizeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(int));
                GraphicsBuffer.CopyCount(buffer, sizeBuffer, 0);
                var bufferCountData = new int[1];
                sizeBuffer.GetData(bufferCountData);
                numElements = bufferCountData[0];
                Release(sizeBuffer);
            }

            // Read data from append buffer
            var data = new T[numElements];
            buffer.GetData(data);

            return data;
        }

        #endregion

        #region Create Buffers

        public static GraphicsBuffer CreateAppendBuffer<T>(int capacity)
        {
            var stride = GetStride<T>();
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, capacity, stride);
            buffer.SetCounterValue(0);
            return buffer;
        }

        public static void CreateStructuredBuffer<T>(ref GraphicsBuffer buffer, int count)
        {
            count = Max(1, count); // cannot create 0 length buffer
            var stride = GetStride<T>();
            var createNewBuffer =
                buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;

            if (!createNewBuffer) return;

            Release(buffer);
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
        }

        public static GraphicsBuffer CreateStructuredBuffer<T>(T[] data)
        {
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Length, GetStride<T>());
            buffer.SetData(data);
            return buffer;
        }

        public static GraphicsBuffer CreateStructuredBuffer<T>(List<T> data) where T : struct
        {
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Count, GetStride<T>());
            buffer.SetData(data);
            return buffer;
        }

        public static int GetStride<T>() => Marshal.SizeOf(typeof(T));

        public static GraphicsBuffer CreateStructuredBuffer<T>(int count)
        {
            return new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, GetStride<T>());
        }


        // Create a graphics buffer containing the given data (Note: data must be blittable)
        public static void CreateStructuredBuffer<T>(ref GraphicsBuffer buffer, T[] data) where T : struct
        {
            // Cannot create 0 length buffer (not sure why?)
            var length = Max(1, data.Length);
            // The size (in bytes) of the given data type
            var stride = GetStride<T>();

            // If buffer is null, wrong size, etc., then we'll need to create a new one
            if (buffer == null || !buffer.IsValid() || buffer.count != length || buffer.stride != stride)
            {
                buffer?.Release();

                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, length, stride);
            }

            buffer.SetData(data);
        }

        // Create a graphics buffer containing the given data (Note: data must be blittable)
        public static void CreateStructuredBuffer<T>(ref GraphicsBuffer buffer, List<T> data) where T : struct
        {
            // Cannot create 0 length buffer (not sure why?)
            var length = Max(1, data.Count);
            // The size (in bytes) of the given data type
            var stride = GetStride<T>();

            // If buffer is null, wrong size, etc., then we'll need to create a new one
            if (buffer == null || !buffer.IsValid() || buffer.count != length || buffer.stride != stride)
            {
                buffer?.Release();

                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, length, stride);
            }

            buffer.SetData(data);
        }

        #endregion

        #region Create Textures

        public static RenderTexture CreateRenderTexture(RenderTexture template)
        {
            RenderTexture renderTexture = null;
            CreateRenderTexture(ref renderTexture, template);
            return renderTexture;
        }

        public static void InitMaterial(Shader shader, ref Material mat)
        {
            if (mat != null && (mat.shader == shader || shader == null)) return;

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            mat = new Material(shader);
        }

        public static RenderTexture CreateRenderTexture(int width, int height, FilterMode filterMode,
            GraphicsFormat format, string name = "Unnamed", DepthMode depthMode = DepthMode.None,
            bool useMipMaps = false)
        {
            var texture = new RenderTexture(width, height, (int)depthMode)
            {
                graphicsFormat = format,
                enableRandomWrite = true,
                autoGenerateMips = false,
                useMipMap = useMipMaps
            };
            texture.Create();

            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
            return texture;
        }

        public static void CreateRenderTexture(ref RenderTexture texture, RenderTexture template)
        {
            if (texture != null)
            {
                texture.Release();
            }

            texture = new RenderTexture(template.descriptor)
            {
                enableRandomWrite = true
            };
            texture.Create();
        }

        public static bool CreateRenderTexture(ref RenderTexture texture, int width, int height, FilterMode filterMode,
            GraphicsFormat format, string name = "Unnamed", DepthMode depthMode = DepthMode.None,
            bool useMipMaps = false)
        {
            if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height ||
                texture.graphicsFormat != format || texture.depth != (int)depthMode || texture.useMipMap != useMipMaps)
            {
                if (texture != null)
                {
                    texture.Release();
                }

                texture = CreateRenderTexture(width, height, filterMode, format, name, depthMode, useMipMaps);
                return true;
            }

            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;

            return false;
        }


        public static void CreateRenderTexture3D(ref RenderTexture texture, RenderTexture template)
        {
            CreateRenderTexture(ref texture, template);
        }

        public static void CreateRenderTexture3D(ref RenderTexture texture, int size, GraphicsFormat format,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat, string name = "Untitled", bool mipmaps = false)
        {
            if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size ||
                texture.volumeDepth != size || texture.graphicsFormat != format)
            {
                //Debug.Log ("Create tex: update noise: " + updateNoise);
                if (texture != null)
                {
                    texture.Release();
                }

                const int numBitsInDepthBuffer = 0;
                texture = new RenderTexture(size, size, numBitsInDepthBuffer)
                {
                    graphicsFormat = format,
                    volumeDepth = size,
                    enableRandomWrite = true,
                    dimension = TextureDimension.Tex3D,
                    useMipMap = mipmaps,
                    autoGenerateMips = false
                };
                texture.Create();
            }

            texture.wrapMode = wrapMode;
            texture.filterMode = FilterMode.Bilinear;
            texture.name = name;
        }

        public static void ClearRenderTexture(RenderTexture source)
        {
            if (_clearTextureCompute == null)
            {
                _clearTextureCompute = Resources.Load<ComputeShader>("ClearTexture");
            }
            
            _clearTextureCompute.SetInt("width", source.width);
            _clearTextureCompute.SetInt("height", source.height);
            _clearTextureCompute.SetTexture(0, "Source", source);
            Dispatch(_clearTextureCompute, source.width, source.height);
        }

        #endregion

        public static GraphicsBuffer CreateArgsBuffer(Mesh mesh, int numInstances)
        {
            const int subMeshIndex = 0;
            var args = new uint[5];
            args[0] = mesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)numInstances;
            args[2] = mesh.GetIndexStart(subMeshIndex);
            args[3] = mesh.GetBaseVertex(subMeshIndex);
            args[4] = 0; // offset

            var argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            argsBuffer.SetData(args);
            return argsBuffer;
        }

        // Create args buffer for instanced indirect rendering (number of instances comes from size of append buffer)
        public static GraphicsBuffer CreateArgsBuffer(Mesh mesh, GraphicsBuffer appendBuffer)
        {
            var argsBuffer = CreateArgsBuffer(mesh, 0);
            SetArgsBufferCount(argsBuffer, appendBuffer);
            return argsBuffer;
        }

        public static void SetArgsBufferCount(GraphicsBuffer argsBuffer, GraphicsBuffer appendBuffer)
        {
            GraphicsBuffer.CopyCount(appendBuffer, argsBuffer, sizeof(uint));
        }

        public static void Release(GraphicsBuffer buffer)
        {
            buffer?.Release();
        }

        /// Releases supplied buffer/s if not null
        public static void Release(params GraphicsBuffer[] buffers)
        {
            foreach (var t in buffers)
            {
                Release(t);
            }
        }

        public static void Release(RenderTexture tex)
        {
            if (tex != null)
            {
                tex.Release();
            }
        }
    }
}
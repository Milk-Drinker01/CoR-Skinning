using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Jobs;
//using UnityEngine.Profiling;

namespace CoR
{

    // same as LinearGPUSkinning.cs except it also has tBuffer and qBuffer
    public class CorGPUSkinning : BaseCorSkinning
    {
        ComputeShader cs;
        ComputeBuffer verticesOutBuffer;
        ComputeBuffer normalsOutBuffer;
        ComputeBuffer tangentsOutBuffer;
        ComputeBuffer boneBuffer;
        ComputeBuffer qBuffer;
        
        int kernel;

        protected override void OnSetup()
        {
            var v = corAsset.vertices;
            var n = corAsset.normals;
            var tang = corAsset.tangents;
            var w = corAsset.boneWeights;
            cs = Resources.Load<ComputeShader>("CorSkinning");
            verticesOutBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            normalsOutBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector3)));
            tangentsOutBuffer = new ComputeBuffer(tang.Length, Marshal.SizeOf(typeof(Vector4)));
            boneBuffer = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(float4x4)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            qBuffer = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);

            kernel = cs.FindKernel("CSMain");

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetBuffer("verticesOutBuffer", verticesOutBuffer);
                materials[i].SetBuffer("normalsOutBuffer", normalsOutBuffer);
                materials[i].SetBuffer("tangentsOutBuffer", tangentsOutBuffer);
            }

            //test = boneBuffer.GetNativeBufferPtr();
            //_boneArray = boneBuffer.BeginWrite<float4x4>(0, bones.Length);
            //_rotationArray = qBuffer.BeginWrite<Quaternion>(0, bones.Length);
            transforms = new TransformAccessArray(bones);

            gatherMatrixJob = new SetBuffersJob()
            {
                //test = test,
                //BoneArray = _boneArray,
                //RotationArray = _rotationArray
            };
        }
        //IntPtr test;
        static int inverseBaseRotationID = Shader.PropertyToID("inverseBaseRotation");
        static int worldToLocalMatrixID = Shader.PropertyToID("worldToLocalMatrix");

        private TransformAccessArray transforms;
        private JobHandle gatherMatrixJobHandle;
        private SetBuffersJob gatherMatrixJob;
        NativeArray<float4x4> _boneArray;
        NativeArray<float4> _rotationArray;

        protected override void CalculateSkinning()
        {
            //Profiler.BeginSample("setup arrays");
            _boneArray = boneBuffer.BeginWrite<float4x4>(0, bones.Length);
            //var nativeArrayMatrix = UnsafeUtility.As<NativeArray<float4x4>, NativeArray<float4x4>>(ref _boneArray);

            _rotationArray = qBuffer.BeginWrite<float4>(0, bones.Length);
            var nativeArrayQuaternion = UnsafeUtility.As<NativeArray<float4>, NativeArray<Quaternion>>(ref _rotationArray);
            //Profiler.EndSample();

            gatherMatrixJob.BoneArray = _boneArray;
            gatherMatrixJob.RotationArray = nativeArrayQuaternion;
            gatherMatrixJobHandle = gatherMatrixJob.ScheduleReadOnly(transforms, 128);
            //gatherMatrixJob.RunReadOnly(transforms);

            //JobHandle.ScheduleBatchedJobs();

            //if you dont want to use jobs/burst compiler uncomment this loop, 
            //and remove the ComputeBufferMode parameters from boneBuffer and qbuffer. 
            //you also have to re-enable boneBuffer.setdata and qBuffer.setData in applySkinning
            //skinningNoJobs();
        }
        protected override void ApplySkinning()
        {
            gatherMatrixJobHandle.Complete();
            //Profiler.BeginSample("endWrite");
            boneBuffer.EndWrite<Matrix4x4>(bones.Length);
            qBuffer.EndWrite<Quaternion>(bones.Length);
            //Profiler.EndSample();

            //Profiler.BeginSample("set data");

            //uncomment these if you arent using jobs
            //boneBuffer.SetData(boneMatrices);
            //qBuffer.SetData(q);

            cs.SetVector(inverseBaseRotationID, vector4FromQuaternion(Quaternion.Inverse(transform.rotation)));
            cs.SetMatrix(worldToLocalMatrixID, transform.worldToLocalMatrix);
            cs.SetBuffer(kernel, "qBuffer", qBuffer);
            cs.SetBuffer(kernel, "boneBuffer", boneBuffer);
            
            cs.SetBuffer(kernel, "verticesOutBuffer", verticesOutBuffer);
            cs.SetBuffer(kernel, "normalsOutBuffer", normalsOutBuffer);
            cs.SetBuffer(kernel, "tangentsOutBuffer", tangentsOutBuffer);
            //Profiler.EndSample();

            //Profiler.BeginSample("dispatch");
            cs.Dispatch(kernel, corAsset.vertices.Length / 128 + 1, 1, 1);
            //Profiler.EndSample();
        }

        //[BurstCompile(FloatPrecision.Medium, FloatMode.Fast)]
        [BurstCompile(CompileSynchronously = true)]
        private struct SetBuffersJob : IJobParallelForTransform
        {
            //[NativeDisableUnsafePtrRestriction]
            //public IntPtr test;
            [WriteOnly]
            public NativeArray<float4x4> BoneArray;
            [WriteOnly]
            public NativeArray<Quaternion> RotationArray;
            public void Execute(int index, TransformAccess transform)
            {
                //unsafe
                //{
                //    ((float4x4*)test)[index] = transform.localToWorldMatrix;
                //}
                BoneArray[index] = transform.localToWorldMatrix;
                RotationArray[index] = transform.rotation;
            }
        }
        void skinningNoJobs()
        {
            //Profiler.BeginSample("bones");
            for (int i = 0; i < bones.Length; i++)
            {
                // Extra: animation doesn't work with scaling
                // e.g EllenCombo4 animation scales arm bone

                //Profiler.BeginSample("reset");
                //bones[i].localScale = Vector3.one;
                //Profiler.EndSample();

                //Profiler.BeginSample("m");
                boneMatrices[i] = bones[i].localToWorldMatrix;
                //Profiler.EndSample();

                //Profiler.BeginSample("q");
                q[i] = bones[i].rotation;
                //Profiler.EndSample();
            }
            //Profiler.EndSample();
        }
        Vector4 vector4FromQuaternion(Quaternion q)
        {
            return new Vector4(q.x, q.y, q.z, q.w);
        }

        public override void Destroy()
        {
            boneBuffer.Dispose();
            verticesOutBuffer.Dispose();
            normalsOutBuffer.Dispose();
            tangentsOutBuffer.Dispose();
            qBuffer.Dispose();
            transforms.Dispose();
        }

    }

}
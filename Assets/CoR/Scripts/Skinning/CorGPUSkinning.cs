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
        ComputeBuffer verticesBuffer;
        ComputeBuffer normalsBuffer;
        ComputeBuffer tangentsBuffer;
        ComputeBuffer boneWeightBuffer;
        ComputeBuffer boneBuffer;
        ComputeBuffer bindPoseBuffer;
        ComputeBuffer bindPoseRotations;
        ComputeBuffer verticesOutBuffer;
        ComputeBuffer normalsOutBuffer;
        ComputeBuffer tangentsOutBuffer;
        ComputeBuffer tBuffer;
        ComputeBuffer qBuffer;
        ComputeBuffer corWeightBuffer;
        int kernel;

        protected override void OnSetup()
        {
            var v = corAsset.vertices;
            var n = corAsset.normals;
            var tang = corAsset.tangents;
            var w = corAsset.boneWeights;
            cs = Resources.Load<ComputeShader>("CorSkinning");
            verticesBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            verticesBuffer.SetData(v);
            normalsBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector3)));
            normalsBuffer.SetData(n);
            tangentsBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector4)));
            tangentsBuffer.SetData(tang);
            verticesOutBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            normalsOutBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector3)));
            tangentsOutBuffer = new ComputeBuffer(tang.Length, Marshal.SizeOf(typeof(Vector4)));
            tBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            qBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            tBuffer.SetData(t);


            //These buffers need to be used on a per-type instance,
            //rather than a per-instance instance
            //this will help save ram
            boneWeightBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(BoneWeight)));
            boneWeightBuffer.SetData(w);
            boneBuffer = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(float4x4)), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            bindPoseBuffer = new ComputeBuffer(corAsset.bindposes.Length, Marshal.SizeOf(typeof(Matrix4x4)));
            bindPoseBuffer.SetData(corAsset.bindposes);
            Quaternion[] bindRotations = new Quaternion[corAsset.bindposes.Length];
            bindPoseRotations = new ComputeBuffer(bindRotations.Length, Marshal.SizeOf(typeof(Vector4)));
            for (int i = 0; i < bindRotations.Length; i++)
            {
                bindRotations[i] = corAsset.bindposes[i].rotation;
            }
            bindPoseRotations.SetData(bindRotations);
            corWeightBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(float)));
            corWeightBuffer.SetData(corAsset.corWeight);



            kernel = cs.FindKernel("CSMain");

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetBuffer("verticesOutBuffer", verticesOutBuffer);
                materials[i].SetBuffer("normalsOutBuffer", normalsOutBuffer);
                materials[i].SetBuffer("tangentsOutBuffer", tangentsOutBuffer);
            }
            

            //_boneArray = boneBuffer.BeginWrite<float4x4>(0, bones.Length);
            //_rotationArray = qBuffer.BeginWrite<Quaternion>(0, bones.Length);
            transforms = new TransformAccessArray(bones);

            gatherMatrixJob = new SetBuffersJob()
            {
                BoneArray = _boneArray,
                RotationArray = _rotationArray
            };
        }

        static int inverseBaseRotationID = Shader.PropertyToID("inverseBaseRotation");
        static int worldToLocalMatrixID = Shader.PropertyToID("worldToLocalMatrix");

        private TransformAccessArray transforms;
        private JobHandle gatherMatrixJobHandle;
        private SetBuffersJob gatherMatrixJob;
        NativeArray<float4x4> _boneArray;
        NativeArray<Quaternion> _rotationArray;

        protected override void CalculateSkinning()
        {
            //Profiler.BeginSample("setup arrays");
            //boneBuffer.EndWrite<Matrix4x4>(bones.Length);
            _boneArray = boneBuffer.BeginWrite<float4x4>(0, bones.Length);
            //var nativeArrayMatrix = UnsafeUtility.As<NativeArray<Matrix4x4>, NativeArray<float4x4>>(ref _boneArray);

            //qBuffer.EndWrite<Quaternion>(bones.Length);
            _rotationArray = qBuffer.BeginWrite<Quaternion>(0, bones.Length);
            //var nativeArrayQuaternion = UnsafeUtility.As<NativeArray<Quaternion>, NativeArray<float4>>(ref _rotationArray);
            //Profiler.EndSample();

            //experimental shit ill have to play with more
            //unsafe
            //{
            //    Buffer.MemoryCopy(boneBuffer.GetNativeBufferPtr().ToPointer(), gatherMatrixJob.BoneArray.GetUnsafePtr(), gatherMatrixJob.BoneArray.Length * Marshal.SizeOf(typeof(Matrix4x4)), gatherMatrixJob.BoneArray.Length * Marshal.SizeOf(typeof(Matrix4x4)));
            //    Buffer.MemoryCopy(qBuffer.GetNativeBufferPtr().ToPointer(), gatherMatrixJob.RotationArray.GetUnsafePtr(), gatherMatrixJob.RotationArray.Length * Marshal.SizeOf(typeof(Quaternion)), gatherMatrixJob.RotationArray.Length * Marshal.SizeOf(typeof(Quaternion)));
            //}

            gatherMatrixJob.BoneArray = _boneArray;
            gatherMatrixJob.RotationArray = _rotationArray;
            gatherMatrixJobHandle = gatherMatrixJob.ScheduleReadOnly(transforms, 128);
            //gatherMatrixJob.RunReadOnly(transforms);

            //JobHandle.ScheduleBatchedJobs();

            //if you dont want to use jobs/burst compiler uncomment this loop, 
            //and remove the ComputeBufferMode parameters from boneBuffer and qbuffer. 
            //skinningNoJobs();
        }
        protected override void ApplySkinning()
        {
            gatherMatrixJobHandle.Complete();
            boneBuffer.EndWrite<Matrix4x4>(bones.Length);
            qBuffer.EndWrite<Quaternion>(bones.Length);

            //gatherMatrixJob.BoneArray.Dispose();
            //gatherMatrixJob.RotationArray.Dispose();

            //Profiler.BeginSample("set data");
            //boneBuffer.SetData(boneMatrices);
            //qBuffer.SetData(q);

            cs.SetVector(inverseBaseRotationID, vector4FromQuaternion(Quaternion.Inverse(transform.rotation)));
            cs.SetBuffer(kernel, "bindPoseRotations", bindPoseRotations);
            cs.SetBuffer(kernel, "qBuffer", qBuffer);

            cs.SetMatrix(worldToLocalMatrixID, transform.worldToLocalMatrix);
            cs.SetBuffer(kernel, "bindBuffer", bindPoseBuffer);
            cs.SetBuffer(kernel, "boneBuffer", boneBuffer);

            cs.SetBuffer(kernel, "verticesBuffer", verticesBuffer);
            cs.SetBuffer(kernel, "g_corWeight", corWeightBuffer);
            cs.SetBuffer(kernel, "normalsBuffer", normalsBuffer);
            cs.SetBuffer(kernel, "tangentsBuffer", tangentsBuffer);
            cs.SetBuffer(kernel, "boneWeightBuffer", boneWeightBuffer);
            cs.SetBuffer(kernel, "verticesOutBuffer", verticesOutBuffer);
            cs.SetBuffer(kernel, "normalsOutBuffer", normalsOutBuffer);
            cs.SetBuffer(kernel, "tangentsOutBuffer", tangentsOutBuffer);
            cs.SetBuffer(kernel, "tBuffer", tBuffer);
            cs.SetBuffer(kernel, "corWeight", corWeightBuffer);
            cs.SetInt("vertCount", corAsset.vertices.Length);
            cs.SetFloat("g_corWeight", globalCorWeight);
            //Profiler.EndSample();

            //Profiler.BeginSample("dispatch");
            cs.Dispatch(kernel, corAsset.vertices.Length / 128 + 1, 1, 1);
            //Profiler.EndSample();
        }

        //[BurstCompile(FloatPrecision.Medium, FloatMode.Fast)]
        [BurstCompile(CompileSynchronously = true)]
        private struct SetBuffersJob : IJobParallelForTransform
        {
            [WriteOnly]
            public NativeArray<float4x4> BoneArray;
            [WriteOnly]
            public NativeArray<Quaternion> RotationArray;
            public void Execute(int index, TransformAccess transform)
            {
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
            verticesBuffer.Dispose();
            normalsBuffer.Dispose();
            tangentsBuffer.Dispose();
            boneWeightBuffer.Dispose();
            boneBuffer.Dispose();
            verticesOutBuffer.Dispose();
            normalsOutBuffer.Dispose();
            tangentsOutBuffer.Dispose();
            tBuffer.Dispose();
            qBuffer.Dispose();
            corWeightBuffer.Dispose();
            transforms.Dispose();
            bindPoseBuffer.Dispose();
            bindPoseRotations.Dispose();
        }

    }

}
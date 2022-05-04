using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
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
            boneWeightBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(BoneWeight)));
            boneWeightBuffer.SetData(w);
            boneBuffer = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));
            bindPoseBuffer = new ComputeBuffer(corAsset.bindposes.Length, Marshal.SizeOf(typeof(Matrix4x4)));
            bindPoseBuffer.SetData(corAsset.bindposes);
            Quaternion[] bindRotations = new Quaternion[corAsset.bindposes.Length];
            bindPoseRotations = new ComputeBuffer(bindRotations.Length, Marshal.SizeOf(typeof(Vector4)));
            for (int i = 0; i < bindRotations.Length; i++)
            {
                bindRotations[i] = corAsset.bindposes[i].rotation;
            }
            bindPoseRotations.SetData(bindRotations);
            verticesOutBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            normalsOutBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector3)));
            tangentsOutBuffer = new ComputeBuffer(tang.Length, Marshal.SizeOf(typeof(Vector4)));
            tBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            qBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector4)));
            tBuffer.SetData(t);

            corWeightBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(float)));
            corWeightBuffer.SetData(corAsset.corWeight);

            kernel = cs.FindKernel("CSMain");

            material.SetBuffer("verticesOutBuffer", verticesOutBuffer);
            material.SetBuffer("normalsOutBuffer", normalsOutBuffer);
            material.SetBuffer("tangentsOutBuffer", tangentsOutBuffer);
        }

        protected override bool ApplySkinning()
        {
            //Profiler.BeginSample("bones");
            for (int j = 0; j < bones.Length; j++)
            {
                // Extra: animation doesn't work with scaling
                // e.g EllenCombo4 animation scales arm bone

                //Profiler.BeginSample("reset");
                //bones[j].localScale = Vector3.one;
                //Profiler.EndSample();

                //Profiler.BeginSample("m");
                boneMatrices[j] = bones[j].localToWorldMatrix;
                //Profiler.EndSample();

                //Profiler.BeginSample("q");
                q[j] = bones[j].rotation;
                //q[j] = bones[j].rotation;
                //Profiler.EndSample();
            }
            //Profiler.EndSample();

            //Profiler.BeginSample("set data");
            boneBuffer.SetData(boneMatrices);
            qBuffer.SetData(q);

            cs.SetVector(Shader.PropertyToID("inverseBaseRotation"), vector4FromQuaternion(Quaternion.Inverse(transform.rotation)));
            cs.SetBuffer(kernel, "bindPoseRotations", bindPoseRotations);
            cs.SetBuffer(kernel, "qBuffer", qBuffer);

            cs.SetMatrix(Shader.PropertyToID("worldToLocalMatrix"), transform.worldToLocalMatrix);
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
            cs.Dispatch(kernel, corAsset.vertices.Length / 64 + 1, 1, 1);
            //Profiler.EndSample();

            // get data is slow: use Graphics.DrawProcedural() instead
            // e.g 18 characers = 12fps, without get = 35fps
            //verticesOutBuffer.GetData(vOut);
            //normalsOutBuffer.GetData(nOut);
            //tangentsOutBuffer.GetData(tOut);

            //Debug.Log(material.name);
            return true;
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
        }

    }

}
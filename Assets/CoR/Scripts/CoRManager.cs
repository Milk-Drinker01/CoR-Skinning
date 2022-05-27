using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Jobs;
using UnityEngine;
//using UnityEngine.Profiling;

namespace CoR
{
    public class CoRManager : MonoBehaviour
    {
        public static CoRManager instance;
        [Range(0, 1)] public float globalCorWeight = 1;
        //public List<BaseCorSkinning> instances = new List<BaseCorSkinning>();
        public Dictionary<CorAsset, CoRData> sortedInstances = new Dictionary<CorAsset, CoRData>();

        ComputeShader cs;
        int kernel;

        private void Awake()
        {
            cs = Resources.Load<ComputeShader>("CorSkinning");
            kernel = cs.FindKernel("CSMain");
        }
        private void LateUpdate()
        {
            cs.SetFloat("g_corWeight", globalCorWeight);
            //Profiler.BeginSample("setup");
            foreach (CorAsset type in sortedInstances.Keys)
            {
                foreach(BaseCorSkinning instance in sortedInstances[type].instances)
                {
                    instance.Skin();
                }
            }
            //Profiler.EndSample();
            //Profiler.BeginSample("complete");
            //JobHandle.ScheduleBatchedJobs();
            //Profiler.EndSample();
            //Profiler.BeginSample("apply");
            foreach (CorAsset type in sortedInstances.Keys)
            {
                setupCS(sortedInstances[type]);
                foreach (BaseCorSkinning instance in sortedInstances[type].instances)
                {
                    instance.Apply();
                }
            }
        }
        void setupCS(CoRData data)
        {
            cs.SetBuffer(kernel, "realIndices", data.realIndicesBuffer);
            cs.SetBuffer(kernel, "verticesBuffer", data.verticesBuffer);
            cs.SetBuffer(kernel, "normalsBuffer", data.normalsBuffer);
            cs.SetBuffer(kernel, "tangentsBuffer", data.tangentsBuffer);
            cs.SetBuffer(kernel, "corWeight", data.corWeightBuffer);
            cs.SetBuffer(kernel, "boneWeightBuffer", data.boneWeightBuffer);
            cs.SetBuffer(kernel, "bindPoseRotations", data.bindPoseRotations);
            cs.SetBuffer(kernel, "bindBuffer", data.bindPoseBuffer);
            cs.SetBuffer(kernel, "g_corWeight", data.corWeightBuffer);
            cs.SetBuffer(kernel, "tBuffer", data.tBuffer);
            cs.SetInt("vertCount", data.vertexCount);
        }
        public void addInstanceType(CorAsset _asset)
        {
            sortedInstances.Add(_asset, new CoRData(_asset));
        }
        public void removeInstanceType(CorAsset _asset)
        {
            sortedInstances[_asset].cleanup();
            sortedInstances.Remove(_asset);
        }
        private void OnDestroy()
        {
            foreach (CorAsset type in sortedInstances.Keys)
            {
                sortedInstances[type].cleanup();
            }
        }
    }
    public class CoRData
    {
        public List<BaseCorSkinning> instances;
        public int vertexCount;

        public ComputeBuffer realIndicesBuffer;
        public ComputeBuffer verticesBuffer;
        public ComputeBuffer normalsBuffer;
        public ComputeBuffer tangentsBuffer;
        public ComputeBuffer boneWeightBuffer;
        public ComputeBuffer bindPoseBuffer;
        public ComputeBuffer bindPoseRotations;
        public ComputeBuffer corWeightBuffer;
        public ComputeBuffer tBuffer;

        public CoRData(CorAsset _asset)
        {
            instances = new List<BaseCorSkinning>();
            vertexCount = _asset.vertices.Length;
            setupBuffers(_asset);
        }

        public void setupBuffers(CorAsset corAsset)
        {
            var v = corAsset.vertices;
            var n = corAsset.normals;
            var tang = corAsset.tangents;
            var w = corAsset.boneWeights;

            realIndicesBuffer = new ComputeBuffer(corAsset.usedBoneIndices.Length, Marshal.SizeOf(typeof(int)));
            realIndicesBuffer.SetData(corAsset.usedBoneIndices);
            verticesBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            verticesBuffer.SetData(v);
            normalsBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector3)));
            normalsBuffer.SetData(n);
            tangentsBuffer = new ComputeBuffer(n.Length, Marshal.SizeOf(typeof(Vector4)));
            tangentsBuffer.SetData(tang);
            tBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(Vector3)));
            tBuffer.SetData(corAsset.pStar);
            boneWeightBuffer = new ComputeBuffer(v.Length, Marshal.SizeOf(typeof(BoneWeight)));
            boneWeightBuffer.SetData(w);
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
        }

        public void cleanup()
        {
            realIndicesBuffer.Dispose();
            verticesBuffer.Dispose();
            normalsBuffer.Dispose();
            tangentsBuffer.Dispose();
            boneWeightBuffer.Dispose();
            bindPoseBuffer.Dispose();
            bindPoseRotations.Dispose();
            corWeightBuffer.Dispose();
            tBuffer.Dispose();
        }
    }
}

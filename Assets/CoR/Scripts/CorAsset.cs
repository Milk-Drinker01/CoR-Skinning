using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace CoR
{

    // caches computed center of rotations
    // [CreateAssetMenu] create from SkinnedCor component
    public class CorAsset : ScriptableObject
    {
        public float sigma = 0.1f;
        public string message = "";

        // note: zero pStar value => same as LBS
        public Vector3[] pStar;

        //public Mesh hdMesh;

        // from mesh. shared between instances e.g can't access sharedMesh.vertices, creates new array on access
        public Matrix4x4[] bindposes;
        public BoneWeight[] boneWeights;
        public int[] usedBones;
        public int[] usedBoneIndices;
        public Vector3[] vertices; // fixed verts. not the same as modifyMesh.vertices
        public Vector3[] normals;
        public Vector4[] tangents;
        public float[] corWeight; // 0 == linear, 1 == cor
        public float globalCorWeight = 1;

#if UNITY_EDITOR

        [System.NonSerialized]
        public Thread processingThread;

        [System.NonSerialized]
        public bool cpuProcessing;

        [System.NonSerialized]
        public bool threadFinished;

        //[System.NonSerialized]
        //Texture2D weightImg;

        //[System.NonSerialized]
        //public Vector2[] uv;

        public void PreProcess(bool useComputeShader, Mesh mesh, Mesh hdMesh, Texture2D weightImg)
        {
            if (hdMesh== null)
            {
                hdMesh = mesh;
            }
            //this.weightImg = weight;
            bindposes = mesh.bindposes;
            boneWeights = mesh.boneWeights;
            vertices = mesh.vertices;
            normals = mesh.normals;
            tangents = mesh.tangents;
            getUsedBones(mesh, hdMesh);
            //uv = mesh.uv;

            corWeight = GetCorWeight(weightImg, mesh.uv);

            // can't access Mesh data outside of main thread
            var param = new ThreadParams();
            param.useComputeShader = useComputeShader;
            param.boneWeights = mesh.boneWeights;
            param.p = mesh.vertices;
           // param.T = mesh.triangles; // three vert indices per tri

            param.hdP = hdMesh.vertices;
            param.hdT = hdMesh.triangles;
            param.hdBoneWeights = hdMesh.boneWeights;

            threadFinished = false;
            if (useComputeShader)
            {
                // can't run compute shader in another thread (can't cancel)
                // finishes fast, don't normally see a message anyway
                param.computeShader = new CSSimilarity(Resources.Load<ComputeShader>("CorSimilarity"));
                PreProcessThread(param);
                UnityEditor.EditorUtility.SetDirty(this);
            }
            else
            {
                cpuProcessing = true;
                processingThread = new Thread(PreProcessThread);
                processingThread.Start(param);
            }

        }

        void getUsedBones(Mesh mesh, Mesh hdMesh)
        {
            int totalBoneCount = bindposes.Length;  //i hope this works

            BoneWeight[] weights = mesh.boneWeights;
            BoneWeight[] hdWeights = hdMesh.boneWeights;

            //get highest bone index
            int highestBone = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight inQuestion = weights[i];
                highestBone = Mathf.Max(inQuestion.boneIndex0, highestBone);
                highestBone = Mathf.Max(inQuestion.boneIndex1, highestBone);
                highestBone = Mathf.Max(inQuestion.boneIndex2, highestBone);
                highestBone = Mathf.Max(inQuestion.boneIndex3, highestBone);
            }
            highestBone++;

            //find which bones actually have weight for this mesh
            float[] weightPerBone = new float[highestBone];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight inQuestion = weights[i];
                weightPerBone[inQuestion.boneIndex0] += inQuestion.weight0;
                weightPerBone[inQuestion.boneIndex1] += inQuestion.weight1;
                weightPerBone[inQuestion.boneIndex2] += inQuestion.weight2;
                weightPerBone[inQuestion.boneIndex3] += inQuestion.weight3;
            }
            List<int> usedBoneList = new List<int>();
            for (int i = 0; i < highestBone; i++)
            {
                if (!Mathf.Approximately(0, weightPerBone[i]))
                {
                    usedBoneList.Add(i);
                }
            }
            usedBones = usedBoneList.ToArray();


            usedBoneIndices = new int[totalBoneCount];
            Matrix4x4[] realBindPoses = new Matrix4x4[usedBones.Length];
            for (int i = 0; i < usedBones.Length; i++)
            {
                usedBoneIndices[usedBones[i]] = i;  //this just felt wrong when typing it out
                realBindPoses[i] = bindposes[usedBones[i]];
            }
            bindposes = realBindPoses;
        }

        float[] GetCorWeight(Texture2D weightImg, Vector2[] uv)
        {
            var corWeight = new float[uv.Length];
            if (weightImg != null)
            {
                var width = weightImg.width;
                var height = weightImg.height;

                for (var i = 0; i < corWeight.Length; i++)
                {
                    var xPos = (uv[i].x * width) % width;
                    var yPos = (uv[i].y * height) % height;
                    var color = weightImg.GetPixel((int)xPos, (int)yPos);
                    corWeight[i] = color.r; // weight 0 (black) => linear, 1 (white) => CoR
                }
            }
            else
            {
                for (var i = 0; i < corWeight.Length; i++)
                {
                    corWeight[i] = 1;
                }
            }
            return corWeight;
        }

        // need to pass in data to avoid error from not running on the main thread e.g can't load computeShader in thread
        public class ThreadParams
        {
            public bool useComputeShader;
            public Vector3[] p; // mesh.vertices;
            //public int[] T; // mesh.triangles; // three vert indices per tri
            public BoneWeight[] boneWeights; // mesh.boneWeights;
            public CSSimilarity computeShader;

            public Vector3[] hdP;
            public int[] hdT;
            public BoneWeight[] hdBoneWeights;
        }

        // based on sections using "3.2 Computation on Triangle Meshes" and "3 Method: SimilarityFunction"
        // Computation on Triangle Meshes
        public void PreProcessThread(object paramObj)
        {
            var param = paramObj as ThreadParams;
            var useComputeShader = param.useComputeShader;
            var p = param.p;
            var hdP = param.hdP;
            var hdT = param.hdT;
            var boneWeights = param.boneWeights;
            var hdBoneWeights = param.hdBoneWeights;

            message = "Processing...";

            // 'Let T be the set of all triangles tαβγ that represent an input model Ω'
            // will use indices instead e.g t[triIndex*3+0] = tα, t[triIndex*3+1] = tβ, t[triIndex*3+2] = tγ
            var sqrSigma = sigma * sigma;
            var vertCount = p.Length;

            var hdTriCount = hdT.Length / 3;
            pStar = new Vector3[vertCount]; //  best translation tp 

            // using special class to help with compute shader
            var w = new BWeight[vertCount];
            for (var i = 0; i < vertCount; i++)
            {
                var wi = new BWeight();
                wi.Add(boneWeights[i]);
                w[i] = wi;
            }

            // ∑tαβγ∈T => foreach triangle
            // based on hd mesh
            var tWeights = new TWeight[hdTriCount];
            for (var triIndex = 0; triIndex < hdTriCount; triIndex++)
            {
                var triWeight = new TWeight();
                var triVertIndex = triIndex * 3;
                var t0 = hdT[triVertIndex + 0];
                var t1 = hdT[triVertIndex + 1];
                var t2 = hdT[triVertIndex + 2];
                var tp0 = hdP[t0];
                var tp1 = hdP[t1];
                var tp2 = hdP[t2];

                // aαβγ = area
                triWeight.area = Vector3.Cross(tp0 - tp1, tp0 - tp2).magnitude * 0.5f;

                // vα+vβ+vγ/3 (positions of tri / 3 => center of triangle)
                triWeight.center = new Vector3((tp0.x + tp1.x + tp2.x) / 3.0f, (tp0.y + tp1.y + tp2.y) / 3.0f, (tp0.z + tp1.z + tp2.z) / 3.0f);

                // wα+wβ+wγ/3 bone weights always < 12
                triWeight.Add(hdBoneWeights[t0]);
                triWeight.Add(hdBoneWeights[t1]);
                triWeight.Add(hdBoneWeights[t2]);
                triWeight.DivideBy(3);

                tWeights[triIndex] = triWeight;
            }
            
            if (useComputeShader)
            {
                // much faster on GPU e.g 10 minutes => 0.2 seconds
                var cs = param.computeShader;
                var bwIn = new BWeightCS[w.Length];
                var twIn = new TWeightCS[tWeights.Length];
                for (var i = 0; i < w.Length; i++)
                {
                    bwIn[i].Setup(w[i]);
                }
                for (var i = 0; i < tWeights.Length; i++)
                {
                    twIn[i].Setup(tWeights[i]);
                }
                cs.Compute(bwIn, twIn, sigma, out pStar);
                cs.Destroy();
            }
            else
            {
                // vα and wα denote the position and skinning weight of vertex α,respectively(and similarly for vertices i, β,and γ).
                // process each vert
                var startTime = System.DateTime.Now;
                for (var vertIndex = 0; vertIndex < vertCount; vertIndex++)
                {
                    // Log CPU info
                    if (vertIndex > 0)
                    {
                        var duration = System.DateTime.Now - startTime;
                        float progress = (float)(vertIndex) / (float)(vertCount);
                        var percentPerSecond = duration.TotalSeconds / progress;
                        var timeRemainning = (1.0f - progress) * percentPerSecond;
                        var d = duration;
                        var r = System.TimeSpan.FromSeconds(timeRemainning);
                        message = string.Format("Processing: {0:0.0%}\nDuration:{1:00}m:{2:00}s\nRemainning:{3:00}m:{4:00}s",
                            progress, d.TotalMinutes, d.Seconds, r.TotalMinutes, r.Seconds);
                    }

                    var wi = w[vertIndex];

                    // ∑tαβγ∈T => sum foreach triangle
                    var top = Vector3.zero;
                    var baseValue = 0.0f;
                    for (var triIndex = 0; triIndex < hdTriCount; triIndex++)
                    {
                        var tw = tWeights[triIndex];
                        // between vert bone weight(4) and tri weights(12)
                        // s(wi, wα+wβ+wγ/3)
                        // wi = bone weights for this vert => 4 bones/weights (some might have zero weight)
                        // wα+wβ+wγ => three bone weights for this vert => 12 bones/weights (some might have zero weight)
                        // note: most vert weights will probably overlap e.g three points of the triangle probably on the same bone, need to combine
                        // e.g wα.boneIndex0 = 3, wb.boneIndex2 = 3

                        // simularity scaled by tri area
                        var similarity = Similarity(wi, tw, sqrSigma) * tw.area;
                        top += tw.center * similarity;
                        baseValue += similarity;
                    }

                    if (baseValue > 0)
                    {
                        pStar[vertIndex] = top / baseValue;
                    }
                    else
                    {
                        // prevents NaN, has the same affect as LBS
                        pStar[vertIndex] = Vector3.zero;
                    }
                }
            }



            for (var i = 0; i < pStar.Length; i++)
            {
                //pStar[i] = Vector3.zero;
            }

            message = "Modified " + System.DateTime.Now.ToString();
            processingThread = null;
            cpuProcessing = false;
            threadFinished = true;
        }

        

        // σ (sigma) is the parameter that controls the width of the exponential kernel. 
        // wp is the weights for a single vertex. it can have up to four bones
        // wv is weight triangle. it can have up to twelve bones .
        // the orig doc makes it look like wp[vertCount], where the index is the vertex and 
        // the value is the weight. Most weights would be zero and any zero weight results in zero similarity.
        // using BWeight2 and TWeight to help with compute shader
        float Similarity(BWeight wp, TWeight wv, float sqrSigma)
        {
            var similarity = 0.0f;
            var sigmaSq = sqrSigma;

            // NOTE:  wP.Length might not equal wV.Length
            for (int jI = 0; jI < wp.count; jI++)
            {
                int j = wp.GetBoneIndex(jI);
                float wvj = wv.GetBoneIndexWeight(j);
                if (wvj == 0)
                {
                    continue;
                }
                float wpj = wp.GetBoneIndexWeight(j);
                for (int kI = 0; kI < wv.count; kI++)
                {
                    int k = wv.GetBoneIndex(kI);
                    if (k <= j)
                    {
                        continue;
                    }
                    float wvk = wv.GetBoneIndexWeight(k);
                    float wpk = wp.GetBoneIndexWeight(k);
                    if (wpk == 0)
                    {
                        continue;
                    }
                    similarity += wpj * wpk * wvj * wvk * Mathf.Exp(-(Mathf.Pow(wpj * wvk - wpk * wvj, 2)) / sqrSigma);
                }
            }
            return similarity;
        }

#endif

    }

}
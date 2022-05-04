using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

namespace CoR
{

    public class TWeight
    {
        public Vector3 center;
        public float area;

        public int[] index = new int[12];
        public float[] weight = new float[12];
        public int count = 0;

        public void Add(BoneWeight bw)
        {
            Add(bw.boneIndex0, bw.weight0);
            Add(bw.boneIndex1, bw.weight1);
            Add(bw.boneIndex2, bw.weight2);
            Add(bw.boneIndex3, bw.weight3);
        }

        // IMPORTANT: must append to existing weights.
        // not an issue with BWeight because they are always unique.
        // e.g adding three BoneWeights => 12 values
        // boneIndex0=1, weight0=0.2f, boneIndex5=1, weight5=0.3f
        // is the same as boneIndex0=1, weight0=0.5f but the similar method will fail
        // e.g 3 is similar to 3 but 1+2 is not similar to 3
        // also reduces calculations even if similar function did work e.g 4-12 => 4-6 normally (shared bones)
        public void Add(int boneIndex, float boneWeight)
        {
            if (boneWeight == 0)
            {
                return;
            }
            for (var i = 0; i < count; i++)
            {
                if (index[i] == boneIndex)
                {
                    // combine
                    weight[i] += boneWeight;
                    return;
                }
            }
            index[count] = boneIndex;
            weight[count] = boneWeight;
            count++;
        }

        public int GetIndex(int i)
        {
            return index[i];
        }

        public float GetWeight(int i)
        {
            return weight[i];
        }
        public int GetBoneIndex(int i)
        {
            return index[i];
        }
        public void DivideBy(float value)
        {
            for (var i = 0; i < count; i++)
            {
                weight[i] /= value;
            }
        }

        public float GetBoneIndexWeight(int boneIndex)
        {
            for (var i = 0; i < count; i++)
            {
                if (index[i] == boneIndex)
                {
                    return weight[i];
                }
            }
            return 0;
        }
    };

    // compute shader
    public struct TWeightCS
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3, boneIndex4, boneIndex5, boneIndex6, boneIndex7, boneIndex8, boneIndex9, boneIndex10, boneIndex11;
        public float boneWeight0, boneWeight1, boneWeight2, boneWeight3, boneWeight4, boneWeight5, boneWeight6, boneWeight7, boneWeight8, boneWeight9, boneWeight10, boneWeight11;
        public int count;
        public float area;
        public Vector3 center;

        public void Setup(TWeight w)
        {
            this.count = 0; // will increase with Add()
            this.area = w.area;
            this.center = w.center;
            for (var i = 0; i < w.count; i++)
            {
                Add(w.index[i], w.weight[i]);
            }
        }

        public void Add(int boneIndex, float boneWeight)
        {
            if (boneWeight == 0)
            {
                return;
            }

            switch (count)
            {
                case 0: boneIndex0 = boneIndex; boneWeight0 = boneWeight; break;
                case 1: boneIndex1 = boneIndex; boneWeight1 = boneWeight; break;
                case 2: boneIndex2 = boneIndex; boneWeight2 = boneWeight; break;
                case 3: boneIndex3 = boneIndex; boneWeight3 = boneWeight; break;
                case 4: boneIndex4 = boneIndex; boneWeight4 = boneWeight; break;
                case 5: boneIndex5 = boneIndex; boneWeight5 = boneWeight; break;
                case 6: boneIndex6 = boneIndex; boneWeight6 = boneWeight; break;
                case 7: boneIndex7 = boneIndex; boneWeight7 = boneWeight; break;
                case 8: boneIndex8 = boneIndex; boneWeight8 = boneWeight; break;
                case 9: boneIndex9 = boneIndex; boneWeight9 = boneWeight; break;
                case 10: boneIndex10 = boneIndex; boneWeight10 = boneWeight; break;
                case 11: boneIndex11 = boneIndex; boneWeight11 = boneWeight; break;
                default:
                    throw new System.Exception("Can only store 12 values");
            }
            count++;
        }

    };

}

#endif
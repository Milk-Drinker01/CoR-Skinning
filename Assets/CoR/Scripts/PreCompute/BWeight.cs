using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

namespace CoR
{

    // same as BoneWeight but with a count and index lookup
    public class BWeight
    {
        public int[] index = new int[4];
        public float[] weight = new float[4];
        public int count = 0;

        public void Add(BoneWeight bw)
        {
            Add(bw.boneIndex0, bw.weight0);
            Add(bw.boneIndex1, bw.weight1);
            Add(bw.boneIndex2, bw.weight2);
            Add(bw.boneIndex3, bw.weight3);
        }

        public void Add(int boneIndex, float boneWeight)
        {
            if (boneWeight == 0)
            {
                return;
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
    public struct BWeightCS
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float boneWeight0, boneWeight1, boneWeight2, boneWeight3;
        public int count;

        // faster to create with BWeight instead of working directly with this class
        public void Setup(BWeight w)
        {
            this.count = 0; // will increase with Add()
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
                default:
                    throw new System.Exception("Can only store four values");
            }
            count++;
        }
    };

}

#endif
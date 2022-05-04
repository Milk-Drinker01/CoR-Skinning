using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

#if UNITY_EDITOR

namespace CoR
{

    public class CSSimilarity
    {
        public ComputeShader cs;

        ComputeBuffer bwBuffer;
        ComputeBuffer twBuffer;
        ComputeBuffer pStarBuffer;

        public void Destroy()
        {
            bwBuffer.Dispose();
            twBuffer.Dispose();
            pStarBuffer.Dispose();
        }

        public CSSimilarity(ComputeShader cs)
        {
            this.cs = cs;
        }

        public void Compute(BWeightCS[] bw, TWeightCS[] tw, float sigma, out Vector3[] pStar)
        {
            var vertCount = bw.Length;

            //cs = Resources.Load<ComputeShader>("CorSimilarity");

            bwBuffer = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(BWeightCS)));
            twBuffer = new ComputeBuffer(tw.Length, Marshal.SizeOf(typeof(TWeightCS)));
            pStarBuffer = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)));
            bwBuffer.SetData(bw);
            twBuffer.SetData(tw);

            var kernel = cs.FindKernel("CSMain");
            cs.SetBuffer(kernel, "bwBuffer", bwBuffer);
            cs.SetBuffer(kernel, "twBuffer", twBuffer);
            cs.SetBuffer(kernel, "pStarBuffer", pStarBuffer);
            cs.SetInt("twCount", twBuffer.count);
            cs.SetFloat("sigma", sigma);

            cs.Dispatch(kernel, vertCount / 64 + 1, 1, 1);

            // get result
            pStar = new Vector3[vertCount];
            pStarBuffer.GetData(pStar);
        }

    }

}

#endif

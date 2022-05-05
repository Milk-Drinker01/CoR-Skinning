using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoR
{

    // NOTE: doesn't use any linear skinning weights
    public class CorCPUSkinning : BaseCorSkinning
    {
        protected Vector3[] vOut; // transformed verts v′
        protected Vector3[] nOut; // normals
        protected Vector4[] tOut; // tangents

        protected override void OnSetup()
        {
            vOut = new Vector3[corAsset.vertices.Length];
            nOut = new Vector3[corAsset.normals.Length];
            tOut = new Vector4[corAsset.tangents.Length];
        }
        protected override void ApplySkinning()
        {
            Quaternion invBaseRot = Quaternion.Inverse(transform.rotation);
            for (int j = 0; j < bones.Length; j++)
            {
                // Extra: animation doesn't work with scaling
                // e.g EllenCombo4 animation scales arm bone

                //Profiler.BeginSample("reset");
                //bones[j].localScale = Vector3.one;
                //Profiler.EndSample();

                //Profiler.BeginSample("m");
                boneMatrices[j] = transform.worldToLocalMatrix * bones[j].localToWorldMatrix * corAsset.bindposes[j];
                //Profiler.EndSample();

                //Profiler.BeginSample("q");
                q[j] = invBaseRot * bones[j].rotation * corAsset.bindposes[j].rotation;
                //Profiler.EndSample();
            }


            var v = corAsset.vertices;
            var n = corAsset.normals;
            var w = corAsset.boneWeights;

            for (var i = 0; i < v.Length; i++)
            {
                var bw = w[i];

                // don't default qOut to identity, summing values
                var qOut = new Quaternion(0, 0, 0, 0);
                var tOut = Vector3.zero; // ~t
                CoRWeight(i, bw.boneIndex0, bw.weight0, ref qOut, ref tOut);
                CoRWeight(i, bw.boneIndex1, bw.weight1, ref qOut, ref tOut);
                CoRWeight(i, bw.boneIndex2, bw.weight2, ref qOut, ref tOut);
                CoRWeight(i, bw.boneIndex3, bw.weight3, ref qOut, ref tOut);

                // 6: Normalize and convert q to rotation matrix R 
                qOut.Normalize();
                //## var qM = Matrix4x4.TRS(Vector3.zero, Rp, Vector3.zero);

                // 8: Compute translation: t2 ←~Rpi + ~t − Rpi (Eq. (3b)) 
                //## Vector3 t2 = tOut - (qOut * t[i]);

                // 9: v′i ← Rvi + t2
                //## vOut[i] = (qOut * v[i]) + t2;

                // combine 8 and 9 (single quat mult)
                vOut[i] = (qOut * (v[i] - t[i])) + tOut;


                // normal => transform direction, not position
                if (bw.weight0 > 0) nOut[i] += bw.weight0 * boneMatrices[bw.boneIndex0].MultiplyVector(n[i]);
                if (bw.weight1 > 0) nOut[i] += bw.weight1 * boneMatrices[bw.boneIndex1].MultiplyVector(n[i]);
                if (bw.weight2 > 0) nOut[i] += bw.weight2 * boneMatrices[bw.boneIndex2].MultiplyVector(n[i]);
                if (bw.weight3 > 0) nOut[i] += bw.weight3 * boneMatrices[bw.boneIndex3].MultiplyVector(n[i]);
                nOut[i].Normalize();
            }


            modifyMesh.vertices = vOut;
            modifyMesh.normals = nOut;
            modifyMesh.tangents = tOut;
        }

        //  q ← wi1q1 ⊕wi2q2 ⊕...⊕wimqm
        // where: qa ⊕qb ={qa + qb if qa ·qb ≥ 0 qa −qb if qa ·qb < 0 
        // (qa ·qb denotes the vector dot product) 
        void CoRWeight(int vertIndex, int boneIndex, float boneWeight, ref Quaternion qOut, ref Vector3 tOut)
        {
            if (boneWeight > 0)
            {
                // 7: LBS in line 7 computes the transformation interpolation of the CoR p∗ 
                tOut += boneWeight * boneMatrices[boneIndex].MultiplyPoint3x4(t[vertIndex]);

                // 5: 
                PlusEqualsAntipodalit(ref qOut, Mult(q[boneIndex], boneWeight));
            }
        }

        Quaternion Mult(Quaternion q, float s)
        {
            return new Quaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }

        //  qa ⊕ qb
        void PlusEqualsAntipodalit(ref Quaternion qa, Quaternion qb)
        {
            var dot = Quaternion.Dot(qa, qb); //  a.x * b.x + a.y* b.y + a.z* b.z + a.w* b.w;
            if (dot >= 0)
            {
                // qa + qb if qa·qb ≥ 0 
                qa.x += qb.x;
                qa.y += qb.y;
                qa.z += qb.z;
                qa.w += qb.w;
            }
            else
            {
                // qa − qb if qa·qb < 0 
                qa.x -= qb.x;
                qa.y -= qb.y;
                qa.z -= qb.z;
                qa.w -= qb.w;
            }
        }
    }

}
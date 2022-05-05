using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;
namespace CoR
{

    public abstract class BaseCorSkinning
    {
        // store mesh data in corAsset (shared)
        // never access from mesh (getter creates a new array)
        protected Transform transform; // of this mesh
        protected Transform[] bones;
        protected Mesh modifyMesh;
   
        protected Matrix4x4[] boneMatrices;

        // not used in SkinnedLinear 
        protected Vector3[] t; // pStar or p*
        protected Quaternion[] q; // unit quaternion qj for each bone

        // shared mesh settings in asset e.g includes vertices
        // can't access from mesh (creates new arrays)
        protected CorAsset corAsset;
        public Material[] materials;
        protected float globalCorWeight = 1;

        public void Setup(CorAsset corAsset, Transform[] bones,  GameObject gameObject, Mesh modifyMesh, Material[] m)
        {
            materials = m;
            this.corAsset = corAsset;
            this.transform = gameObject.transform;
            this.bones = bones;
            this.modifyMesh = modifyMesh;
            t = corAsset.pStar;
            q = new Quaternion[bones.Length];
            boneMatrices = new Matrix4x4[bones.Length];

            OnSetup();
        }

    

        protected virtual void OnSetup()
        {

        }

        [BurstCompile(CompileSynchronously = true)]
        public void Skin(float globalCorWeight)
        {
            this.globalCorWeight = globalCorWeight;

            // apply skinning
            ApplySkinning();
        }

        // return true if the modify mesh should be updated
        protected abstract void ApplySkinning();

        // required for compute shader
        public virtual void Destroy()
        {

        }
    }

}
using UnityEngine;
using UnityEngine.Rendering;

namespace CoR
{
    //[ExecuteInEditMode]
    public class SkinnedCor : MonoBehaviour
    {
        public CorAsset corAsset;
        public Mesh optionalHdMesh;
        public Texture2D weightTexture;
        BaseCorSkinning skinning;
  
        // only keeping values for switching modes
        Mesh modifyMesh;
        Transform[] bones;
        Material[] materials;
        int vertexCount;
        Material mat;
        bool initialized = false;

        public bool gpuEnabled {
            get
            {
                return SystemInfo.supportsComputeShaders;
            }
        }

        private void Awake()
        {
            initializeSkinning();
        }
        private void OnEnable()
        {
            initializeSkinning();
        }
        private void initializeSkinning()
        {
            if (!enabled || initialized)
            {
                return;
            }
            if (corAsset == null)
            {
                throw new System.Exception("CoRAsset required. Click 'Create CoR Asset'");
            }
            if (corAsset.pStar.Length == 0)
            {
                throw new System.Exception("Need to pre process core asset. ");
            }
            var skin = GetComponent<SkinnedMeshRenderer>();
            if (skin == null)
            {
                throw new System.Exception("SkinnedMeshRenderer required");
            }

            // doesn't seem to animate the bones if it doesn't find a skinned mesh
            // TODO: find a better way to handle this
            var anim = GetComponentInParent<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            bones = skin.bones;
            var mf = gameObject.AddComponent<MeshFilter>();
            vertexCount = skin.sharedMesh.vertexCount;
            mat = skin.materials[0];
            modifyMesh = (Mesh)GameObject.Instantiate(skin.sharedMesh); // clone
            //modifyMesh.MarkDynamic(); // Optimize mesh for frequent updates.
            mf.mesh = modifyMesh;
            modifyMesh.RecalculateBounds();

            materials = skin.sharedMaterials;

            var meshRend = gameObject.AddComponent<MeshRenderer>();
            meshRend.shadowCastingMode = skin.shadowCastingMode;
            meshRend.allowOcclusionWhenDynamic = false;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = mat;
            }
            meshRend.materials = materials;

            skin.enabled = false;
            Destroy(skin);

            ChangeSkinning();
            initialized = true;
        }
       
        private void ChangeSkinning()
        {
            if (skinning != null)
            {
                skinning.Destroy();
                skinning = null;
            }

            if (gpuEnabled)
            {
                skinning = new CorGPUSkinning();
            }
            else
            {
                skinning = new CorCPUSkinning();
            }

            skinning.Setup(corAsset, bones, gameObject, modifyMesh, mat);
        }

        // FixedUpdate(), LateUpdate() or  Update(). Using FixedUpdate() for testing 
        void LateUpdate()
        {
            skinning.Skin(corAsset.globalCorWeight);

            return;
            Graphics.DrawProcedural(
            mat,
            new Bounds(transform.position, transform.lossyScale * 5),
            MeshTopology.Triangles, vertexCount, 1,
            null, null,
            ShadowCastingMode.Off, true, gameObject.layer);
        }

        void OnDestroy()
        {
            if (skinning != null)
            {
                skinning.Destroy();
                skinning = null;
            }
        }

    }

}
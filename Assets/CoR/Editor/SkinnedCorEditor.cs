using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace CoR
{

    [CustomEditor(typeof(SkinnedCor))]
    public class SkinnedCorEditor : Editor
    {
        SerializedProperty corAsset;
        SerializedProperty hdMesh;

        void OnEnable()
        {
            corAsset = serializedObject.FindProperty("corAsset");
            hdMesh = serializedObject.FindProperty("optionalHdMesh");
        }

        public override void OnInspectorGUI()
        {
            var skinnedCor = (SkinnedCor)target;
            var skinnedRenderer = skinnedCor.gameObject.GetComponent<SkinnedMeshRenderer>();
            if (Application.isPlaying == false && skinnedRenderer == null)
            {
                EditorGUILayout.HelpBox("Requires a SkinnedMeshRenderer component", MessageType.Error);
                return;
            }

            var asset = skinnedCor.corAsset;

            serializedObject.Update();
            EditorGUILayout.PropertyField(corAsset);
            EditorGUILayout.PropertyField(hdMesh);

            if (asset != null)
            {
                asset.globalCorWeight = EditorGUILayout.Slider("CoR Weight", asset.globalCorWeight, 0, 1);
            }
            serializedObject.ApplyModifiedProperties();
            
            var modeMsg = (skinnedCor.gpuEnabled ? "GPU " : "CPU");

            if (asset == null)
            {
                if (GUILayout.Button("Create CoR Asset"))
                {
                    var obj = PrefabUtility.GetCorrespondingObjectFromSource(skinnedCor.gameObject);
                    var prefabPath = AssetDatabase.GetAssetPath(obj);
                    var assetPath = Path.GetDirectoryName(prefabPath);
                    var fileName = Path.GetFileNameWithoutExtension(prefabPath);
                    var meshName = skinnedRenderer.sharedMesh.name;
                    var savePath = Path.Combine(assetPath, fileName + "_" + meshName + ".asset");
                    if (File.Exists(savePath))
                    {
                        Debug.LogError("Asset already exists: " + savePath + ". Rename it or add it to this object");
                        return;
                    }

                    asset = ScriptableObject.CreateInstance<CorAsset>();
                    asset.message = "New asset. Need to click 'Pre Process' button";
                    AssetDatabase.CreateAsset(asset, savePath);
                    AssetDatabase.SaveAssets();
                    //EditorUtility.FocusProjectWindow();
                    //Selection.activeObject = asset;
                    skinnedCor.corAsset = asset;
                    EditorUtility.SetDirty(asset);
                }
            }
            else if (Application.isPlaying == false)
            {
              
                asset.sigma = EditorGUILayout.FloatField("Sigma", asset.sigma);

                //asset.hdMesh = (Mesh) EditorGUILayout.ObjectField("HD Mesh", asset.hdMesh, typeof(Mesh), false);
                if (asset.threadFinished)
                {
                    // finished processing, save changes (can't call in main thread)
                    UnityEditor.EditorUtility.SetDirty(this);
                    asset.threadFinished = false;
                }
                if (asset.processingThread == null && asset.cpuProcessing == false)
                {
                    // 0 == linear, 1 = CoR
                    skinnedCor.weightTexture = (Texture2D) EditorGUILayout.ObjectField("Optional Weight Map", skinnedCor.weightTexture, typeof(Texture2D), false);
                    if (SystemInfo.supportsComputeShaders)
                    {
                        if (GUILayout.Button("Compute Shader Pre Process"))
                        {
                            asset.PreProcess(true, skinnedRenderer.sharedMesh, (Mesh)hdMesh.objectReferenceValue, skinnedCor.weightTexture);
                        }
                    } else
                    {
                        if (GUILayout.Button("CPU Pre Process (slow)"))
                        {
                            asset.PreProcess(false, skinnedRenderer.sharedMesh, (Mesh)hdMesh.objectReferenceValue, skinnedCor.weightTexture);
                        }
                    }
                }
                else if (asset.processingThread != null)
                {
                    Repaint(); // refresh info message (progress)
                    if (GUILayout.Button("Cancel Pre Process"))
                    {
                        asset.processingThread.Abort();
                        asset.processingThread = null;
                        asset.cpuProcessing = false;
                        asset.message = "Canceled";
                    }
                }

                EditorGUILayout.HelpBox(modeMsg + "\n" + asset.message, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(modeMsg, MessageType.Info);
            }

        }

    }

}

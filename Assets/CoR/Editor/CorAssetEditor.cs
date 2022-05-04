using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CoR
{
    
    // hides details
    [CustomEditor(typeof(CorAsset))]
    public class CorAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = target as CorAsset;
            EditorGUILayout.HelpBox("Generated from the SkinnedCor component\n" + asset.message, MessageType.Info);
        }
    }

}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CoR
{

    public class CoRSample : MonoBehaviour
    {
        private SkinnedCor skinnedCor;

        private void Awake()
        {
            skinnedCor = GetComponent<SkinnedCor>();
        }

        private void OnGUI()
        {
            if (skinnedCor == null)
            {
                return;
            }
            GUILayout.Label("");
            GUILayout.BeginHorizontal();
            GUILayout.Label("  CoR Weight: ");
            skinnedCor.corAsset.globalCorWeight = GUILayout.HorizontalSlider( skinnedCor.corAsset.globalCorWeight, 0, 1, GUILayout.Width(150));
            GUILayout.EndHorizontal();
        }
    }

}
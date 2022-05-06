using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CoR
{
    public class CoRManager : MonoBehaviour
    {
        public static CoRManager instance;
        public List<BaseCorSkinning> instances = new List<BaseCorSkinning>();

        private void LateUpdate()
        {
            foreach(BaseCorSkinning s in instances)
            {
                s.Skin(1);
            }
            foreach (BaseCorSkinning s in instances)
            {
                s.Apply();
            }
        }
    }
}

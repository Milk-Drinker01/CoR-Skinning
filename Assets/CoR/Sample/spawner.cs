using CoR;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spawner : MonoBehaviour
{
    public GameObject prefab;
    public GameObject prefabOldMethod;

    public int x;
    public bool _useNewSkinning;

    public static spawner instance;
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        if (!_useNewSkinning)
        {
            prefab = prefabOldMethod;
        }
        for (int i = 0; i < x; i++)
        {
            for (int j = 0; j < x; j++)
            {
                GameObject go = Instantiate(prefab, new Vector3(i, 0, j), Quaternion.identity);
                go.SetActive(true);
                if (_useNewSkinning)
                {
                    go.GetComponentInChildren<SkinnedCor>().enabled = true;
                }
            }
        }
    }
}

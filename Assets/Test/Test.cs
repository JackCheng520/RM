using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TG.AssetBundleRM;
using System.IO;

public class Test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        RM.Instance.Init();
        for (int i = 0; i < 5; i++)
        {
            RM.Instance.LoadAsync("Res/Prefabs/UI/Main/MainWindow", typeof(GameObject), (obj) =>
            {
                (obj as GameObject).transform.SetParent(GameObject.Find("Canvas").transform, false);
            }, true);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetWidthHeight : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

            // 例：把 2400×1080 的机器降到 1600×720
            int targetW = Screen.currentResolution.width  / 10;
            int targetH = Screen.currentResolution.height / 10;
            Screen.SetResolution(targetW, targetH, true);   // 全屏

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

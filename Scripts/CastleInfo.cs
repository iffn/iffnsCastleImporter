#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[SelectionBase]
public class CastleInfo : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] Object saveFile;
    [SerializeField] GameObject removalObject;
    //[SerializeField] bool keepMeshUnique = false;

    public Object SaveFile
    {
        get
        {
            return saveFile;
        }
    }

    public void SetTransform(Transform castle)
    {
        if (removalObject)
        {
            DestroyImmediate(removalObject);
        }


        castle.parent = transform;

        castle.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
    
    /*
    public bool KeepMeshUnique
    {
        get
        {
            return keepMeshUnique;
        }
    }
    */
}
#endif
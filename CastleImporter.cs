# if UNITY_EDITOR

using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace iffnsStuff.iffnsUnityTools.CastleBuilderTools.CastleImporter
{
    public class CastleImporter : EditorWindow
    {
        public List<MaterialLibrary> currentLibraries;
        string folderLocation = "StreamingAssets/Exports";
        string fileName;

        public string LastExportResult;
        public GameObject LastExportObject;

        [MenuItem("Tools/iffns stuff/Castle Importer")]
        public static void ShowWindow()
        {
            GetWindow(typeof(CastleImporter));
        }

        void OnGUI()
        {
            GUILayout.Label("Folder location");
            folderLocation = GUILayout.TextField(folderLocation);

            GUILayout.Label("File name");
            fileName = GUILayout.TextField(fileName);

            GUILayout.Label("Current library");
            AddList(nameof(currentLibraries));
            //currentLibrary = EditorGUILayout.ObjectField(obj: currentLibrary, objType: typeof(MaterialLibrary), true) as MaterialLibrary;

            if (GUILayout.Button("Import"))
            {
                ImportBasedOnIdentifiers();
            }
        }

        public void ImportBasedOnIdentifiers()
        {
            MaterialLibrary.ClearLibrary();

            foreach (MaterialLibrary library in currentLibraries)
            {
                library.Setup();
            }

            List<string> ObjLines = new List<string>(System.IO.File.ReadAllLines(Application.dataPath + @"\" + folderLocation + @"\" + fileName + ".obj"));

            Transform outputObject = new GameObject(fileName).transform;

            List<Vector3> v = new List<Vector3>();
            List<Vector2> vt = new List<Vector2>();
            List<int> f = new List<int>();

            GameObject currentObject = null;
            Mesh currentMesh = null;
            MeshRenderer currentRenderer;
            bool hasMeshCollider = false;
            int previoiusVertexCount = 0;

            for (int i = 0; i < ObjLines.Count; i++)
            {
                string currentLine = ObjLines[i];

                if (currentLine.StartsWith("o "))
                {
                    if (currentObject != null)
                    {
                        AssignCurrentValues();
                    }

                    currentLine = currentLine.Remove(0, 2);

                    currentObject = new GameObject(currentLine);

                    currentObject.transform.parent = outputObject;

                    string[] titleSeparator = { " - " };

                    string[] optionStrings = currentLine.Split(separator: titleSeparator, options: System.StringSplitOptions.RemoveEmptyEntries);

                    MeshFilter currentMeshFilter = currentObject.AddComponent(typeof(MeshFilter)) as MeshFilter;

                    currentMesh = new Mesh();

                    currentMesh.name = "imported obj mesh";

                    currentMeshFilter.sharedMesh = currentMesh;

                    foreach (string option in optionStrings)
                    {
                        string[] optionSeparator = { " = " };

                        string[] options = option.Split(separator: optionSeparator, options: System.StringSplitOptions.RemoveEmptyEntries);

                        if (options[0].Equals("Material"))
                        {
                            if (!options[1].Equals("Invisible"))
                            {
                                currentRenderer = currentObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

                                currentRenderer.material = MaterialLibrary.GetMaterialFromIdentifier(identifier: options[1]);
                            }
                        }
                        else if (options[0].Equals("Collider"))
                        {
                            hasMeshCollider = options[1].Equals("True");
                        }
                    }
                }
                else if (currentLine.StartsWith("v "))
                {
                    if (currentObject == null)
                    {
                        LastExportResult = "Error with file: Check console for warnings";
                        Debug.LogWarning("Error: should contain o Object name line before declaing vertices");
                        return;
                    }

                    currentLine = currentLine.Remove(0, 2);

                    string[] substrings = currentLine.Split(' ');

                    v.Add(new Vector3(
                        -float.Parse(substrings[0]),
                        float.Parse(substrings[1]),
                        float.Parse(substrings[2])
                        ));
                }
                else if (currentLine.StartsWith("vt "))
                {
                    if (currentObject == null)
                    {
                        LastExportResult = "Error with file: Check console for warnings";
                        Debug.LogWarning("Error: should contain o Object name line before declaing UVs");
                        return;
                    }

                    currentLine = currentLine.Remove(0, 3);

                    string[] substrings = currentLine.Split(' ');

                    vt.Add(new Vector3(
                        float.Parse(substrings[0]),
                        float.Parse(substrings[1])
                        ));
                }
                else if (currentLine.StartsWith("f "))
                {
                    if (currentObject == null)
                    {
                        LastExportResult = "Error with file: Check console for warnings";
                        Debug.LogWarning("Error: should contain o Object name line before declaing triangles");
                        return;
                    }

                    currentLine = currentLine.Remove(0, 2);

                    string[] substrings = currentLine.Split(' ');

                    string[] number0 = substrings[0].Split('/');
                    f.Add(int.Parse(number0[0]) - 1 - previoiusVertexCount);

                    string[] number2 = substrings[2].Split('/');
                    f.Add(int.Parse(number2[0]) - 1 - previoiusVertexCount);

                    string[] number1 = substrings[1].Split('/');
                    f.Add(int.Parse(number1[0]) - 1 - previoiusVertexCount);

                    /*
                    foreach (string sub in substrings)
                    {
                        string[] number = sub.Split('/');

                        f.Add(int.Parse(number[0]) - 1 - previoiusVertexCount);
                    }
                    */
                }
            }

            AssignCurrentValues();

            bool isValid()
            {
                if (v.Count == 0)
                {
                    Debug.LogWarning("Error: Mesh has no info on verticies");
                    return false;
                }

                if (f.Count == 0)
                {
                    Debug.LogWarning("Error: Mesh has no triangle information");
                    return false;
                }

                if (f.Max() > v.Count - 1)
                {
                    Debug.LogWarning("Error: triangles reference vertex that doesn't exist");
                    return false;
                }

                for (int i = 0; i < f.Count; i += 3)
                {
                    if (v[f[i]] == v[f[i + 1]]
                       || v[f[i]] == v[f[i + 2]]
                       || v[f[i + 1]] == v[f[i + 2]])
                    {
                        Debug.LogWarning("Error: triangle pair does not have unique vertecies");
                        return false;
                    }
                    if (float.IsInfinity(v[f[i]].x) || float.IsInfinity(v[f[i]].y) || float.IsInfinity(v[f[i]].z))
                    {
                        Debug.LogWarning("Error: triangle references infinity vertex");
                        return false;
                    }
                }

                return true;
            }

            void AssignCurrentValues()
            {
                if (!isValid())
                {
                    LastExportResult = "Error with file: Check console for warnings";
                    Debug.LogWarning("Error identified");
                    return;
                }

                if (currentMesh == null)
                {
                    LastExportResult = "Error with code: Check console for warnings";
                    Debug.LogWarning("Error: Current mesh is null");

                    return;
                }

                if (v.Count > 65535)
                {
                    //Avoid vertex limit
                    //https://answers.unity.com/questions/471639/mesh-with-more-than-65000-vertices.html
                    currentMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                currentMesh.vertices = v.ToArray();
                currentMesh.uv = vt.ToArray();
                currentMesh.triangles = f.ToArray();

                previoiusVertexCount += v.Count;



                v = new List<Vector3>();
                vt = new List<Vector2>();
                f = new List<int>();

                currentMesh.RecalculateNormals();
                currentMesh.RecalculateTangents();
                currentMesh.RecalculateBounds();

                if (hasMeshCollider) currentObject.AddComponent(typeof(MeshCollider));


                LastExportResult = "Hopefully worked well";
            }
        }

        void AddList(string propertyName)
        {
            SerializedObject tihsScriptSerialized = new SerializedObject(this);
            EditorGUILayout.PropertyField(tihsScriptSerialized.FindProperty(propertyName), true);
            tihsScriptSerialized.ApplyModifiedProperties();
        }

        List<string> SepparateString(string input, string separator)
        {
            List<string> returnList = new List<string>();

            if (!input.Contains(separator)) return null;

            int separatorLocation = input.IndexOf(separator);

            returnList.Add(input.Substring(0, separatorLocation));

            string rest = input.Substring(separatorLocation + separator.Length);

            if (rest.Contains(separator))
            {
                returnList.AddRange(SepparateString(input: rest, separator: separator));
            }
            else
            {
                returnList.Add(rest);
            }

            return returnList;
        }
    }
}
#endif
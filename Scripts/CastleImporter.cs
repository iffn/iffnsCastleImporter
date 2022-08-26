# if UNITY_EDITOR

using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Globalization;
using System.IO;

namespace iffnsStuff.iffnsUnityTools.CastleBuilderTools.CastleImporter
{
    public class CastleImporter : EditorWindow
    {
        //public List<MaterialLibrary> MaterialLibraries;

        [SerializeField] LibraryCollection LinkedCollection;

        public string LastExportResult;
        public GameObject LastExportObject;

        string lastFilePath = "";

        [MenuItem("Tools/iffns stuff/Castle Importer")]
        public static void ShowWindow()
        {
            GetWindow(typeof(CastleImporter));
        }

        void OnGUI()
        {
            GUILayout.Label("iffn's Castle Importer", EditorStyles.boldLabel);

            if (LinkedCollection == null)
            {
                GUILayout.Label("Libraries");
                LinkedCollection = EditorGUILayout.ObjectField(obj: LinkedCollection, objType: typeof(LibraryCollection), true) as LibraryCollection;

                EditorGUILayout.HelpBox("Setup: Assign CastleLibraryCollection found in Asssets/iffnsStuff/CastleLibraryCollection", MessageType.Info);
            }
            else
            {
                GUILayout.Label("Libraries");
                AddList(nameof(LinkedCollection));

                if (GUILayout.Button("Select file to import"))
                {
                    string filePath = SelectFile();

                    if (File.Exists(filePath))
                    {
                        ImportBasedOnIdentifiers(filePath: filePath);
                    }
                }
            }
        }

        string SelectFile()
        {
            string defaultFolder = Application.dataPath;

            if (lastFilePath.Length > 0 && Directory.Exists(lastFilePath))
            {
                defaultFolder = lastFilePath;
            }

            string filePath = EditorUtility.OpenFilePanel(title: "Select import file", directory: defaultFolder, extension: "obj");

            if (filePath.Length > 0)
            {
                lastFilePath = Path.GetDirectoryName(filePath);
            }

            return filePath;
        }

        public void ImportBasedOnIdentifiers(string filePath)
        {
            MaterialLibrary.ClearLibrary();

            foreach (MaterialLibrary library in LinkedCollection.MaterialLibraries)
            {
                library.Setup();
            }

            //List<string> ObjLines = new List<string>(System.IO.File.ReadAllLines(Application.dataPath + @"\" + folderLocation + @"\" + fileName + ".obj"));
            List<string> ObjLines = new List<string>(File.ReadAllLines(filePath));

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            Transform outputObject = new GameObject(fileName).transform;

            List<Vector3> v = new List<Vector3>();
            List<Vector2> vt = new List<Vector2>();
            List<int> f = new List<int>();

            GameObject currentObject = null;
            Mesh currentMesh = null;
            MeshRenderer currentRenderer;
            bool hasMeshCollider = false;
            int previoiusVertexCount = 0;

            Vector3 currentOffset = Vector3.zero;

            List<Transform> Holders = new List<Transform>();

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

                    string titleSeparator = " - ";

                    string[] optionStrings = currentLine.Split(separator: titleSeparator, options: System.StringSplitOptions.RemoveEmptyEntries);

                    MeshFilter currentMeshFilter = currentObject.AddComponent(typeof(MeshFilter)) as MeshFilter;

                    currentMesh = new Mesh();

                    currentMesh.name = "imported obj mesh";

                    currentMeshFilter.sharedMesh = currentMesh;

                    currentOffset = Vector3.zero;

                    foreach (string option in optionStrings)
                    {
                        string optionSeparator = " = ";

                        string[] options = option.Split(separator: optionSeparator, options: System.StringSplitOptions.RemoveEmptyEntries);

                        string identifier = "";
                        string content = "";

                        if (options.Length == 1)
                        {
                            content = options[0];
                        }
                        else if (options.Length == 2)
                        {
                            identifier = options[0];
                            content = options[1];
                        }

                        if (identifier.Equals("Material"))
                        {
                            if (!content.Equals("Invisible"))
                            {
                                currentRenderer = currentObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

                                currentRenderer.material = MaterialLibrary.GetMaterialFromIdentifier(identifier: options[1]);
                            }
                        }
                        else if (identifier.Equals("Collider"))
                        {
                            hasMeshCollider = content.Equals("True") || content.Equals("true");
                        }
                        else if (identifier.Equals("Local position"))
                        {
                            string withoutBracket = content.Substring(1, content.Length - 2);
                            string[] axis = withoutBracket.Split(separator: ", ".ToCharArray());

                            bool xCorrect = float.TryParse(axis[0], NumberStyles.Number, CultureInfo.InvariantCulture, out float x);
                            bool yCorrect = float.TryParse(axis[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float y);
                            bool zCorrect = float.TryParse(axis[2], NumberStyles.Number, CultureInfo.InvariantCulture, out float z);

                            if (xCorrect && yCorrect && zCorrect)
                            {
                                currentOffset = new Vector3(x, y, z);
                                currentObject.transform.localPosition = currentOffset;
                            }
                            else
                            {
                                Debug.LogWarning($"Error: Local position string {content} could not be converted into a Vector3");
                            }
                        }
                        else if (identifier.Equals("Name"))
                        {
                            currentObject.name = content;
                        }
                        else if (identifier.Equals("Hierarchy position"))
                        {
                            string[] hierarchyIndexStrings = content.Split(separator: "-".ToCharArray());

                            int[] hierarchyIndexes = new int[hierarchyIndexStrings.Length];

                            for (int j = 0; j < hierarchyIndexStrings.Length; j++)
                            {
                                hierarchyIndexes[j] = int.Parse(hierarchyIndexStrings[j], CultureInfo.InvariantCulture) - 1;
                            }

                            Transform parent = GetChildFromParent(currentHierarchyIndexes: hierarchyIndexes, currentParent: outputObject);

                            //ToDo: Position

                            parent.name = currentObject.name;
                            currentObject.transform.parent = parent;

                            Transform GetChildFromParent(int[] currentHierarchyIndexes, Transform currentParent)
                            {
                                Transform currentTransform = currentParent;

                                /*
                                if(currentHierarchyIndexes.Length > 1)
                                {
                                    int k = 0;
                                }
                                */

                                for (int k = 0; k < currentHierarchyIndexes.Length; k++)
                                {
                                    List<Transform> children = new List<Transform>();

                                    foreach (Transform child in currentTransform)
                                    {
                                        if (Holders.Contains(child))
                                        {
                                            children.Add(child);
                                        }
                                    }

                                    int currentIndex = currentHierarchyIndexes[k];

                                    int missingChildren = currentIndex + 1 - children.Count;

                                    if (missingChildren < 0)
                                    {
                                        missingChildren = 0;
                                    }

                                    else if (missingChildren > 0)
                                    {
                                        for (int j = 0; j < missingChildren; j++)
                                        {
                                            GameObject newObject = new GameObject();
                                            Transform newTransform = newObject.transform;

                                            newTransform.parent = currentTransform;
                                            newTransform.SetSiblingIndex(children.Count);
                                            children.Add(newTransform);
                                            Holders.Add(newTransform);
                                        }
                                    }

                                    currentTransform = children[currentHierarchyIndexes[k]];
                                }

                                return currentTransform;
                            }
                        }
                    }

                    //Cleanup
                    if (currentObject.transform.parent == null)
                    {
                        currentObject.transform.parent = outputObject;
                    }
                    else
                    {
                        currentObject.transform.parent.name = currentObject.name;
                    }

                    currentObject.name = currentLine;
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

                    bool xCorrect = float.TryParse(substrings[0], NumberStyles.Number, CultureInfo.InvariantCulture, out float x);
                    bool yCorrect = float.TryParse(substrings[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float y);
                    bool zCorrect = float.TryParse(substrings[2], NumberStyles.Number, CultureInfo.InvariantCulture, out float z);

                    if (xCorrect && yCorrect && zCorrect)
                    {
                        v.Add(new Vector3(-x, y, z) - currentOffset);
                    }
                    else
                    {
                        Debug.LogWarning($"Error: {currentLine} could not be converted into a Vector3");
                        return;
                    }


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

                    bool xCorrect = float.TryParse(substrings[0], NumberStyles.Number, CultureInfo.InvariantCulture, out float x);
                    bool yCorrect = float.TryParse(substrings[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float y);

                    if (xCorrect && yCorrect)
                    {
                        vt.Add(new Vector2(x, y));
                    }
                    else
                    {
                        Debug.LogWarning($"Error: {currentLine} could not be converted into a Vector2");
                        return;
                    }
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

                    string[] index0 = substrings[0].Split('/');
                    string[] index1 = substrings[1].Split('/');
                    string[] index2 = substrings[2].Split('/');

                    bool index0Correct = int.TryParse(index0[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i0);
                    bool index1Correct = int.TryParse(index1[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i1);
                    bool index2Correct = int.TryParse(index2[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i2);

                    //triangle index order needs to be 0, 2, 1 for the correct normal orientation
                    f.Add(i0 - 1 - previoiusVertexCount);
                    f.Add(i2 - 1 - previoiusVertexCount);
                    f.Add(i1 - 1 - previoiusVertexCount);

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

                currentMesh.RecalculateNormals();
                currentMesh.RecalculateTangents();
                currentMesh.RecalculateBounds();

                if (hasMeshCollider) currentObject.AddComponent(typeof(MeshCollider));

                //Reset values for next object
                v = new List<Vector3>();
                vt = new List<Vector2>();
                f = new List<int>();

                LastExportResult = "Hopefully worked well";
            }
        }

        void AddList(string propertyName)
        {
            SerializedObject tihsScriptSerialized = new SerializedObject(this);
            EditorGUILayout.PropertyField(tihsScriptSerialized.FindProperty(propertyName), true);
            tihsScriptSerialized.ApplyModifiedProperties();
        }
    }
}
#endif
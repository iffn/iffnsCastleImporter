# if UNITY_EDITOR

using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Globalization;
using System.IO;
using System;
using System.Xml.Linq;

namespace iffnsStuff.iffnsUnityTools.CastleBuilderTools.CastleImporter
{
    public class CastleImporter : EditorWindow
    {
        //Editor values
        [SerializeField] LibraryCollection LinkedCollection;
        bool setAsStatic = true;
        bool generateLightmap = false;
        float hardAngle = 88;
        float packMargin = 4;
        float angleError = 8;
        float areaError = 15;

        //Runtime values
        public string LastExportResult;
        public GameObject LastExportObject;
        UnwrapParam currentLightmapSettings;

        string lastFilePath = "";

        [MenuItem("Tools/iffnsStuff/Castle Importer")]
        public static void ShowWindow()
        {
            GetWindow(t: typeof(CastleImporter), utility: false, title: "Castle Importer");
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

                setAsStatic = GUILayout.Toggle(setAsStatic, "Set as static");
                generateLightmap = GUILayout.Toggle(generateLightmap, "Generate lightmap UVs");

                if (generateLightmap)
                {
                    hardAngle = EditorGUILayout.Slider("  Hard angle (default = 88)", hardAngle, 0, 180);
                    packMargin = EditorGUILayout.Slider("  Pack margin (default = 4)", packMargin, 0, 64);
                    angleError = EditorGUILayout.Slider("  Angle error (default = 8)", angleError, 1, 75);
                    areaError = EditorGUILayout.Slider("  Area error (default = 15)", areaError, 1, 75);
                }

                EditorGUILayout.HelpBox($"Note:{System.Environment.NewLine}You can use the Lightmap UV Generator tool to set them later.", MessageType.Info);

                if (GUILayout.Button("Select file to import"))
                {
                    string filePath = SelectFile();

                    if (generateLightmap)
                    {
                        currentLightmapSettings = new UnwrapParam
                        {
                            hardAngle = hardAngle,
                            packMargin = packMargin,
                            angleError = angleError,
                            areaError = areaError
                        };
                    }

                    if (File.Exists(filePath))
                    {
                        ImportBasedOnIdentifiers(filePath: filePath);
                    }
                }
            }
        }

        void AddList(string propertyName)
        {
            SerializedObject tihsScriptSerialized = new SerializedObject(this);
            EditorGUILayout.PropertyField(tihsScriptSerialized.FindProperty(propertyName), true);
            tihsScriptSerialized.ApplyModifiedProperties();
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

        void ImportBasedOnIdentifiers(string filePath)
        {
            ResetLibraries();

            List<string> ObjLines = GetLinesFromPath(filePath);

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            List<ImportMeshInfo> meshInfo = GenerateMeshInfoFromObjFile(ObjLines);

            GenerateObjectFromInfo(fileName, meshInfo);

            return;
        }

        void ResetLibraries()
        {
            MaterialLibrary.ClearLibrary();

            foreach (MaterialLibrary library in LinkedCollection.MaterialLibraries)
            {
                library.Setup();
            }
        }

        List<string> GetLinesFromPath(string filePath)
        {
            return new List<string>(File.ReadAllLines(filePath));
        }

        List<ImportMeshInfo> GenerateMeshInfoFromObjFile(List<string> objLines)
        {
            //Obj files share a common triangle index count while the one in Unity is separate
            int currentOffset = 0;

            List<ImportMeshInfo> returnList = new List<ImportMeshInfo>();

            ImportMeshInfo currentMeshInfo = new ImportMeshInfo("");
            returnList.Add(currentMeshInfo);


            foreach (string currentLine in objLines)
            {
                if (currentLine.StartsWith("o "))
                {
                    currentOffset += currentMeshInfo.verticies.Count;

                    string identifier = currentLine.Remove(0, 2);

                    currentMeshInfo = new ImportMeshInfo(identifier);
                    returnList.Add(currentMeshInfo);

                }
                else if (currentLine.StartsWith("v "))
                {
                    string info = currentLine.Remove(0, 2);
                    currentMeshInfo.AddVertexInfo(info, ImportMeshInfo.OriginType.ObjFile);
                }
                else if (currentLine.StartsWith("vt "))
                {
                    string info = currentLine.Remove(0, 3);
                    currentMeshInfo.AddUV0Info(info, ImportMeshInfo.OriginType.ObjFile);
                }
                else if (currentLine.StartsWith("f "))
                {
                    string info = currentLine.Remove(0, 2);
                    currentMeshInfo.AddTriangleInfo(info, ImportMeshInfo.OriginType.ObjFile, currentOffset);
                }
            }

            for (int i = 0; i < returnList.Count; i++)
            {
                if (returnList[i].IsEmpty)
                {
                    returnList.RemoveAt(i);
                    i--;
                }
            } 

            return returnList;
        }

        void GenerateObjectFromInfo(string fileName, List<ImportMeshInfo> meshInfo)
        {
            Transform outputObject = new GameObject(fileName).transform;

            //Create hierarchy:
            foreach(ImportMeshInfo info in meshInfo)
            {
                if (!info.IsValid) continue;

                Transform currentTransform = GetTransformInHierarchy(mainParent: outputObject, HierarchyIndexes: info.hierarchyIndexes);

                info.linkedTransform = currentTransform;

                currentTransform.name = info.name;
                currentTransform.localPosition = info.localPosition;
            }
            
            //Assign meshes
            foreach (ImportMeshInfo info in meshInfo)
            {
                Transform newTransform = new GameObject().transform;

                if(info.linkedTransform != null)
                {
                    newTransform.parent = info.linkedTransform;
                }
                else
                {
                    newTransform.parent = outputObject;
                }
                
                newTransform.localPosition = Vector3.zero;
                newTransform.name = info.completeIdentifier;

                Vector3 offset = newTransform.InverseTransformPoint(outputObject.position)  ;

                GenerateMeshFromInfo(currentTransform: newTransform, info: info, offset: offset);
            }

            SetObjectAndAllChildrenToStatic(outputObject.gameObject);
        }

        Transform GetTransformInHierarchy(Transform mainParent, List<int> HierarchyIndexes)
        {
            Transform currentParent = mainParent;

            foreach (int index in HierarchyIndexes)
            {
                int missingChildren = index - currentParent.childCount;

                for (int i = 0; i < missingChildren; i++)
                {
                    Transform newChild = new GameObject().transform;
                    newChild.parent = currentParent;
                    newChild.localPosition = Vector3.zero;
                    newChild.localRotation = Quaternion.identity;
                }

                currentParent = currentParent.GetChild(index - 1);
            }

            return currentParent;
        }

        void GenerateMeshFromInfo(Transform currentTransform, ImportMeshInfo info, Vector3 offset)
        {
            GameObject currentGameObject = currentTransform.gameObject;

            //Mesh
            MeshFilter meshFilter = currentGameObject.AddComponent(typeof(MeshFilter)) as MeshFilter;

            Mesh mesh = new Mesh(); //Note: Only for editor use. For ingame, use a mesh pool to avoid a memory leak.
            mesh.name = "Imported castle mesh";

            if (info.verticies.Count > 65535)
            {
                //Avoid vertex limit
                //https://answers.unity.com/questions/471639/mesh-with-more-than-65000-vertices.html
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            Vector3[] vertexArray;

            if (offset.magnitude > 0)
            {
                vertexArray = new Vector3[info.verticies.Count];

                //Vector3 offset = -info.localPosition;

                for (int i = 0; i < info.verticies.Count; i++)
                {
                    vertexArray[i] = info.verticies[i] + offset;
                }
            }
            else
            {
                vertexArray = info.verticies.ToArray();
            }

            mesh.vertices = vertexArray;
            mesh.triangles = info.triangles.ToArray();
            mesh.uv = info.uv0.ToArray();
            
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            if (generateLightmap)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh, currentLightmapSettings);
            }

            meshFilter.sharedMesh = mesh;

            //Material
            if (info.MaterialIdentifier.Length != 0 && !info.MaterialIdentifier.Equals("Invisible"))
            {
                MeshRenderer currentRenderer = currentGameObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

                currentRenderer.material = MaterialLibrary.GetMaterialFromIdentifier(identifier: info.MaterialIdentifier);
            }

            //Collider
            if(info.collider) currentGameObject.AddComponent(typeof(MeshCollider));
        }

        void SetObjectAndAllChildrenToStatic(GameObject mainObject)
        {
            mainObject.isStatic = true;

            foreach (Transform child in mainObject.GetComponentsInChildren<Transform>())
            {
                child.gameObject.isStatic = true;
            }
        }
    }
}
#endif
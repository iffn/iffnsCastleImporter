# if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace iffnsStuff.iffnsUnityTools.CastleBuilderTools.CastleImporter
{
    public class LightmapUVGEnerator : EditorWindow
    {
        //Editor values
        [SerializeField] GameObject LinkedObject;
        bool alsoRunForChildren = true;
        float hardAngle = 88;
        float packMargin = 4;
        float angleError = 8;
        float areaError = 15;

        //Runtime values
        public static int MeshesCalculated { get; private set; } = -1;

        [MenuItem("Tools/iffnsStuff/CustomImporters/Lightmap UV generator")]
        public static void ShowWindow()
        {
            GetWindow(t: typeof(LightmapUVGEnerator), utility: false, title: "Lightmap UV generator");
        }

        void OnGUI()
        {
            GUILayout.Label("iffn's Lightmap UV generator", EditorStyles.boldLabel);

            if (LinkedObject == null)
            {
                GUILayout.Label("Object to generate stuff");
                LinkedObject = EditorGUILayout.ObjectField(obj: LinkedObject, objType: typeof(GameObject), true) as GameObject;
            }
            else
            {
                AddList(nameof(LinkedObject));
            }

            alsoRunForChildren = GUILayout.Toggle(alsoRunForChildren, "Also run for children");

            GUILayout.Label("Lightmap UV settings", EditorStyles.boldLabel);
            hardAngle = EditorGUILayout.Slider("Hard angle (default = 88)", hardAngle, 0, 180);
            packMargin = EditorGUILayout.Slider("Pack margin (default = 4)", packMargin, 0, 64);
            angleError = EditorGUILayout.Slider("Angle error (default = 8)", angleError, 1, 75);
            areaError = EditorGUILayout.Slider("Area error (default = 15)", areaError, 1, 75);

            if (LinkedObject != null)
            {
                if (GUILayout.Button("Generate"))
                {
                    GenerateBasedOnSettings();
                }
            }

            if(MeshesCalculated >= 0)
            {
                GUILayout.Label($"Calculated meshes = {MeshesCalculated}");
            }
        }

        void AddList(string propertyName)
        {
            SerializedObject tihsScriptSerialized = new SerializedObject(this);
            EditorGUILayout.PropertyField(tihsScriptSerialized.FindProperty(propertyName), true);
            tihsScriptSerialized.ApplyModifiedProperties();
        }

        void GenerateBasedOnSettings()
        {
            UnwrapParam lightmapSettings = new UnwrapParam();



            /*
            {
                hardAngle = hardAngle,
                packMargin = packMargin,
                angleError = angleError,
                areaError = areaError
            };
            */

            if (alsoRunForChildren)
            {
                GenrerateLightmapUVMapsForAllObjectBelow(element: LinkedObject.transform, settings: lightmapSettings);
            }
            else
            {
                GenrerateLightmapUVMapsForSingleObject(element: LinkedObject.transform, settings: lightmapSettings);
            }
        }
        
        static void GenrerateLightmapUVMapsForObject(Transform element, UnwrapParam settings)
        {
            MeshFilter meshFilter = element.GetComponent<MeshFilter>();

            if (meshFilter == null) return;

            Mesh mesh = meshFilter.sharedMesh;

            if (mesh == null) return;

            if (!mesh.isReadable) return;

            Unwrapping.GenerateSecondaryUVSet(mesh, settings);

            MeshesCalculated++;
        }

        static void GenrerateLightmapUVMapsForSingleObject(Transform element, UnwrapParam settings)
        {
            MeshesCalculated = 0;

            GenrerateLightmapUVMapsForObject(element: element, settings: settings);
        }

        public static void GenrerateLightmapUVMapsForAllObjectBelow(Transform element, UnwrapParam settings)
        {
            MeshesCalculated = 0;

            //Generate for element
            GenrerateLightmapUVMapsForObject(element: element, settings: settings);

            //Generate for every child of child of child... of element
            foreach (Transform child in element.GetComponentsInChildren<Transform>())
            {
                GenrerateLightmapUVMapsForObject(element: child, settings: settings);
            }
        }
    }
}
#endif
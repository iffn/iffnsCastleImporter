using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iffnsStuff.iffnsUnityTools.CastleBuilderTools.CastleImporter
{
    public class MaterialLibrary : MonoBehaviour
    {

        public List<Material> MaterialAssignments;

        public static Dictionary<string, Material> MaterialLibary = new Dictionary<string, Material>();

        public static void ClearLibrary()
        {
            MaterialLibary = new Dictionary<string, Material>();
        }

        public void Setup()
        {
            foreach (Material material in MaterialAssignments)
            {
                if (MaterialLibary.ContainsKey(material.name)) continue;

                MaterialLibary.Add(key: material.name, value: material);
            }
        }

        public static Material GetMaterialFromIdentifier(string identifier)
        {
            string searchString = identifier.Replace("\"", "");

            if (MaterialLibary == null)
            {
                Debug.Log("Error: Library is not set up for some reason");
                return null;
            }

            if (MaterialLibary.ContainsKey(searchString) == false)
            {
                Debug.LogWarning("Error: Library does not contain identifier: " + identifier);
                return null;
            }

            return MaterialLibary[searchString];
        }
    }
}
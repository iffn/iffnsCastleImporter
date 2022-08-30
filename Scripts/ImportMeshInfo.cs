using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.WSA;
using static ImportMeshInfo;
using static UnityEditor.Timeline.TimelinePlaybackControls;

public class ImportMeshInfo
{
    public string completeIdentifier;
    public string name;
    public string MaterialIdentifier;
    public bool collider;
    public Vector3 localPosition;
    public List<int> hierarchyIndexes;

    public List<Vector3> verticies;
    public List<int> triangles;
    public List<Vector2> uv0;
    
    public Transform linkedTransform;

    public bool IsEmpty
    {
        get
        {
            if(completeIdentifier.Length != 0) return false;
            if(verticies.Count != 0) return false;
            if(triangles.Count != 0) return false;
            if(uv0.Count != 0) return false;

            return true;
        }
    }

    public bool IsValid
    {
        get
        {
            if (IsEmpty) return false;

            if (this.triangles.Count == 0 || this.verticies.Count == 0)
            {
                Debug.LogWarning("Error with mesh: Mesh has no verticies or triangles");
                return false;
            }

            //Check if all triangles accessible
            if (this.triangles.Max() > this.verticies.Count - 1)
            {
                Debug.LogWarning("Error with mesh: Triangles reference vertex that don't exist");
                return false;
            }

            //Check unique vertices
            List<int> errorLines = new List<int>();

            for (int i = 0; i < triangles.Count; i += 3)
            {
                if (verticies[triangles[i]] == verticies[triangles[i + 1]]
                   || verticies[triangles[i]] == verticies[triangles[i + 2]]
                   || verticies[triangles[i + 1]] == verticies[triangles[i + 2]])
                {
                    Debug.LogWarning("Error with mesh: the same vertex is referenced multiple times in same triangle");
                    return false;
                }
                if (float.IsInfinity(verticies[triangles[i]].x) || float.IsInfinity(verticies[triangles[i]].y) || float.IsInfinity(verticies[triangles[i]].z))
                {
                    Debug.LogWarning("Error with mesh: the same vertex is referenced multiple times in same triangle");
                    return false;
                }
            }

            foreach(Vector3 vertex in verticies)
            {
                if (float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z))
                {
                    Debug.LogWarning("Error with mesh: Vertex has a position of infinity");
                    return false;
                }
            }

            return true;
        }
    }

    public void SetDefaultParameters()
    {
        completeIdentifier = "";
        name = "Unnamed mesh";
        MaterialIdentifier = "";
        collider = true;
        localPosition = Vector3.zero;
        hierarchyIndexes = new List<int>();

        verticies = new List<Vector3>();
        triangles = new List<int>();
        uv0 = new List<Vector2>();
    }

    public ImportMeshInfo(List<ImportMeshInfo> meshInfos, Vector3 localPosition, List<int> hierarchyIndexes)
    {
        SetDefaultParameters();

        //Main parameters
        name = meshInfos[0].name;
        MaterialIdentifier = meshInfos[0].MaterialIdentifier;
        collider = meshInfos[0].collider;
        this.localPosition = localPosition;
        this.hierarchyIndexes = hierarchyIndexes;

        int currentTriangleIndex = 0;

        //Mesh parameters
        foreach (ImportMeshInfo currentInfo in meshInfos)
        {
            if (!currentInfo.IsValid) continue;

            verticies.AddRange(currentInfo.verticies);

            foreach(int triangle in currentInfo.triangles)
            {
                triangles.Add(triangle + currentTriangleIndex);
            }

            triangles.AddRange(currentInfo.triangles);
            uv0.AddRange(currentInfo.uv0);

            int missingUVs = currentInfo.verticies.Count - currentInfo.uv0.Count;

            for(int i = 0; i<missingUVs; i++)
            {
                uv0.Add(Vector2.zero);
            }

            currentTriangleIndex = verticies.Count;
        }
    }

    public ImportMeshInfo(string InfoLine)
    {
        SetDefaultParameters();

        completeIdentifier = InfoLine;

        List<string> optionStrings = InfoLine.Split(" - ").ToList();

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

            if (identifier.Equals("Name"))
            {
                name = content;
            }
            else if (identifier.Equals("Material"))
            {
                MaterialIdentifier = content;
            }
            else if (identifier.Equals("Collider"))
            {
                collider = content.Equals("True") || content.Equals("true");
            }
            else if (identifier.Equals("Local position"))
            {
                localPosition = GetVector3FromString(content, OriginType.Unity);
            }
            else if (identifier.Equals("Hierarchy position"))
            {
                string[] hierarchyIndexStrings = content.Split(separator: "-".ToCharArray());

                for (int j = 0; j < hierarchyIndexStrings.Length; j++)
                {
                    bool posCorrect = int.TryParse(hierarchyIndexStrings[j], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pos);

                    if (posCorrect)
                    {
                        hierarchyIndexes.Add(pos);
                    }
                    else
                    {
                        Debug.LogWarning($"Error: Hierarchy index {hierarchyIndexStrings[j]} could not be converted into an int");
                        hierarchyIndexes.Add(-1);
                    }
                }
            }
        }
    }

    public void AddVertexInfo(string info, OriginType originType)
    {
        verticies.Add(GetVector3FromString(info, originType));
    }

    public void AddUV0Info(string info, OriginType originType)
    {
        uv0.Add(GetVector2FromString(info, originType));
    }

    public void AddTriangleInfo(string info, OriginType originType, int offset)
    {
        Vector3Int triangleInfos = GetVector3IntFromString(info, originType, offset);

        triangles.Add(triangleInfos.x);
        triangles.Add(triangleInfos.z);
        triangles.Add(triangleInfos.y);
    }

    public enum OriginType
    {
        Unity,
        ObjFile
    }

    string[] GetElementsAccordingToConversionType(string parseString, OriginType originType)
    {
        string[] returnValue;

        switch (originType)
        {
            case OriginType.Unity:
                parseString = parseString.Replace("(", "");
                parseString = parseString.Replace(")", "");
                parseString = parseString.Replace(" ", "");
                returnValue = parseString.Split(separator: ',');
                break;
            case OriginType.ObjFile:
                returnValue = parseString.Split(separator: ' ');
                break;
            default:
                returnValue = new string[0];
                break;
        }

        return returnValue;
    }

    Vector3 GetVector3FromString(string parseString, OriginType originType)
    {
        string originalString = parseString;

        string[] axis = GetElementsAccordingToConversionType(parseString, originType);

        if(axis.Length == 0)
        {
            Debug.LogWarning($"Error: Convertsion type {originType} not defined for Vector3");
            return Vector3.zero;
        }

        bool xCorrect = float.TryParse(axis[0], NumberStyles.Number, CultureInfo.InvariantCulture, out float x);
        bool yCorrect = float.TryParse(axis[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float y);
        bool zCorrect = float.TryParse(axis[2], NumberStyles.Number, CultureInfo.InvariantCulture, out float z);

        if (!xCorrect || !yCorrect || !zCorrect)
        {
            Debug.LogWarning($"Error: Local position string {originalString} could not be converted into a Vector3");
        }

        switch (originType)
        {
            case OriginType.Unity:
                return new Vector3(x, y, z);
            case OriginType.ObjFile:
                return new Vector3(-x, y, z);
            default:
                return new Vector3(x, y, z);
        }
    }

    Vector3Int GetVector3IntFromString(string parseString, OriginType originType, int offset)
    {
        string originalString = parseString;

        string[] axis;
        
        switch (originType)
        {
            case OriginType.Unity:
                axis = GetElementsAccordingToConversionType(parseString, originType);
                break;
            case OriginType.ObjFile:
                //Format is: "a/a b/b c/c"
                axis = parseString.Split(' ');
                for(int i = 0; i<axis.Length; i++)
                {
                    axis[i] = axis[i].Split('/')[0];
                }
                break;
            default:
                Debug.LogWarning($"Error: Convertsion type {originType} not defined for Vector3");
                return Vector3Int.zero;
        }

        if (axis.Length == 0)
        {
            Debug.LogWarning($"Error: Convertsion type {originType} not defined for Vector3Int");
            return Vector3Int.zero;
        }

        bool xCorrect = int.TryParse(axis[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x);
        bool yCorrect = int.TryParse(axis[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y);
        bool zCorrect = int.TryParse(axis[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z);

        if (!xCorrect || !yCorrect || !zCorrect)
        {
            Debug.LogWarning($"Error: Local position string {originalString} could not be converted into a Vector3Int");
        }

        int totalOffset = 0;

        switch (originType)
        {
            case OriginType.Unity:
                totalOffset = offset;
                break;
            case OriginType.ObjFile:
                totalOffset = offset + 1;
                break;
            default:
                break;
        }

        return new Vector3Int(x - totalOffset, y - totalOffset, z - totalOffset);
    }

    Vector2 GetVector2FromString(string parseString, OriginType originType)
    {
        string originalString = parseString;

        string[] axis = GetElementsAccordingToConversionType(parseString, originType);

        if (axis.Length == 0)
        {
            Debug.LogWarning($"Error: Convertsion type {originType} not defined for Vector2");
            return Vector2.zero;
        }

        bool xCorrect = float.TryParse(axis[0], NumberStyles.Number, CultureInfo.InvariantCulture, out float x);
        bool yCorrect = float.TryParse(axis[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float y);

        if (!xCorrect || !yCorrect)
        {
            Debug.LogWarning($"Error: Local position string {originalString} could not be converted into a Vector2");
        }

        return new Vector2(x, y);
    }
}

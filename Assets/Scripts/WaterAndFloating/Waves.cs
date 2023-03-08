using System;
using System.Collections.Generic;
using System.Linq;
using Singleton;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Waves : MonoBehaviour
{
    //Properties
    [SerializeField] private int _dimension = 10;
    [SerializeField] private float _UVScale = 2f;
    [SerializeField] private Transform _waterPosition;
    [SerializeField] private Octave[] _octaves;

    //Mesh
    protected MeshFilter MeshFilter;
    protected Mesh Mesh;
    private List<Vector3> _vertices = new List<Vector3>();

    private void Start()
    {
        //mesh generation
        MeshFilter = GetComponent<MeshFilter>();
        Mesh = MeshFilter.mesh;
        Mesh.name = gameObject.name;
        Mesh.vertices = GenerateVertices();
        Mesh.triangles = GenerateTriangles();
        Mesh.uv = GenerateUVs();
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();

        //position & scale
        transform.position = _waterPosition.position;
        transform.localScale = _waterPosition.localScale;

        Mesh.GetVertices(_vertices);
        WaveGeneration();

        SetupVerticesAmplitudeDictionary();
    }

    private void Update()
    {
        WaveGeneration();
        ManageCircularWavesTimer();
    }

    public float GetHeight(Vector3 position)
    {
        //scale factor and position in local space
        Vector3 scale = new Vector3(1 / transform.lossyScale.x, 0, 1 / transform.lossyScale.z);
        Vector3 localPos = Vector3.Scale((position - transform.position), scale);

        //get edge points
        Vector3 p1 = new Vector3(Mathf.Floor(localPos.x), 0, Mathf.Floor(localPos.z));
        Vector3 p2 = new Vector3(Mathf.Floor(localPos.x), 0, Mathf.Ceil(localPos.z));
        Vector3 p3 = new Vector3(Mathf.Ceil(localPos.x), 0, Mathf.Floor(localPos.z));
        Vector3 p4 = new Vector3(Mathf.Ceil(localPos.x), 0, Mathf.Ceil(localPos.z));

        //clamp if the position is outside the plane
        p1.x = Mathf.Clamp(p1.x, 0, _dimension);
        p1.z = Mathf.Clamp(p1.z, 0, _dimension);
        p2.x = Mathf.Clamp(p2.x, 0, _dimension);
        p2.z = Mathf.Clamp(p2.z, 0, _dimension);
        p3.x = Mathf.Clamp(p3.x, 0, _dimension);
        p3.z = Mathf.Clamp(p3.z, 0, _dimension);
        p4.x = Mathf.Clamp(p4.x, 0, _dimension);
        p4.z = Mathf.Clamp(p4.z, 0, _dimension);

        //get the max distance to one of the edges and take that to compute max - dist
        float max = Mathf.Max(Vector3.Distance(p1, localPos), Vector3.Distance(p2, localPos),
            Vector3.Distance(p3, localPos), Vector3.Distance(p4, localPos) + Mathf.Epsilon);
        float dist = (max - Vector3.Distance(p1, localPos))
                   + (max - Vector3.Distance(p2, localPos))
                   + (max - Vector3.Distance(p3, localPos))
                   + (max - Vector3.Distance(p4, localPos) + Mathf.Epsilon);
        //weighted sum


        float height = _vertices[Index(p1.x, p1.z)].y * (max - Vector3.Distance(p1, localPos))
                       + _vertices[Index(p2.x, p2.z)].y * (max - Vector3.Distance(p2, localPos))
                       + _vertices[Index(p3.x, p3.z)].y * (max - Vector3.Distance(p3, localPos))
                       + _vertices[Index(p4.x, p4.z)].y * (max - Vector3.Distance(p4, localPos));

        //scale
        return height * transform.lossyScale.y / dist;
    }

    #region Mesh Generation

    private Vector3[] GenerateVertices()
    {
        Vector3[] verts = new Vector3[(_dimension + 1) * (_dimension + 1)];

        //equaly distributed verts
        for (int x = 0; x <= _dimension; x++)
        for (int z = 0; z <= _dimension; z++)
            verts[Index(x, z)] = new Vector3(x, 0, z);

        return verts;
    }

    private int[] GenerateTriangles()
    {
        int[] tries = new int[Mesh.vertices.Length * 6];

        //two triangles are one tile
        for (int x = 0; x < _dimension; x++)
        {
            for (int z = 0; z < _dimension; z++)
            {
                tries[Index(x, z) * 6 + 0] = Index(x, z);
                tries[Index(x, z) * 6 + 1] = Index(x + 1, z + 1);
                tries[Index(x, z) * 6 + 2] = Index(x + 1, z);
                tries[Index(x, z) * 6 + 3] = Index(x, z);
                tries[Index(x, z) * 6 + 4] = Index(x, z + 1);
                tries[Index(x, z) * 6 + 5] = Index(x + 1, z + 1);
            }
        }

        return tries;
    }

    private Vector2[] GenerateUVs()
    {
        Vector2[] uvs = new Vector2[Mesh.vertices.Length];

        //always set one uv over n tiles than flip the uv and set it again
        for (int x = 0; x <= _dimension; x++)
        {
            for (int z = 0; z <= _dimension; z++)
            {
                Vector2 vec = new Vector2((x / _UVScale) % 2, (z / _UVScale) % 2);
                uvs[Index(x, z)] = new Vector2(vec.x <= 1 ? vec.x : 2 - vec.x, vec.y <= 1 ? vec.y : 2 - vec.y);
            }
        }

        return uvs;
    }

    #endregion

    #region Index

    private int Index(int x, int z)
    {
        return x * (_dimension + 1) + z;
    }

    private int Index(float x, float z)
    {
        return Index((int)x, (int)z);
    }

    #endregion

    private void WaveGeneration()
    {
        float time = Time.time;
        Octave octave = _octaves[0];
        float octaveScaleX = octave.Scale.x;
        float octaveScaleY = octave.Scale.y;
        float octaveSpeedX = octave.Speed.x * time;
        float octaveSpeedY = octave.Speed.y * time;
        float octaveHeight = octave.Height;

        for (int x = 0; x <= _dimension; x++)
        {
            for (int z = 0; z <= _dimension; z++)
            {
                float y = 0f;
     
                float perlinNoiseValue = Mathf.PerlinNoise(
                    (x * octaveScaleX +  octaveSpeedX) / _dimension, 
                    (z * octaveScaleY +  octaveSpeedY) / _dimension) 
                                         - 0.5f;
                
                y += perlinNoiseValue * octaveHeight;

                _vertices[ x * (_dimension + 1) + z] = new Vector3(x, y, z);
            }
        }
        
        ManageCircularWaves();
        ManageVerticesAmplitude();

        Mesh.SetVertices(_vertices);
        Mesh.RecalculateNormals();
    }

    #region CircularWaves

    private List<CircularWave> _circularWavesList = new List<CircularWave>();
    private List<float> _circularWavesDurationList = new List<float>();

    public void LaunchCircularWave(CircularWave circularWave)
    {
        _circularWavesList.Add(circularWave);
        _circularWavesDurationList.Add(circularWave.Duration);
    }
    
    private void ManageCircularWaves()
    {
        for (int i = 0; i < _circularWavesList.Count; i++)
        {
            //calculate the values
            CircularWave waveData = _circularWavesList[i];
            Vector3 center = new Vector3(waveData.Center.x, 0, waveData.Center.y);
            float currentTime = _circularWavesDurationList[i];
            float percent = currentTime / waveData.Duration;
            float distance = (1 - percent) * waveData.Distance;
            float amplitude = percent * waveData.Amplitude;

            //set vertex
            float angleDifference = 360 / waveData.NumberOfPoints;
            for (int j = 1; j <= waveData.NumberOfPoints; j++)
            {
                float angle = j * angleDifference;
                Vector3 point = GetPointFromAngleAndDistance(center, angle, distance);
                int index = FindIndexOfClosestVerticeTo(new Vector2(point.x,point.z));
                
                _verticesAmplitudeDictionary[new Vector2(_vertices[index].x, _vertices[index].z)] = amplitude;
            }
        }
    }

    private void ManageCircularWavesTimer()
    {
        for (int i = 0; i < _circularWavesList.Count; i++)
        {
            _circularWavesDurationList[i] -= Time.deltaTime;
            
            if (_circularWavesDurationList[i] <= 0)
            {
                _circularWavesList.Remove(_circularWavesList[i]);
                _circularWavesDurationList.Remove(_circularWavesDurationList[i]);
            }
        }
    }

    private int FindIndexOfClosestVerticeTo(Vector2 position)
    {
        Vector2 positionDifference = new Vector2(transform.position.x, transform.position.z);
        Vector2 scaleDifference = new Vector2(transform.localScale.x, transform.localScale.z);
        
        float closestDistance = Vector2.Distance(position, 
            new Vector2((_vertices[0].x*scaleDifference.x)+positionDifference.x, (_vertices[0].z*scaleDifference.y)+positionDifference.y));
        int closestIndex = 0;

        for (int i = 0; i < _vertices.Count; i++)
        {
            Vector2 vertice = new Vector2((_vertices[i].x*scaleDifference.x)+positionDifference.x, 
                                          (_vertices[i].z*scaleDifference.y)+positionDifference.y);
            
            float distance = Vector2.Distance(position, vertice);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
    
    public Vector3 GetPointFromAngleAndDistance(Vector3 startingPoint, float yAngleDegrees, float distance)
    {
        // Convert the Y angle to radians
        float yAngleRadians = yAngleDegrees * Mathf.Deg2Rad;

        // Calculate the X and Z offsets using trigonometry
        float xOffset = distance * Mathf.Sin(yAngleRadians);
        float zOffset = distance * Mathf.Cos(yAngleRadians);

        // Create a new Vector3 with the calculated offsets and the same Y position as the starting point
        Vector3 newPoint = new Vector3(startingPoint.x + xOffset, startingPoint.y, startingPoint.z + zOffset);

        return newPoint;
    }

    #endregion

    #region Amplitude
    
    private Dictionary<Vector2, float> _verticesAmplitudeDictionary = new Dictionary<Vector2, float>();

    private void SetupVerticesAmplitudeDictionary()
    {
        foreach (Vector3 vertice in _vertices)
        {
            _verticesAmplitudeDictionary.Add(new Vector2(vertice.x,vertice.z),0);
        }
    }

    private void ManageVerticesAmplitude()
    {
        List<KeyValuePair<Vector2, float>> list = _verticesAmplitudeDictionary.ToList();
        for(int i = 0; i < list.Count; i++)
        {
            if (list[i].Value <= 0)
            {
                continue;
            }
            _vertices[i] = new Vector3(_vertices[i].x, list[i].Value, _vertices[i].z);
            const float waveReducingAmplitudeMultiplier = 3;
            float newValue = list[i].Value - Time.deltaTime * waveReducingAmplitudeMultiplier;
            list[i] = new KeyValuePair<Vector2, float>(new Vector2(list[i].Key.x, list[i].Key.y), newValue);
        }
        _verticesAmplitudeDictionary = list.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    #endregion

    
}

[Serializable]
public struct CircularWave
{
    [Tooltip("The center of the wave, the point where it start")]
    public Vector2 Center { get; set; }

    [Tooltip("The duration of the wave in seconds")]
    public float Duration;

    [Tooltip("The height of the waves")]
    public float Amplitude;
    public float CurrentAmplitude { get; set; }

    [Tooltip("The distance it runs")]
    public float Distance;

    [Tooltip("Number of circular vertices points the wave will manage")]
    public int NumberOfPoints;

    [Tooltip("The time it takes to the waves to reach the amplitude and then decrease")]
    public float TimeToAttainMaxAmplitude;
}

[Serializable]
public struct Octave
{
    public Vector2 Speed;
    public Vector2 Scale;
    public float Height;
}
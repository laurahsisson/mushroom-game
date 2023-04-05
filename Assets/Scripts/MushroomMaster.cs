using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MushroomMaster : MonoBehaviour {
    public GameObject MushroomPrefab;
    public GameObject Plane;
    public Shader shader;
    public Material TerrainMaterial;
    public Terrain worldTerrain;

    private const float WorldWidth = 2000;
    private const int MushroomCount = 100;
    private const int LineCount = 3;
    private const float Step = 3;
    private const float HeightOffset =  1f; // Idk the mushrooms arent vertically centered

    // Higher the value, the noiser the result.
    private float PerlinScaler = 10;

    // Offsets are used in perlin noise generator
    private const float SizeMin = .5f;
    private const float SizeMax = 1.5f;

    private const float HeightMax = 50f;

    private const float AlphaMin = .75f;
    private const float AlphaMax = 1f;

    private const float LineDensityMin = 5f;
    private const float LineDensityMax = 10f;


    // Min is just negative max.
    // This tends to cluser around 0 but the alternative would be results that flip.
    private const float RotationMax = 30f;

    private float heightOffset;
    private float sizeOffset;
    private float hueOffset;
    private float saturationOffset;
    private float valueOffset;
    private float alphaOffset;
    private float xRotationOffset;
    private float zRotationOffset;
    private Shader diffuse;

    private GameObject[] anchorShrooms;
    private float[,] heights;
    private int resolution;


    // Start is called before the first frame update
    void Start() {
        heightOffset = Random.value;
        sizeOffset = Random.value;
        hueOffset = Random.value;
        saturationOffset = Random.value;
        valueOffset = Random.value;
        alphaOffset = Random.value;
        xRotationOffset = Random.value;
        zRotationOffset = Random.value;


        TerrainData worldData = worldTerrain.terrainData;
        worldData.size = new Vector3(WorldWidth, HeightMax, WorldWidth);
        resolution = worldData.heightmapResolution;
        heights = worldData.GetHeights(0, 0, resolution, resolution);
        for (int tx = 0; tx < resolution; tx++) {
            for (int ty = 0; ty < resolution; ty++) {
                heights[tx, ty] = TerrainPositionToHeightFraction(tx, ty);
            }
        }
        worldData.SetHeights(0, 0, heights);

        // diffuse = Shader.Find("Transparent/Diffuse");

        Plane.transform.position = new Vector3(WorldWidth / 2, HeightMax*1.25f, WorldWidth / 2);

        anchorShrooms = new GameObject[MushroomCount];
        for (int i = 0; i < MushroomCount; i++) {
            float x = Random.Range(0, WorldWidth);
            float z = Random.Range(0, WorldWidth);
            anchorShrooms[i] = AddMushroom(x, z);
        }


        for (int i = 0; i < MushroomCount; i++) {
            GameObject start = anchorShrooms[i];
            
            HashSet<int> hasLines = new HashSet<int>();
            for (int lc = 0; lc < LineCount; lc++) {
                float closestDist = Mathf.Infinity;
                int closestIdx = -1;
                // Debug.Log("III"+i+","+lc);
                
                for (int j = i+1; j < MushroomCount; j++) {
                    if (hasLines.Contains(j)) {
                        continue;
                    }
                    GameObject end = anchorShrooms[j];
                    float dist = Vector3.Distance(start.transform.position,end.transform.position);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestIdx = j;
                    }
                }
                if (closestIdx == -1) {
                    continue;
                }
                AddMushroomLine(i,closestIdx);
                hasLines.Add(closestIdx);
            }
        }
    }

    private void AddMushroomLine(int startidx, int endidx) {
        GameObject start = anchorShrooms[startidx];
        GameObject end = anchorShrooms[endidx];

        float dist = Vector3.Distance(start.transform.position, end.transform.position);
        float pos = Random.Range(LineDensityMin, LineDensityMax);;
        while (pos < dist) {
            float t = pos / dist;
            float x = Mathf.Lerp(start.transform.position.x, end.transform.position.x, t);
            float z = Mathf.Lerp(start.transform.position.z, end.transform.position.z, t);
            BreedMushroom(start, end, t);
            pos += Random.Range(LineDensityMin, LineDensityMax);
        }
    }

    private float TerrainPositionToHeightFraction(int tx, int tz) {
        float xf = ((float)tx) / resolution;
        float zf = ((float)tz) / resolution;
        float xworld = xf * WorldWidth;
        float zworld = zf * WorldWidth;
        return GetClampedPerlin(0, 1, xworld, zworld, heightOffset);
    }

    private float GetHeight(float x, float z, float size) {
        RaycastHit hit;
        bool hadHit = Physics.Raycast(new Vector3(x, HeightMax, z), Vector3.down, out hit);
        return HeightMax-hit.distance+HeightOffset*size;
    }

    private GameObject AddMushroom(float x, float z) {
        float size = GetClampedPerlin(SizeMin, SizeMax, x, z, sizeOffset);
        
        float height = GetHeight(x,z,size);
        Vector3 position = new Vector3(x, height, z);
        float xr = GetClampedPerlin(-RotationMax, RotationMax, x, z, xRotationOffset);
        float zr = GetClampedPerlin(-RotationMax, RotationMax, x, z, zRotationOffset);
        GameObject mushroom = Object.Instantiate(MushroomPrefab, position, Quaternion.Euler(xr, 0, zr));

        mushroom.transform.localScale = new Vector3(size, size, size);

        Renderer[] renderers = mushroom.GetComponentsInChildren<Renderer>();
        float h = GetPerlin(x, z, hueOffset);
        float s = GetPerlin(x, z, saturationOffset);
        float v = GetPerlin(x, z, valueOffset);
        Color color = Color.HSVToRGB(h, .5f, 1);
        color.a = GetClampedPerlin(AlphaMin, AlphaMax, x, z, alphaOffset);
        foreach (Renderer r in renderers) {
            r.material.SetColor("_Color", color);
            r.material.shader = shader;
        }
        return mushroom;
    }

    private GameObject BreedMushroom(GameObject start, GameObject end, float t) {
        Transform startTrans = start.transform;
        Transform endTrans = end.transform;

        Vector3 scale = Vector3.Lerp(startTrans.localScale, endTrans.localScale, t);

        Vector3 pos = Vector3.Lerp(startTrans.position, endTrans.position, t);
        float height = GetHeight(pos.x, pos.z, scale.x);
        Vector3 fp = new Vector3(pos.x, height, pos.z);


        GameObject mushroom = Object.Instantiate(MushroomPrefab, fp, Quaternion.Lerp(startTrans.rotation, endTrans.rotation, t));
        mushroom.transform.localScale = scale;


        Color startColor = start.GetComponent<Renderer>().material.GetColor("_Color");
        Color endColor = end.GetComponent<Renderer>().material.GetColor("_Color");
        Color color = Color.Lerp(startColor, endColor, t);

        Renderer[] renderers = mushroom.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) {
            r.material.SetColor("_Color", color);
            r.material.shader = shader;
        }

        return mushroom;
    }

    private float GetPerlin(float x, float z, float offset) {
        float xf = (x / WorldWidth) + offset;
        float zf = (z / WorldWidth) + offset;
        return Mathf.PerlinNoise(xf * PerlinScaler, zf * PerlinScaler);
    }

    private float GetClampedPerlin(float min, float max, float x, float z, float offset) {
        return Mathf.Lerp(min, max, GetPerlin(x, z, offset));
    }
}

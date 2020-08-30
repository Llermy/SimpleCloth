using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MassSpringCloth : MonoBehaviour
{
    public Mesh referenceMesh;
    public Material clothMaterial;
    public Material springMaterial;
    public Material massMaterial;
    public float mass = 0.05f;
    public bool drawNet = true;
    public bool drawMesh = false;

    private Mesh sphereMesh;
    private Mesh cylinderMesh;

    private Mesh mesh;
    private Spring[,] springs;
    private Vector3[] positions;
    private Vector3[] velocities;
    private Vector3 gravity = new Vector3(0, -9.8f, 0);

    // Start is called before the first frame update
    void OnValidate()
    {
        Init();
    }

    void Awake()
    {
        Init();
    }

    // Update is called once per frame
    void Update()
    {
        if(Application.isPlaying)
        {
            Simulate();
        }
        
        if (drawNet)
        {
            DrawNet();
        }
        if(drawMesh)
        {
            Graphics.DrawMesh(mesh, transform.localToWorldMatrix, clothMaterial, 0);
        }
/*
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        };
#endif*/
    }

    void Init()
    {
        mesh = CopyMesh(referenceMesh);

        springs = new Spring[mesh.vertexCount, 4];

        positions = mesh.vertices;
        velocities = new Vector3[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            velocities[i] = Vector3.zero;

        }

        GameObject sphObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GameObject cylObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        sphereMesh = sphObj.GetComponent<MeshFilter>().sharedMesh;
        cylinderMesh = cylObj.GetComponent<MeshFilter>().sharedMesh;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
#endif
            GameObject.DestroyImmediate(sphObj);
            GameObject.DestroyImmediate(cylObj);
#if UNITY_EDITOR
        };
#endif
    }

    void Simulate()
    {
        float time = Time.deltaTime;
        Vector3 force = gravity * mass;
        for(int i = 0; i < mesh.vertexCount; i++)
        {
            velocities[i] += Time.deltaTime * force / mass;
            positions[i] += Time.deltaTime * velocities[i];
        }
        mesh.vertices = positions;
    }

    void DrawNet()
    {
        Vector3 scale = Vector3.one * 0.05f;
        Matrix4x4 scaleMat = Matrix4x4.Scale(scale);
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            Graphics.DrawMesh(sphereMesh, Matrix4x4.Translate(mesh.vertices[i]) * transform.localToWorldMatrix * scaleMat, massMaterial, 0);
        }
    }

    static Mesh CopyMesh(Mesh mesh)
    {
        Mesh newmesh = new Mesh();
        newmesh.vertices = mesh.vertices;
        newmesh.triangles = mesh.triangles;
        newmesh.uv = mesh.uv;
        newmesh.normals = mesh.normals;
        newmesh.colors = mesh.colors;
        newmesh.tangents = mesh.tangents;
        return newmesh;
    }
}

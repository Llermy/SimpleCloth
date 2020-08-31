using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct TwoVector3
{
    public Vector3 a1;
    public Vector3 a2;
}

public class Spring
{
    public int[] vertexIndices = new int[2];
    public float restLength;
    public float stiffness;

    private MassSpringClothRectangle cloth;

    public Spring(MassSpringClothRectangle cloth, int v1, int v2, float stiffness = 10)
    {
        this.vertexIndices[0] = v1;
        this.vertexIndices[1] = v2;
        this.cloth = cloth;
        this.stiffness = stiffness;
        this.restLength = this.getLength();
    }

    public float getLength()
    {
        return (cloth.getVertex(vertexIndices[0]) - cloth.getVertex(vertexIndices[1])).magnitude;
    }

    public float CalcSpringForce()
    {
        float distFromRest = this.restLength - this.getLength();
        if (Mathf.Abs(distFromRest) > 0.0001)
            return stiffness * distFromRest;
        return 0;
    }

    public Vector3 CalcSpringForce(int vertex)
    {
        Vector3 a = vertex == vertexIndices[0] ? cloth.getVertex(vertexIndices[1]) : cloth.getVertex(vertexIndices[0]);
        Vector3 b = vertex == vertexIndices[0] ? cloth.getVertex(vertexIndices[0]) : cloth.getVertex(vertexIndices[1]);
        Vector3 link = b - a;
        //return this.CalcSpringForce() * (b-a).normalized;
        Vector3 force = (link - (link / link.magnitude) * this.restLength) * this.stiffness;
        /*if(force.magnitude < 0.001)
        {
            return Vector3.zero;
        }*/
        return -force;
    }

    public TwoVector3 ApplyFixedDistance(float newDistance)
    {
        Vector3 p1 = cloth.getVertex(vertexIndices[0]);
        Vector3 p2 = cloth.getVertex(vertexIndices[1]);

        bool p1Fixed = cloth.fixedVertices.Contains(vertexIndices[0]);
        bool p2Fixed = cloth.fixedVertices.Contains(vertexIndices[1]);

        float distance = (p1 - p2).magnitude;
        if(distance > newDistance)
        {
            if(p1Fixed)
            {
                p2 += (p1 - p2).normalized * (distance - newDistance);
            } else if (p2Fixed)
            {
                p1 += (p2 - p1).normalized * (distance - newDistance);
            } else if(!p1Fixed && !p2Fixed)
            {
                p2 += (p1 - p2).normalized * (distance - newDistance) / 2;
                p1 += (p2 - p1).normalized * (distance - newDistance) / 2;
            }
            
        }
        return new TwoVector3() { 
            a1 = p1,
            a2 = p2
        };
    }
}

[ExecuteInEditMode, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MassSpringClothRectangle : MonoBehaviour
{
    static float timer = 0.0f;

    private static int SPRINGS_PER_VERTEX = 12;
    private static int[,] links = {
        {0, 1},
        {1, 0},
        {0, -1},
        {-1, 0},
        {1, 1},
        {-1, 1},
        {1, -1},
        {-1, -1},
        {0, 2},
        {2, 0},
        {0, -2},
        {-2, 0},
    };

    public int xResolution, yResolution;
    public int[] fixedVertices;
    public Material massMaterial;
    public float mass = 1.0f;
    public float stiffness;
    public float damping;
    public float criticalDeformation;
    public bool isSimulating = false;
    public bool drawNet = false;
    public bool addWind = false;
    public Vector3 windVelocity;

    private Mesh mesh;
    private Mesh sphereMesh;
    private Spring[,] springs;
    private Vector3[] vertices;
    private Vector3[] velocities;
    private Vector3 gravity = new Vector3(0, -9.8f, 0);

    // Start is called before the first frame update
    void Start()
    {
        Init();
        Time.fixedDeltaTime = 0.005f;
    }

    private void OnValidate()
    {
        Init();
    }

    // Update is called once per frame
    void Update()
    {
        if (drawNet)
        {
            DrawNet();
        }
    }

    private void FixedUpdate()
    {
        if (Application.isPlaying && isSimulating)
        {
            Simulate();
        }
    }

    void Init()
    {
        if (sphereMesh == null)
        {
            GameObject sphObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = sphObj.GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
#endif
                GameObject.DestroyImmediate(sphObj);
#if UNITY_EDITOR
            };
#endif
        }

        if (vertices != null && xResolution * yResolution > 0 && xResolution * yResolution == vertices.Length)
        {
            return;
        }

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Cloth mesh";

        // 顶点
        vertices = new Vector3[xResolution * yResolution];
        Vector2[] uv = new Vector2[vertices.Length];
        for (int i = 0, y = 0; y < yResolution; y++)
        {
            for (int x = 0; x < xResolution; x++, i++)
            {
                vertices[i] = new Vector3((float)x / (xResolution - 1) - 0.5f, (float)y / (yResolution - 1) - 0.5f);
                uv[i] = new Vector2((float)x / xResolution, (float)y / yResolution);
            }
        }
        mesh.vertices = vertices;
        mesh.uv = uv;

        // 三角形
        int[] triangles = new int[(xResolution - 1) * (yResolution - 1) * 6];
        for (int ti = 0, vi = 0, y = 0; y < yResolution - 1; y++, vi++)
        {
            for (int x = 0; x < xResolution - 1; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + xResolution;
                triangles[ti + 5] = vi + xResolution + 1;
            }
        }
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        velocities = new Vector3[mesh.vertexCount];
        
        // 初始化弹簧
        int springcount = 0;
        springs = new Spring[mesh.vertexCount, SPRINGS_PER_VERTEX];
        for (int i = 0, y = 0; y < yResolution; y++)
        {
            for (int x = 0; x < xResolution; x++, i++)
            {
                int j = 0;
                for(int k = 0; k < SPRINGS_PER_VERTEX; k++)
                {
                    Vector2Int xy2 = new Vector2Int(x, y);
                    xy2 += new Vector2Int(links[k, 0], links[k, 1]);
                    if(xy2.x < 0 || xy2.y < 0 || xy2.x >= xResolution || xy2.y >= yResolution)
                    {
                        continue;
                    }
                    int i2 = xy2.y * xResolution + xy2.x;
                    if(i2 > i)
                    {
                        springs[i, j] = new Spring(this, i, i2, stiffness);
                        springcount++;
                    } else
                    {
                        springs[i, j] = GetSpring(i2, i);
                    }
                    
                    j++;
                }
            }
        }
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

    void Simulate()
    {
        timer += Time.deltaTime;
        /*float time = Time.deltaTime;
        if(time > 0.02f)
        {
            time = 0.02f;
        }*/
        Vector3 gravForce = gravity * mass;
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            if(!fixedVertices.Contains(i))
            {
                Vector3 accForce = gravForce; // 重力
                accForce += -damping * velocities[i]; // 阻尼
                for(int s = 0; s < SPRINGS_PER_VERTEX; s++)
                {
                    if(springs[i,s] != null)
                    {
                        //Vector3 sf = springs[i, s].CalcSpringForce(i);
                        accForce += springs[i, s].CalcSpringForce(i);
                    }
                }
                //accForce /= 100;s
                Vector3 viscousFluidForce;
                if (addWind)
                    viscousFluidForce = mesh.normals[i] * (Vector3.Dot(mesh.normals[i], windVelocity - velocities[i])) * 1.0f;
                else
                    viscousFluidForce = Vector3.zero;
                accForce += viscousFluidForce;

                velocities[i] += Time.deltaTime * accForce / mass;
                vertices[i] += Time.deltaTime * velocities[i];

                if (vertices[i].y < -1)
                    vertices[i].y = -1;
            }
        }

        if(criticalDeformation > 0)
        {
            ApplyCriticalDeformation();
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }

    public void ApplyCriticalDeformation()
    {
        for(int i = 0; i < mesh.vertexCount; i++)
        {
            for (int s = 0; s < SPRINGS_PER_VERTEX; s++)
            {
                if (springs[i, s] != null && springs[i, s].vertexIndices[1] > springs[i, s].vertexIndices[0])
                {
                    float maxLength = springs[i, s].restLength*(1+criticalDeformation);
                    TwoVector3 newPositions = springs[i, s].ApplyFixedDistance(maxLength);
                    vertices[springs[i, s].vertexIndices[0]] = newPositions.a1;
                    vertices[springs[i, s].vertexIndices[1]] = newPositions.a2;
                }
            }
        }
    }

    public Vector3 getVertex(int vi)
    {
        return this.vertices[vi];
    }

    public Spring GetSpring(int v1, int v2)
    {
        for(int i = 0; i < SPRINGS_PER_VERTEX; i++)
        {
            if(springs[v1, i] != null && springs[v1, i].vertexIndices[1] == v2)
            {
                return springs[v1, i];
            }
        }
        return null;
    }
}

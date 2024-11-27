using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)]
public struct Part
{
    public float press;
    public float dens;
    public Vector3 force;
    public Vector3 vel;
    public Vector3 position;
}

public class comp : MonoBehaviour
{
    public ComputeShader shader;
    public Part[] particles;
    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;

    public Vector3Int numParticles = new Vector3Int(10,10,10);
    public float rad = 0.05f;
    public float particleSize = 12f;

    public Vector3 box = new Vector3(10,10,10);
    public Vector3 init = new Vector3(4,2,1.5f);
    public Vector3 initCenter = new Vector3(0,3,0);
    
    public Mesh mesh;
    public Material mat;

    private static readonly int size = Shader.PropertyToID("_size");
    private static readonly int particleBuffer = Shader.PropertyToID("_particlesBuffer");

    public float mass = 1f;
    public float visc = -0.006f;
    public float damping = -0.5f;

    public float gasConstant = 2f; 
    public float restingDensity = 1f; 

    public float step = 0.0001f;
    public Transform sphere;

    private int num = 0;
    private int densityPressureKernel;
    private int computeForceKernel;
    private int integrateKernel;

    private void Awake() {
        // particle creation
        List<Part> particleList = new List<Part>();

        // loop x, y, and z to fill particle list
        for (int i = 0; i < numParticles.x; i++) {
            for (int j = 0; j < numParticles.y; j++) {
                for (int k = 0; k < numParticles.z; k++) {

                    Vector3 particlePos = new Vector3(i * rad * 2, j * rad * 2, k * rad * 2) + Random.onUnitSphere * rad * 0.1f;
                    particlePos += initCenter - init / 2;

                    Part newPart = new Part {position = particlePos};
                    particleList.Add(newPart);
                }
            }
        }

        // update count and turn list into an array
        num = particleList.Count;
        particles = particleList.ToArray();

        // compute buffer
        _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] indirectArgs = {mesh.GetIndexCount(0), (uint) num, mesh.GetIndexStart(0), mesh.GetBaseVertex(0), 0};
        _argsBuffer.SetData(indirectArgs);

        // set up buffers to process particles later
        InitializeValues();
    }

    private void InitializeValues() {

        // initializing particle variables
        shader.SetFloat("visc", visc);
        shader.SetInt("partLen", num);
        shader.SetFloat("partMass", mass);
        shader.SetFloat("gasConst", gasConstant);
        shader.SetFloat("damping", damping);
        shader.SetFloat("restDensity", restingDensity);

        // constants
        shader.SetFloat("dwConstant", 0.004973591971622f);
        shader.SetFloat("viscLaplacian", 0.397887357729738f);
        shader.SetFloat("grad", -0.099471839432435f);
        shader.SetFloat("pi", 3.141592653589793f);

        // defining radius powers, so we dont have to keep computing
        shader.SetFloat("rad", rad);
        shader.SetFloat("rad2", Mathf.Pow(rad, 2.0f));
        shader.SetFloat("rad3", Mathf.Pow(rad, 3.0f));
        shader.SetFloat("rad4", Mathf.Pow(rad, 4.0f));
        shader.SetFloat("rad5", Mathf.Pow(rad, 5.0f));

        shader.SetVector("box", box);

        _particlesBuffer = new ComputeBuffer(num, 44);
        _particlesBuffer.SetData(particles);

        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        computeForceKernel = shader.FindKernel("ComputeForces");
        integrateKernel = shader.FindKernel("Integrate");

        shader.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(integrateKernel, "_particles", _particlesBuffer);

    }

    private void FixedUpdate() {

        shader.SetVector("box", box);
        shader.SetFloat("step", step);

        mat.SetFloat(size, particleSize);
        mat.SetBuffer(particleBuffer, _particlesBuffer);

        shader.Dispatch(densityPressureKernel, num / 100, 1, 1);
        shader.Dispatch(computeForceKernel, num / 100, 1, 1);
        shader.Dispatch(integrateKernel, num / 100, 1, 1);
    }

    private void Update() {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, new Bounds(Vector3.zero, box), _argsBuffer, castShadows: UnityEngine.Rendering.ShadowCastingMode.Off);
    }

}

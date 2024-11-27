using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class main : MonoBehaviour
{

    // general simulation parameters and particle settings
    public Vector3 box;
    public Vector3 boxMid;

    public Vector3 create;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float gasConstant = 1000.0f;
    public float restingdens = 200.0f;
    public float damp = -0.5f;
    public float viscosity = 150;
    public float partMass = 5f;
    public float rad;
    public float scale = 0.005f;
    public float step;
    private float time = 0f;
    private List<Part> parts = new List<Part>();

    // store properties for individual particles
    private class Part {               
        public Vector3 pos;
        public Vector3 vel;
        public float press;
        public float dens;
        public Vector3 force;
        public bool outside = false;
    }

    // Unity program initialize the particle system
    private void Awake() {
        int widthIter = Mathf.RoundToInt(create.x / (rad * 2));
        int heightIter = Mathf.RoundToInt(create.y / (rad * 2));
        int depthIter = Mathf.RoundToInt(create.z / (rad * 2));
        Vector3 createTopLeft = boxMid - create / 2;

        for (int widthIndex = 1; widthIndex < widthIter; widthIndex++) {
            float widthOffset = widthIndex * rad * 2;

            for (int heightIndex = 1; heightIndex < heightIter; heightIndex++) {
                float heightOffset = heightIndex * rad * 2;

                for (int depthIndex = 1; depthIndex < depthIter; depthIndex++) {
                    float depthOffset = depthIndex * rad * 2;
                    Vector3 basepos = createTopLeft + new Vector3(widthOffset, heightOffset, depthOffset);
                    Vector3 createpos = basepos + Random.onUnitSphere * rad * 0.5f;
                    Part newPart = new Part {
                        pos = createpos
                    };
                    parts.Add(newPart);
                }
            }
        }
    }

    // update particle positions based on current forces and collisions with the bounding box
    private void MoveParts(float deltaTime) {
        Vector3 boundingBoxUpper = box / 2;
        Vector3 boundingBoxLower = -box / 2;

        for (int index = 0; index < parts.Count; index++) {
            Part part = parts[index];
            part.vel += deltaTime * part.force / partMass;
            part.pos += deltaTime * part.vel;
            part.outside = false;

            // handle collisions with the bounding box edges
            if (part.pos.x + rad > boundingBoxUpper.x) {
                part.vel.x *= damp;
                part.pos.x = boundingBoxUpper.x - rad;
                part.outside = true;
            } else if (part.pos.y + rad > boundingBoxUpper.y) {
                part.vel.y *= damp;
                part.pos.y = boundingBoxUpper.y - rad;
                part.outside = true;
            } else if (part.pos.z + rad > boundingBoxUpper.z) {
                part.vel.z *= damp;
                part.pos.z = boundingBoxUpper.z - rad;
                part.outside = true;
            } else if (part.pos.x - rad < boundingBoxLower.x) {
                part.vel.x *= damp;
                part.pos.x = boundingBoxLower.x + rad;
                part.outside = true;
            } else if (part.pos.y - rad < boundingBoxLower.y) {
                part.vel.y *= damp;
                part.pos.y = boundingBoxLower.y + rad;
                part.outside = true;
            } else if (part.pos.z - rad < boundingBoxLower.z) {
                part.vel.z *= damp;
                part.pos.z = boundingBoxLower.z + rad;
                part.outside = true;
            }

        }
    }

    // calculate densities and pressures for all particles based on neighboring particles
    // we could speed up our program by only computing nearby particles
    private void ComputeDensities() {
        
        float adjustedRad = rad * scale;
        float densityFact = 4 / (Mathf.PI * Mathf.Pow(adjustedRad, 8.0f));
        int n = parts.Count;

        // we take the weighted sums of mass near each particle 
        for (int i = 0; i < n; i++) {
            Part part1 = parts[i];
            part1.dens = 0f;

            for (int j = 0; j < n; j++) {
                Part part2 = parts[j];
                float distanceSquared = (part2.pos - part1.pos).sqrMagnitude;

                if (distanceSquared < Mathf.Pow(adjustedRad, 2.0f)) {
                    part1.dens += partMass * densityFact * Mathf.Pow(Mathf.Pow(adjustedRad, 2.0f) - distanceSquared, 3.0f);
                }
            }

            part1.press = gasConstant * (part1.dens - restingdens);
        }
    }


    // calculate acceleration based on pressure and viscosity forces
    private void ComputeAcceleration() {
        int n = parts.Count;

        for (int i = 0; i < n; i++) {
            Part a = parts[i];
            Vector3 press = Vector3.zero;
            Vector3 visc = Vector3.zero;

            for (int j = 0; j < n; j++) {
                Part b = parts[j];
                if (i == j) {
                    continue;
                }
                float distance = Vector3.Distance(a.pos, b.pos) * scale;
                if (2*rad > distance) {
                    float grad = -10.0f / (Mathf.PI * Mathf.Pow(rad, 5.0f));
                    press += ((a.pos - b.pos).normalized) * partMass * (a.press + b.press) / (2.0f * b.dens) * Mathf.Pow(rad - distance, 3.0f) * grad;
                    float viscConst = 40.0f / (Mathf.PI * Mathf.Pow(rad, 5.0f));
                    visc += viscosity * partMass * (b.vel - a.vel) / b.dens * viscConst * (rad - distance);
                }
            }
            a.force = (gravity * partMass) + press + visc / a.dens;
        }
    }

    // main update loop
    private void FixedUpdate() {
        ComputeDensities();
        ComputeAcceleration();
        MoveParts(step);
    }
}

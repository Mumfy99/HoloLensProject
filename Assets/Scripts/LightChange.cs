using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightChange : MonoBehaviour
{
    // --- THIS IS THE CONNECTOR ---
    // By making this 'public', Unity creates a slot in the Inspector.
    // We call it 'bulbPart' so we know what to put there.
    public Renderer bulbPart; 

    // Define the colors we want to use
    // [ColorUsage(true, true)] allows High Dynamic Range (HDR) for glowing brightness
    public Material Grey;
    public Material Green;
    public Material Yellow;
    public Material Red;

    void Start()
    {
        // This runs when the hololens starts. 
        ChangeMaterial(Grey);
    }

    void Update()
    {
        // Simple test inputs
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeMaterial(Grey);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeMaterial(Green);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeMaterial(Yellow);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangeMaterial(Red);
    }

    void ChangeMaterial(Material newMat)
    {
        // SAFETY CHECK: This prevents errors if you forgot to connect the part!
        if (bulbPart == null) 
        {
            Debug.LogError("You forgot to drag the lamp part into the script slot!");
            return;
        }

        if (newMat == null)
        {
            Debug.LogError("You forgot to drag the material into the script slot!");
            return; 
        }

        // --- THIS CHANGES THE MATERIAL ---
        // We access the 'material' property of the connected part
        bulbPart.material = newMat;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightChange : MonoBehaviour
{
    public Renderer bulbPart; 

    // Define the Materials we want to use
    public Material Grey;
    public Material Green;
    public Material Yellow;
    public Material Red;

    void Start()
    {
        ChangeMaterial(Grey);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeMaterial(Grey);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeMaterial(Green);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeMaterial(Yellow);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangeMaterial(Red);
    }

    void ChangeMaterial(Material newMat)
    {
        // SAFETY CHECKS
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

        // We access the 'material' property of the connected part
        bulbPart.material = newMat;
    }
}
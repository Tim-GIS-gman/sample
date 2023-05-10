// Copyright 2022 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using Esri.HPFramework;



// This class holds information for each stadium, controls how they are rendered, and
// also is responsible for placing the object on the surface of the Earth using a raycast.
// For the raycast to properly work ArcGISMapViewComponent.UseMeshColliders has to be true.
public class Tree : MonoBehaviour
{
    [SerializeField]
    private List<string> TreeInfo = new List<string>();

    public ArcGISCameraComponent ArcGISCamera;

    private double SpawnHeight = 10000;
    public double RayCastDistanceThreshold = 300000;
    private bool OnGround = false;


    public void SetInfo(string Info)
    {
        TreeInfo.Add(Info);

        // Based on which leage team belongs to, either the national or american league, we will render the stadium differently
        // See StadiumMaterial.shadergraph for how this is being accomplished
    }

    // Used to tell this object how high it was spawned so we can control the distance of the raycast
    public void SetSpawnHeight(double InSpawnHeight)
    {
        SpawnHeight = InSpawnHeight;
    }

    public void Start()
    {
        // Your existing code

        StartCoroutine(SetOnGround());
    }

    // This Feature Layer does not contain information about the feature's altitude.
    // To account for this when we get within a certain distance. Cast a ray down
    // to find the height of the ground.
    // The reason we are checking within a distance is because we only stream data for what we are looking 
    // at so the hit test wouldn't work for objects that don't have loaded terrain underneath them
    // Another way to get the elevation would be to query/identify the elevation service you are using for each
    // feature to discover the altitude

   
    private IEnumerator SetOnGround()
    {
        //updating position multiple times to increase positioning accuracy
        //you can modify this number to place the object more accurately 
        int maxAttempts = 300; // maximum number of attempts
        int currentAttempt = 0;

        while (!OnGround && currentAttempt < maxAttempts)
        {
            currentAttempt++;

            var CameraHP = ArcGISCamera.GetComponent<HPTransform>();
            var HP = transform.GetComponent<HPTransform>();
            var Distance = (CameraHP.UniversePosition - HP.UniversePosition).ToVector3().magnitude;

            if (Distance < RayCastDistanceThreshold)
            {
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo, (float)SpawnHeight))
                {
                    // Modify the Stadiums altitude based off the raycast hit
                    var TreeLocationComponent = transform.GetComponent<ArcGISLocationComponent>();
                    double NewHeight = TreeLocationComponent.Position.Z - hitInfo.distance;
                    double TreeLongitude = TreeLocationComponent.Position.X;
                    double TreeLatitude = TreeLocationComponent.Position.Y;
                    ArcGISPoint Position = new ArcGISPoint(TreeLongitude, TreeLatitude, NewHeight+0.5, TreeLocationComponent.Position.SpatialReference);
                    TreeLocationComponent.Position = Position;

                    OnGround = true;
                }
            }
            // Wait for a short period before trying again
            yield return new WaitForSeconds(0.3f);
        }
    }

    

}
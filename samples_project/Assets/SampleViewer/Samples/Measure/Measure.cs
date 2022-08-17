﻿// Copyright 2022 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using Esri.HPFramework;
using UnityEngine;
using Unity.Mathematics;

public enum UnitType
{
    m = 0,
    km = 1,
    mi = 2,
    ft=3
}

public class Measure : MonoBehaviour
{
    public GameObject Line;
    public Text GeodedicDistanceText;
    public Text TerrainDistanceText;
    private String unitTxt;
    public GameObject LineMarker;
    public GameObject InterpolationMarker;
    public float InterpolationInterval=100;
    public Dropdown UnitDropdown;
    public Button ClearButton;
    private HPRoot hpRoot;
    private ArcGISMapComponent arcGISMapComponent;
    private float elevationOffset = 20.0f;
    private GameObject FeaturePoint;
    private List<GameObject> featurePoints = new List<GameObject>();
    private Stack<GameObject> stops = new Stack<GameObject>();
    private GameObject lastStop;
    private ArcGISLocationComponent lastStopLocation;
    private double3 lastRootPosition;
    private ArcGISPoint thisPoint;
    private ArcGISPoint lastPoint;
    private ArcGISPoint prePoint;
    private ArcGISPoint nextPoint;
    private double geodedicDistance=0;
    private double terrainDistance=0;
    private LineRenderer lineRenderer;
    private ArcGISSpatialReference spatialRef = new ArcGISSpatialReference(3857);
    private ArcGISLinearUnitId unit;
    private ArcGISAngularUnitId unitDegree = (ArcGISAngularUnitId)9102;
    UnitType currentUnit;

    void Start()
    {
        // We need HPRoot for the HitToGeoPosition Method
        hpRoot = FindObjectOfType<HPRoot>();

        // We need this ArcGISMapComponent for the FromCartesianPosition Method
        // defined on the ArcGISMapComponent.View
        arcGISMapComponent = FindObjectOfType<ArcGISMapComponent>();
        lineRenderer = Line.GetComponent<LineRenderer>();
        lastRootPosition = arcGISMapComponent.GetComponent<HPRoot>().RootUniversePosition;
        unit = (ArcGISLinearUnitId)9001;
        currentUnit = UnitType.m;
        unitTxt = " m";
        UnitDropdown.onValueChanged.AddListener(delegate {
            UnitChanged();
        });
        ClearButton.onClick.AddListener(delegate {
            ClearLine();
        });

       
    }
    
    void Update()
    {
 
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                var lineMarker = Instantiate(LineMarker, hit.point, Quaternion.identity, arcGISMapComponent.transform);

                var geoPosition = HitToGeoPosition(hit);

                lineMarker.GetComponent<ArcGISLocationComponent>().enabled = true;
                lineMarker.GetComponent<ArcGISLocationComponent>().Position = geoPosition;
                lineMarker.GetComponent<ArcGISLocationComponent>().Rotation = new ArcGISRotation(0, 90, 0);

                var thisPoint = new ArcGISPoint(geoPosition.X, geoPosition.Y, geoPosition.Z, spatialRef);

                
                if (stops.Count > 0)
                {
                    lastStop = stops.Peek();
                    lastStopLocation = lastStop.GetComponent<ArcGISLocationComponent>();
                    lastPoint = new ArcGISPoint(lastStopLocation.Position.X, lastStopLocation.Position.Y, lastStopLocation.Position.Z, spatialRef);
                    
                    //calculate distance from last point to this point
                    geodedicDistance += ArcGISGeometryEngine.DistanceGeodetic(lastPoint, thisPoint, new ArcGISLinearUnit(unit), new ArcGISAngularUnit(unitDegree), ArcGISGeodeticCurveType.Geodesic).Distance;
                    GeodedicDistanceText.text = "Distance: "+ Math.Round(geodedicDistance, 3).ToString()+unitTxt;

                    featurePoints.Add(lastStop);
                    //interpolate middle points between last point and this point
                    Interpolate(lastStop, lineMarker, featurePoints);
                    featurePoints.Add(lineMarker);

                    RenderLine(ref featurePoints);
                    
                    RebaseLine();
                   
                }
                //add this point to stops and also to feature points where stop is user-drawed, and feature points is a collection of user-drawed and interpolated
                stops.Push(lineMarker);
                
            }
        }

    }
    
    private void Interpolate(GameObject start, GameObject end, List<GameObject> featurePoints)
    {
        SetElevation(start);
        SetElevation(end);
        ArcGISLocationComponent startLocation = start.GetComponent<ArcGISLocationComponent>();
        ArcGISLocationComponent endLocation = end.GetComponent<ArcGISLocationComponent>();

        ArcGISPoint startPoint = new ArcGISPoint(startLocation.Position.X, startLocation.Position.Y, startLocation.Position.Z, spatialRef);
        ArcGISPoint endPoint = new ArcGISPoint(endLocation.Position.X, endLocation.Position.Y, endLocation.Position.Z, spatialRef);

        double d = ArcGISGeometryEngine.DistanceGeodetic(startPoint, endPoint, new ArcGISLinearUnit((ArcGISLinearUnitId)9001), new ArcGISAngularUnit(unitDegree), ArcGISGeodeticCurveType.Geodesic).Distance;
        float n = Mathf.Floor((float)d / InterpolationInterval);
        double dx = (end.transform.position.x - start.transform.position.x) / n;
        double dz = (end.transform.position.z - start.transform.position.z) / n;

        prePoint = startPoint;
        GameObject pre = start;

        for (int i=0;i<n-1;i++)
        {
            GameObject next = Instantiate(InterpolationMarker, arcGISMapComponent.transform);

            //calculate transform of next point
            float nextX = pre.transform.position.x + (float)dx;
            float nextZ = pre.transform.position.z + (float)dz;
            next.transform.position = new Vector3(nextX, 0, nextZ);

            //set default location component of next point
            next.GetComponent<ArcGISLocationComponent>().enabled = true;
            next.GetComponent<ArcGISLocationComponent>().Rotation = new ArcGISRotation(0, 90, 0);

            //define height
            SetElevation(next);

            //calculate terrain distance between the next point just created and previous point 
            ArcGISLocationComponent nextLocation = next.GetComponent<ArcGISLocationComponent>();
            ArcGISPoint nextPoint = new ArcGISPoint(nextLocation.Position.X, nextLocation.Position.Y, nextLocation.Position.Z, spatialRef);
            terrainDistance += ArcGISGeometryEngine.DistanceGeodetic(prePoint, nextPoint, new ArcGISLinearUnit((ArcGISLinearUnitId)9001), new ArcGISAngularUnit(unitDegree), ArcGISGeodeticCurveType.Geodesic).Distance;
     
            featurePoints.Add(next);

            prePoint = nextPoint;
            pre = next;
        }
        //calculate reminder distance
        terrainDistance += ArcGISGeometryEngine.DistanceGeodetic(prePoint, endPoint, new ArcGISLinearUnit((ArcGISLinearUnitId)9001), new ArcGISAngularUnit(unitDegree), ArcGISGeodeticCurveType.Geodesic).Distance;
        TerrainDistanceText.text = "Terrain distance: " + Math.Round(terrainDistance, 3).ToString() + unitTxt;
    }
    
    private ArcGISPoint HitToGeoPosition(RaycastHit hit, float yOffset = 0)
    {
        var worldPosition = math.inverse(arcGISMapComponent.WorldMatrix).HomogeneousTransformPoint(hit.point.ToDouble3());

        var geoPosition = arcGISMapComponent.View.WorldToGeographic(worldPosition);
        var offsetPosition = new ArcGISPoint(geoPosition.X, geoPosition.Y, geoPosition.Z + yOffset, geoPosition.SpatialReference);

        return GeoUtils.ProjectToSpatialReference(offsetPosition, spatialRef);
    }
 
    private void SetBreadcrumbHeight()
    {
        for (int i = 0; i < featurePoints.Count; i++)
        {
            SetElevation(featurePoints[i]);
        }
    }

    // set height for point transform and location component
    void SetElevation(GameObject stop)
    {
        // start the raycast in the air at an arbitrary to ensure it is above the ground
        var raycastHeight = 5000;
        var position = stop.transform.position;
        var raycastStart = new Vector3(position.x, position.y + raycastHeight, position.z);
        if (Physics.Raycast(raycastStart, Vector3.down, out RaycastHit hitInfo))
        {
            var location = stop.GetComponent<ArcGISLocationComponent>();
            location.Position = HitToGeoPosition(hitInfo, elevationOffset);
            stop.transform.position =  hitInfo.point-new Vector3(0,20,0);
        }
    }

    

    private void RenderLine(ref List<GameObject> featurePoints)
    {
       
        lineRenderer.widthMultiplier = 5;

        var allPoints = new List<Vector3>();

        foreach (var stop in featurePoints)
        {
            if (stop.transform.position.Equals(Vector3.zero))
            {
                Destroy(stop);
                continue;
            }
            allPoints.Add(stop.transform.position);
        }

        lineRenderer.positionCount = allPoints.Count;
        lineRenderer.SetPositions(allPoints.ToArray());
    }

    public void ClearLine()
    {
        foreach (var stop in featurePoints)
            Destroy(stop);
        featurePoints.Clear();
        stops.Clear();
        geodedicDistance = 0;
        terrainDistance = 0;
        GeodedicDistanceText.text = "Distance: " + geodedicDistance + unitTxt;
        TerrainDistanceText.text = "Distance: " + terrainDistance + unitTxt;
        if (lineRenderer)
            lineRenderer.positionCount = 0;

    }

    private void RebaseLine()
    {
        var rootPosition = arcGISMapComponent.GetComponent<HPRoot>().RootUniversePosition;
        var delta = (lastRootPosition - rootPosition).ToVector3();
        if (delta.magnitude > 1) // 1km
        {
            if (lineRenderer != null)
            {
                Vector3[] points = new Vector3[lineRenderer.positionCount];
                lineRenderer.GetPositions(points);
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] += delta;
                }
                lineRenderer.SetPositions(points);
            }
            lastRootPosition = rootPosition;
        }
    }

    void UnitChanged()
    {
        if (UnitDropdown.options[UnitDropdown.value].text == "Meters")
        {
            ArcGISLinearUnitId unitM = (ArcGISLinearUnitId)9001;
            unit = unitM;
            geodedicDistance = ConvertUnits(geodedicDistance, currentUnit, UnitType.m);
            terrainDistance = ConvertUnits(terrainDistance, currentUnit, UnitType.m);
            currentUnit =UnitType.m;
            unitTxt = " m";
        }
        else if (UnitDropdown.options[UnitDropdown.value].text == "Kilometers")
        {
            ArcGISLinearUnitId unitKm = (ArcGISLinearUnitId)9036;
            unit = unitKm;
            geodedicDistance = ConvertUnits(geodedicDistance, currentUnit, UnitType.km);
            terrainDistance = ConvertUnits(terrainDistance, currentUnit, UnitType.m);
            currentUnit = UnitType.km;
            unitTxt = " km";
        }
        else if (UnitDropdown.options[UnitDropdown.value].text == "Miles")
        {
            ArcGISLinearUnitId unitMi = (ArcGISLinearUnitId)9093;
            unit = unitMi;
            geodedicDistance = ConvertUnits(geodedicDistance, currentUnit, UnitType.mi);
            terrainDistance = ConvertUnits(terrainDistance, currentUnit, UnitType.m);
            currentUnit = UnitType.mi;
            unitTxt = " mi";
        }
        else if (UnitDropdown.options[UnitDropdown.value].text == "Feet")
        {
            ArcGISLinearUnitId unitFt = (ArcGISLinearUnitId)9002;
            unit = unitFt;
            geodedicDistance = ConvertUnits(geodedicDistance, currentUnit, UnitType.ft);
            terrainDistance = ConvertUnits(terrainDistance, currentUnit, UnitType.m);
            currentUnit = UnitType.ft;
            unitTxt = " ft";
        }
        GeodedicDistanceText.text = "Geodedic distance: " + Math.Round(geodedicDistance, 3).ToString() + unitTxt;
        TerrainDistanceText.text = "Terrain distance: " + Math.Round(terrainDistance, 3).ToString() + unitTxt;
        //UnitDropdown.interactable=false;

    }

    public static double ConvertUnits(double units, UnitType from, UnitType to)
    {
        double[][] factor =
        {
            new double[] { 1, 0.001, 0.000621371, 3.28084 },
            new double[] { 1000,   1,     0.621371,   3280.84},
            new double[] { 1609.344,     1.609344,       1,   5280},
            new double[] { 0.3048,    0.0003048,  0.00018939,    1}
        };
            
        return units * factor[(int)from][(int)to];
    }

}

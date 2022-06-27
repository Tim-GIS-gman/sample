﻿// Copyright 2022 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//


using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.UI;
using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using Esri.HPFramework;

using UnityEngine;
using TMPro;

using Unity.Mathematics;

public class measure : MonoBehaviour
{
    public GameObject Route;
    public string apiKey;
    public Text txt;
    public GameObject RouteMarker;
    public GameObject InterpolationMarker;
    private HPRoot hpRoot;
    private ArcGISMapComponent arcGISMapComponent;
    private GameObject lastStop;
    private float elevationOffset = 20.0f;
    private List<GameObject> featurePoints = new List<GameObject>();
    private Stack<GameObject> stops = new Stack<GameObject>();
    private double distance;
    private ArcGISPoint thisPoint;
    private ArcGISPoint lastPoint;
    private ArcGISLocationComponent lastStopLocation;
    private LineRenderer lineRenderer;
    private List<LineRenderer> lines;
    GameObject FeaturePoint;
    ArcGISSpatialReference spatialRef = new ArcGISSpatialReference(3857);
    private Vector3 midPosition;

    double3 lastRootPosition;

    void Start()
    {
        // We need HPRoot for the HitToGeoPosition Method
        hpRoot = FindObjectOfType<HPRoot>();

        // We need this ArcGISMapComponent for the FromCartesianPosition Method
        // defined on the ArcGISMapComponent.View
        arcGISMapComponent = FindObjectOfType<ArcGISMapComponent>();

        lineRenderer = Route.GetComponent<LineRenderer>();

        lastRootPosition = arcGISMapComponent.GetComponent<HPRoot>().RootUniversePosition;
        distance = 0;

    }

    void Update()
    {
        // Only Create Marker when Shift is Held and Mouse is Clicked
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                var routeMarker = Instantiate(RouteMarker, hit.point, Quaternion.identity, arcGISMapComponent.transform);

                var geoPosition = HitToGeoPosition(hit);

                routeMarker.GetComponent<ArcGISLocationComponent>().enabled = true;
                routeMarker.GetComponent<ArcGISLocationComponent>().Position = geoPosition;
                routeMarker.GetComponent<ArcGISLocationComponent>().Rotation = new ArcGISRotation(0, 90, 0);

                var thisPoint = new ArcGISPoint(geoPosition.X, geoPosition.Y, geoPosition.Z, spatialRef);


                if (stops.Count > 0)
                {
                    featurePoints.Add(routeMarker);

                    //calculating distance
                    lastStop = stops.Peek();
                    lastStopLocation = lastStop.GetComponent<ArcGISLocationComponent>();
                    lastPoint = new ArcGISPoint(lastStopLocation.Position.X, lastStopLocation.Position.Y, lastStopLocation.Position.Z, spatialRef);
                    distance = distance + ArcGISGeometryEngine.Distance(lastPoint, thisPoint);
                    txt.text = distance.ToString();


                    Insert(lastStop, routeMarker, featurePoints);
                    SetBreadcrumbHeight();
                    RenderLine(ref featurePoints);
                    RebaseRoute();
                    //featurePoints.Clear();


                }

                stops.Push(routeMarker);
                featurePoints.Add(routeMarker);

            }
        }
        if (Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Return))
        {
            /* 
             SetBreadcrumbHeight();
             RenderLine();
             RebaseRoute();*/
        }

    }

    /// <summary>
    /// Return GeoPosition Based on RaycastHit; I.E. Where the user clicked in the Scene.
    /// </summary>
    /// <param name="hit"></param>
    /// <returns></returns>

    private void Insert(GameObject start, GameObject end, List<GameObject> featurePoints)
    {
        //calculating distance 
        ArcGISLocationComponent startLocation = start.GetComponent<ArcGISLocationComponent>();
        ArcGISLocationComponent endLocation = end.GetComponent<ArcGISLocationComponent>();
        ArcGISPoint startPoint = new ArcGISPoint(startLocation.Position.X, startLocation.Position.Y, startLocation.Position.Z, spatialRef);
        ArcGISPoint endPoint = new ArcGISPoint(endLocation.Position.X, endLocation.Position.Y, endLocation.Position.Z, spatialRef);
        double d = ArcGISGeometryEngine.Distance(startPoint, endPoint);
        if (d < 200)
            return;

        //calculating geolocation for next loop
        GameObject mid = Instantiate(InterpolationMarker, arcGISMapComponent.transform);
        double midLocationComponentX = startLocation.Position.X + (endLocation.Position.X - startLocation.Position.X) / 2;
        double midLocaitonComponentY = startLocation.Position.Y + (endLocation.Position.Y - startLocation.Position.Y) / 2;
        mid.GetComponent<ArcGISLocationComponent>().enabled = true;
        mid.GetComponent<ArcGISLocationComponent>().Position = new ArcGISPoint(midLocationComponentX, midLocaitonComponentY, 0, spatialRef);
        mid.GetComponent<ArcGISLocationComponent>().Rotation = new ArcGISRotation(0, 90, 0);

        //calculating unity location to draw points 
        float midX = start.transform.position.x + (end.transform.position.x - start.transform.position.x) / 2;
        float midY = start.transform.position.y + (end.transform.position.y - start.transform.position.y) / 2;
        float midZ = start.transform.position.z + (end.transform.position.z - start.transform.position.z) / 2;
        midPosition = new Vector3(midX, midY, midZ);
        mid.transform.position = midPosition;


        featurePoints.Add(mid);
        Insert(start, mid, featurePoints);
        Insert(mid, end, featurePoints);

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

    // Does a raycast to find the ground
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
        }
    }

    private void ClearRoute()
    {
        /*
        foreach (var stop in stops)
            Destroy(stop);

        stops.Clear();

        if (lineRenderer)
            lineRenderer.positionCount = 0;*/
    }

    private void RenderLine(ref List<GameObject> featurePoints)
    {
        //lineRenderer.positionCount = 0;
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

    // The ArcGIS Rebase component
    private void RebaseRoute()
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

}

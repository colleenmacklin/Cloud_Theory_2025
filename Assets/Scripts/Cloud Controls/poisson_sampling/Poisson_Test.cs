using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoissonDisc;
using TMPro;

public class Poisson_Test : MonoBehaviour
{
    public float radius = 1;
    public Vector2 regionSize = Vector2.one;
    public int rejectionSamples = 30;
    public float displayRadius = 1;
    //public Vector3 regionTranslation = new Vector3(-75, 60f, -40f);//default is -30,-40,0
    public Vector3 regionTranslation = new Vector3(-30f, -40f, -0f);//default is -30,-40,0
    public int _y = 100; //based on how high clouds should spawn
    //public CloudManager _cloudmanager;
    

    List<Vector2> points;

    void OnValidate()
    {
        //pull in values from cloudmanager for testing
        //radius = _cloudmanager.poissonRadius;
        //regionSize = _cloudmanager.poissonRegionSize;
        //rejectionSamples = _cloudmanager.poissonRejectionSamples;
        //regionTranslation = _cloudmanager.regionTranslation;

        points = PoissonDiscSampling.GeneratePoints(radius, regionSize, rejectionSamples);
        //Sort list in order of points closest to the center of the distribution area, outwards

    }

    private void OnDrawGizmos()
    {
        Vector3 gv3 = new Vector3(regionSize.x, 0, regionSize.y); //scale
        Vector2 center2 = new Vector2(gv3.x / 2, gv3.z / 2); //center point

        Vector3 center = new Vector3(gv3.x / 2, _y, gv3.z / 2); //center point
        Vector3 region_shift = new Vector3(gv3.x - regionSize.x, _y, gv3.z - regionSize.y);

        Vector3 region_scale = new Vector3(0, 0, 0);
        Vector3 newRegion = ScaleAndShiftVector(center, region_shift, region_scale); //would want to use a region_shift relative to the player

        //gv2 = (regionSize / 2);
        points.Sort((v1, v2) => (v1 - center2).sqrMagnitude.CompareTo((v2 - center2).sqrMagnitude));

        //Gizmos.DrawWireCube(regionSize / 2, regionSize);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(newRegion, gv3);



        if (points != null)
        {
            //foreach (Vector2 point in points)
                for (int i = 0; i <= 10; i++)
                {
                //convert vector2D to vector3d
                //Vector3 v3point = new Vector3(point.x, _y, point.y); //turn vector2 point into vector3
                Vector3 v3point = new Vector3(points[i].x, _y, points[i].y); //turn vector2 point into vector3
                Vector3 _shift = new Vector3(v3point.x - center.x, v3point.y, v3point.z - center.z); //need to test and eyball based on player POV
                Vector3 _scale = new Vector3(0, 0, 0);
                Vector3 v3point_shift = ScaleAndShiftVector(v3point, _shift, _scale);
                //Gizmos.DrawSphere(v3point_shift, displayRadius);
                //_positions.GetComponent<TextMeshProUGUI>().text = point.x.ToString() + "'" + point.y.ToString();
                Gizmos.DrawSphere(v3point_shift, displayRadius);

                //for (int i = 0; i<=10; i++)
                //{
                    //Draw ten spheres from first ten positions - should look more centered
                //}
                //Gizmos.DrawSphere(point, displayRadius);
                //Debug.Log("GIZMO location: " + v3point_shift);
                //Debug.Log("height of points = " + v3point_shift.y);


            }

        }

    }



    void OnDrawGizmos2d()
    {
        Gizmos.DrawWireCube(regionSize / 2, regionSize);
        if (points != null)
        {
            foreach (Vector2 point in points)
            {
                Gizmos.DrawSphere(point, displayRadius);
            }
        }
    }


    Vector3 ScaleAndShiftVector(Vector3 v, Vector3 shift, Vector3 scale)
    {
        return Vector3.Scale(v, scale) + shift + regionTranslation;
    }

}

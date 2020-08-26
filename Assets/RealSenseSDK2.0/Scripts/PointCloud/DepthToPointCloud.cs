using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Intel.RealSense;

public class DepthToPointCloud : MonoBehaviour
{
    public Texture depthTexture;
    public PointCloud pointCloud;

    // Start is called before the first frame update
    void Start()
    {
        this.pointCloud = new PointCloud();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void updatePointCloud(Frame frame)
    {
        Points points = pointCloud.Process(frame).As<Points>();
        float[] vertices = new float[points.Count * 3];
        points.CopyVertices(vertices);
    }
}

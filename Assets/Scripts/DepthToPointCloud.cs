using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Intel.RealSense;

public class DepthToPointCloud : MonoBehaviour
{
    /// <summary>
    /// RealSense 디바이스로부터 프레임을 받아올 인스턴스
    /// </summary>
    public RsFrameProvider FrameProvider;

    /// <summary>
    /// 깊이 이미지인 프레임만 찾아낼 matcher
    /// </summary>
    private Predicate<Frame> depthMatcher;
    /// <summary>
    /// 포인트 클라우드로 변환할 때 사용할 인스턴스
    /// </summary>
    private PointCloud pointCloud;
    /// <summary>
    /// 포인트 클라우드 좌표 리스트 (x, y, z, x, y, z, ... )
    /// </summary>
    private float[] vertices;
    /// <summary>
    /// 포인트 개수
    /// </summary>
    private int pointsCount;

    // Start is called before the first frame update
    void Start()
    {
        FrameProvider.OnStart += OnStartStreaming;
        pointCloud = new PointCloud();
        pointsCount = 0;
    }

    /// <summary>
    /// 카메라 프레임 스트림이 시작되면 호출되는 메서드
    /// </summary>
    /// <param name="activeProfile">나도모름</param>
    private void OnStartStreaming(PipelineProfile activeProfile)
    {
        depthMatcher = new Predicate<Frame>(DepthMatches);
        FrameProvider.OnNewSample += NewFrame;
    }

    /// <summary>
    /// 프레임 중 깊이 이미지인 프레임만 찾아내는 매칭 메서드
    /// </summary>
    /// <param name="f">프레임 오브젝트</param>
    /// <returns>깊이 이미지인지 여부</returns>
    private bool DepthMatches(Frame f)
    {
        using (var p = f.Profile)
            return p.Stream == Stream.Depth && p.Format == Format.Z16 && p.Index == 0;
    }

    /// <summary>
    /// 스트림으로부터 새 프레임이 도착하면 호출되는 메서드
    /// </summary>
    /// <param name="frame"></param>
    private void NewFrame(Frame frame)
    {
        try
        {
            if (frame.IsComposite)
            {
                // 깊이 이미지만 찾아낸다.
                if (DepthMatches(frame))
                {
                    FrameSet fs = frame.As<FrameSet>();
                    DepthFrame depthFrame = fs.DepthFrame;
                    // 포인트 클라우드 변환
                    Points points = pointCloud.Process(depthFrame).As<Points>();
                    if (pointsCount != points.Count) vertices = new float[points.Count];
                    points.CopyVertices(vertices);

                    float sum = 0;
                    foreach (float v in vertices) sum += v;
                    Debug.Log("Length: " + vertices.Length + ", Sum: " + sum);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}

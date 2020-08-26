using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using Intel.RealSense;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DepthToPointCloud : MonoBehaviour
{
    private static readonly byte FAR = 6;
    private static readonly float BYTE_MAX = 255.0f;
    private static readonly float X_TRANSLATION = 3.0f;
    private static readonly float Y_TRANSLATION = 2.42f;

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
    private Vector3[] vertices;
    private byte[] pointCloudRawData;
    /// <summary>
    /// 포인트 클라우드를 보여줄 매쉬
    /// </summary>
    private Mesh mesh;

    private FrameQueue pointsQueue;
    public  Texture2D uvmap; // 컬러맵
    public Texture2D pointCloudTexture; // 변환된 포인트 클라우드

    // Start is called before the first frame update
    void Start()
    {
        FrameProvider.OnStart += OnStartStreaming;
        pointCloud = new PointCloud();
    }

    /// <summary>
    /// 카메라 프레임 스트림이 시작되면 호출되는 메서드
    /// </summary>
    /// <param name="activeProfile">나도모름</param>
    private void OnStartStreaming(PipelineProfile activeProfile)
    {
        pointsQueue = new FrameQueue(1);
        // using (var depth =
        //    activeProfile.Streams.FirstOrDefault(s => s.Stream == Stream.Depth && s.Format == Format.Z16).As<VideoStreamProfile >())
        //    ResetMesh(depth.Width, depth.Height);
        ResetMesh(640, 480);

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
        if (pointsQueue == null)
            return;
        try
        {
            if (frame.IsComposite)
            {
                using (var fs = frame.As<FrameSet>())
                using (var points = fs.FirstOrDefault<Points>(Stream.Depth, Format.Xyz32f))
                {
                    // 포인트 클라우드 큐에 삽입 (포인트 클라우드로 변환은 PointCloudProcessingPipe 오브젝트에서 함)
                    if (points != null)
                    {
                        pointsQueue.Enqueue(points);
                    }
                }
                return;
            }

            if (frame.Is(Extension.Points))
            {
                pointsQueue.Enqueue(frame);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// 매쉬를 초기화 한다
    /// </summary>
    /// <param name="width">스크린 너비</param>
    /// <param name="height">스크린 높이</param>
    private void ResetMesh(int width, int height)
    {
        Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat));
        pointCloudTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        uvmap = new Texture2D(width, height, TextureFormat.RGFloat, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_UVMap", uvmap);

        if (mesh != null)
            mesh.Clear();
        else
            mesh = new Mesh()
            {
                indexFormat = IndexFormat.UInt32,
            };

        vertices = new Vector3[width * height];
        pointCloudRawData = new byte[width * height * 3];

        var indices = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            indices[i] = i;

        mesh.MarkDynamic();
        mesh.vertices = vertices;

        var uvs = new Vector2[width * height];
        Array.Clear(uvs, 0, uvs.Length);
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                uvs[i + j * width].x = i / (float)width;
                uvs[i + j * width].y = j / (float)height;
            }
        }

        mesh.uv = uvs;

        mesh.SetIndices(indices, MeshTopology.Points, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    protected void LateUpdate()
    {
        if (pointsQueue != null)
        {
            Points points;
            if (pointsQueue.PollForFrame<Points>(out points))
                using (points)
                {
                    if (points.Count != mesh.vertexCount)
                    {
                        using (var p = points.GetProfile<VideoStreamProfile>())
                            ResetMesh(p.Width, p.Height);
                    }

                    // COLOR
                    if (points.TextureData != IntPtr.Zero)
                    {
                        uvmap.LoadRawTextureData(points.TextureData, points.Count * sizeof(float) * 2);
                        uvmap.Apply();
                    }

                    if (points.VertexData != IntPtr.Zero)
                    {
                        points.CopyVertices(vertices);

                        // normalize
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            pointCloudRawData[i * 3] = (byte)((vertices[i].x + X_TRANSLATION) / FAR * BYTE_MAX);
                            pointCloudRawData[i * 3 + 1] = (byte)((vertices[i].y + Y_TRANSLATION) / FAR * BYTE_MAX);
                            pointCloudRawData[i * 3 + 2] = (byte)(vertices[i].z / FAR * BYTE_MAX);
                        }
                        
                        // texture 2d 적용
                        pointCloudTexture.LoadRawTextureData(pointCloudRawData);
                        
                        // 포인트 클라우드 좌표 얻기
                        byte[] rawData = pointCloudTexture.GetRawTextureData();
                        
                        // denormalize
                        for (int i = 0; i < rawData.Length / 3; i++)
                        {
                            vertices[i].Set(
                                (rawData[i * 3] * FAR / BYTE_MAX - X_TRANSLATION),
                                (rawData[i * 3 + 1] * FAR / BYTE_MAX - Y_TRANSLATION),
                                (rawData[i * 3 + 2] * FAR / BYTE_MAX)
                            );
                        }

                        mesh.vertices = vertices;
                        mesh.UploadMeshData(false);
                    }
                }
        }
    }
}

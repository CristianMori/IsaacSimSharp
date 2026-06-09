using IsaacSimSharp.Protocol;
using IsaacSimSharp.Sensors;
using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Exercises the dual sensor access modes (pull + push) against the mock bridge.
/// Real RTX camera output is verified by the Sensors sample against Isaac Sim.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class SensorTests
{
    private readonly MockBridgeFixture _fixture;

    public SensorTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CameraPullReturnsImageFrame()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_pull", width: 8, height: 8);
        var frame = await client.Sensors.GetFrameAsync(cam);

        Assert.Equal(SensorType.SensorCamera, frame.Type);
        Assert.Equal(8u, frame.Image.Width);
        Assert.Equal(8u, frame.Image.Height);
        Assert.Equal(8 * 8 * 4, frame.Image.Data.Length);
    }

    [Fact]
    public async Task CameraStreamPushesFrames()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_stream", width: 4, height: 4);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var count = 0;
        await foreach (var frame in client.Sensors.StreamAsync(cam, cts.Token))
        {
            Assert.Equal(cam, frame.Handle);
            if (++count >= 3)
                break;
        }

        Assert.True(count >= 3, $"expected >= 3 streamed frames, got {count}");
    }

    [Fact]
    public async Task CameraSegmentationReturnsLabelImageAndLabels()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_seg", width: 8, height: 8, segmentation: true);
        var frame = await client.Sensors.GetFrameAsync(cam);

        Assert.Equal(8 * 8 * 4, frame.Image.Segmentation.Length); // uint32 label per pixel
        Assert.True(frame.Image.SegmentationLabels.Count >= 1);
    }

    [Fact]
    public async Task CameraInstanceSegmentationReturnsLabelImage()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_inst", width: 8, height: 8, instanceSegmentation: true);
        var frame = await client.Sensors.GetFrameAsync(cam);

        Assert.Equal(8 * 8 * 4, frame.Image.InstanceSegmentation.Length);
        Assert.True(frame.Image.InstanceLabels.Count >= 1);
    }

    [Fact]
    public async Task CameraNormalsReturnsNormalImage()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_normals", width: 8, height: 8, normals: true);
        var frame = await client.Sensors.GetFrameAsync(cam);

        Assert.Equal(8 * 8 * 3 * 4, frame.Image.Normals.Length); // float32 x,y,z per pixel
    }

    [Fact]
    public async Task CameraFrameCarriesIntrinsics()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_intr", width: 8, height: 8);
        var frame = await client.Sensors.GetFrameAsync(cam);

        Assert.NotNull(frame.Image.Intrinsics);
        Assert.True(frame.Image.Intrinsics.Fx > 0);
        Assert.Equal(4.0, frame.Image.Intrinsics.Cx); // width/2
    }

    [Fact]
    public async Task DepthCloudDeprojectsToCameraSpacePoints()
    {
        using var client = _fixture.CreateClient();
        var cam = await client.Sensors.CreateCameraAsync("/World/cam_cloud", width: 8, height: 8);
        var frame = await client.Sensors.GetFrameAsync(cam);

        var points = DepthCloud.ToPoints(frame.Image);
        Assert.Equal(8 * 8, points.Count); // all pixels have valid (2 m) depth in the mock
        Assert.All(points, p => Assert.Equal(2.0f, p.Z, 3)); // z == depth
        // Principal-point pixel (cx,cy) deprojects to (0,0,d) in the optical frame.
        Assert.Contains(points, p => Math.Abs(p.X) < 1e-3 && Math.Abs(p.Y) < 1e-3);
    }

    [Fact]
    public async Task ImuPullReturnsImuFrame()
    {
        using var client = _fixture.CreateClient();
        var imu = await client.Sensors.CreateImuAsync("/World/imu");
        var frame = await client.Sensors.GetFrameAsync(imu);

        Assert.Equal(SensorType.SensorImu, frame.Type);
        Assert.Equal(9.81, frame.Imu.LinearAcceleration.Z, 3);
    }

    [Fact]
    public async Task ContactPullReturnsContactFrame()
    {
        using var client = _fixture.CreateClient();
        var contact = await client.Sensors.CreateContactAsync("/World/foot/contact");
        var frame = await client.Sensors.GetFrameAsync(contact);

        Assert.Equal(SensorType.SensorContact, frame.Type);
        Assert.True(frame.Contact.InContact);
        Assert.True(frame.Contact.ForceMagnitude > 0);
    }

    [Fact]
    public async Task LidarPullReturnsPointCloudWithIntensities()
    {
        using var client = _fixture.CreateClient();
        var lidar = await client.Sensors.CreateLidarAsync("/World/lidar");
        var frame = await client.Sensors.GetFrameAsync(lidar);

        Assert.Equal(SensorType.SensorLidar, frame.Type);
        Assert.Equal(3u, frame.PointCloud.Count);
        Assert.Equal(3 * 12, frame.PointCloud.Points.Length);
        Assert.Equal(3 * 4, frame.PointCloud.Intensities.Length);
    }

    [Fact]
    public async Task RadarPullReturnsPointCloud()
    {
        using var client = _fixture.CreateClient();
        var radar = await client.Sensors.CreateRadarAsync("/World/radar");
        var frame = await client.Sensors.GetFrameAsync(radar);

        Assert.Equal(SensorType.SensorRadar, frame.Type);
        Assert.Equal(3u, frame.PointCloud.Count);
        Assert.Equal(3 * 12, frame.PointCloud.Points.Length);
    }
}

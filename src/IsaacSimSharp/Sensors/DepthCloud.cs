using System.Numerics;
using System.Runtime.InteropServices;
using IsaacSimSharp.Protocol;

namespace IsaacSimSharp.Sensors;

/// <summary>
/// Converts a camera's depth image into a 3D point cloud using its pinhole intrinsics.
/// Requires a frame captured with <c>depth: true</c> (which also carries
/// <see cref="ImageFrame.Intrinsics"/>). Points are returned in the camera's optical frame:
/// +x right, +y down, +z forward along the view direction.
/// </summary>
public static class DepthCloud
{
    /// <summary>
    /// Deprojects every pixel with finite, positive depth into a camera-space point.
    /// Pixels with zero/negative/non-finite depth (e.g. sky/background) are skipped.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The frame has no depth buffer or no intrinsics, or the buffer size is inconsistent.
    /// </exception>
    public static IReadOnlyList<Vector3> ToPoints(ImageFrame frame)
    {
        if (frame.Depth.IsEmpty)
            throw new InvalidOperationException("frame has no depth buffer; capture the camera with depth: true");
        if (frame.Intrinsics is null)
            throw new InvalidOperationException("frame has no camera intrinsics; cannot deproject");

        var width = (int)frame.Width;
        var height = (int)frame.Height;
        if (frame.Depth.Length != width * height * sizeof(float))
            throw new InvalidOperationException(
                $"depth buffer size {frame.Depth.Length} does not match {width}x{height} float32");

        double fx = frame.Intrinsics.Fx, fy = frame.Intrinsics.Fy;
        double cx = frame.Intrinsics.Cx, cy = frame.Intrinsics.Cy;
        if (fx <= 0.0 || fy <= 0.0)
            throw new InvalidOperationException("intrinsics focal length must be positive");

        var depth = MemoryMarshal.Cast<byte, float>(frame.Depth.Span);
        var points = new List<Vector3>(width * height);
        for (var v = 0; v < height; v++)
        {
            for (var u = 0; u < width; u++)
            {
                var d = depth[v * width + u];
                if (!float.IsFinite(d) || d <= 0f)
                    continue;
                var x = (float)((u - cx) * d / fx);
                var y = (float)((v - cy) * d / fy);
                points.Add(new Vector3(x, y, d));
            }
        }
        return points;
    }
}

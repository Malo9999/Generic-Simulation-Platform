using UnityEngine;

public sealed class TrackGenLabQualityGizmos : MonoBehaviour
{
    private MarbleRaceTrack track;
    private MarbleRaceTrackValidator.QualityReport quality;

    public void Assign(MarbleRaceTrack targetTrack, MarbleRaceTrackValidator.QualityReport qualityReport)
    {
        track = targetTrack;
        quality = qualityReport;
    }

    private void OnDrawGizmos()
    {
        if (track == null || track.SampleCount <= 2)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.95f);
        for (var i = 0; i < quality.SharpCornerIndices.Count; i++)
        {
            var idx = track.Wrap(quality.SharpCornerIndices[i]);
            Gizmos.DrawSphere(ToVector3(track.Center[idx]), 0.45f);
        }

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
        for (var i = 0; i < quality.AxisAlignedSegmentIndices.Count; i++)
        {
            var idx = track.Wrap(quality.AxisAlignedSegmentIndices[i]);
            var a = track.Center[idx];
            var b = track.Center[(idx + 1) % track.SampleCount];
            Gizmos.DrawLine(ToVector3(a), ToVector3(b));
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.95f);
        for (var i = 0; i < quality.MinRadiusIndices.Count; i++)
        {
            var idx = track.Wrap(quality.MinRadiusIndices[i]);
            var p = track.Center[idx];
            Gizmos.DrawWireSphere(ToVector3(p), 0.65f);
        }
    }

    private static Vector3 ToVector3(Vector2 p)
    {
        return new Vector3(p.x, p.y, 0f);
    }
}

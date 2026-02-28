using UnityEngine;

public class TrackRuntime : MonoBehaviour
{
    [SerializeField] private TrackBakedData bakedData;

    public void Initialize(TrackBakedData data)
    {
        bakedData = data;
    }

    public bool IsOffTrack(Vector2 arenaLocalPos)
    {
        return DistanceToMainCenter(arenaLocalPos) > bakedData.trackWidth * 0.5f;
    }

    public Vector2 ClampToTrack(Vector2 arenaLocalPos)
    {
        if (bakedData == null || bakedData.mainCenterline == null || bakedData.mainCenterline.Length < 2)
        {
            return arenaLocalPos;
        }

        var nearest = bakedData.mainCenterline[0];
        var best = float.MaxValue;

        for (var i = 1; i < bakedData.mainCenterline.Length; i++)
        {
            var candidate = TrackMathUtil.ClosestPointOnSegment(bakedData.mainCenterline[i - 1], bakedData.mainCenterline[i], arenaLocalPos);
            var distSq = (candidate - arenaLocalPos).sqrMagnitude;
            if (distSq < best)
            {
                best = distSq;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private float DistanceToMainCenter(Vector2 point)
    {
        if (bakedData == null || bakedData.mainCenterline == null || bakedData.mainCenterline.Length < 2)
        {
            return float.MaxValue;
        }

        var best = float.MaxValue;
        for (var i = 1; i < bakedData.mainCenterline.Length; i++)
        {
            var candidate = TrackMathUtil.ClosestPointOnSegment(bakedData.mainCenterline[i - 1], bakedData.mainCenterline[i], point);
            best = Mathf.Min(best, Vector2.Distance(candidate, point));
        }

        return best;
    }
}

using UnityEngine;

public interface IFieldDepositBuffer
{
    Rect WorldBounds { get; }
    void AddDeposit(Vector2 worldPos, float amount, float radius);
    float Sample(Vector2 worldPos);
}

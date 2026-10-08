using UnityEngine;

/// <summary>
/// 마무리 공격으로 날아갈 때의 회전 / 축소 기준점 표시용.
/// 적 프리팹의 자식 오브젝트에 붙이고, 돌았으면 하는 위치로 옮긴다. (없으면 콜라이더 중심 사용)
/// </summary>
public class LaunchPivot : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.12f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.2f, transform.position + Vector3.right * 0.2f);
        Gizmos.DrawLine(transform.position + Vector3.down * 0.2f, transform.position + Vector3.up * 0.2f);
    }
}

using System.Collections;
using UnityEngine;

/// <summary>독 방울. 일정 시간 뒤 아기를 낳고 사라짐 (소환 패턴이 생성)</summary>
public class BossPoisonBubble : MonoBehaviour
{
    [SerializeField] private BossBaby _babyPrefab;
    [SerializeField, Min(0f)] private float _hatchDelay = 1.2f;
    [SerializeField, Min(1)] private int _babyCount = 1;

    [Tooltip("아기가 여러 마리일 때 좌우 간격")]
    [SerializeField, Min(0f)]
    private float _babySpacing = 0.6f;

    private Boss _boss;

    public void Initialize(Boss boss)
    {
        _boss = boss;
        if (_boss != null)
        {
            _boss.RegisterMinion(gameObject);
        }

        StartCoroutine(HatchRoutine());
    }

    private IEnumerator HatchRoutine()
    {
        yield return new WaitForSeconds(_hatchDelay);

        if (_babyPrefab != null)
        {
            float startOffset = -(_babyCount - 1) * _babySpacing * 0.5f;
            for (int i = 0; i < _babyCount; i++)
            {
                Vector3 position = transform.position + (Vector3.right * (startOffset + (i * _babySpacing)));
                BossBaby baby = Instantiate(_babyPrefab, position, Quaternion.identity);
                baby.Initialize(_boss);
            }
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.UnregisterMinion(gameObject);
        }
    }
}

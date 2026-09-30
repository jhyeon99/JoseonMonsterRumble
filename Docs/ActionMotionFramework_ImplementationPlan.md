# 액션 모션 데이터 프레임워크 구현 계획서

## 1. 개요
바탕화면의 액션 모션 프레임워크 설계를 바탕으로, 애니메이션 클립 길이와 배속을 수식으로 결합하여 총 지속 시간을 도출하고 캔슬 가능 시점을 통제하는 일관된 모션 프레임워크를 프로젝트에 구현한다.

---

## 2. 생성 및 수정 파일 목록

### 2.1 신규 생성 파일
- `Assets/02.Scripts/Player/Data/ActionMotionData.cs`
  - 직렬화 가능한 모션 메타데이터 클래스
  - AnimationClip 참조, Animator 배속, 캔슬 비율, 기본 예비 시간을 보유
  - TotalDuration 및 CanCancel 계산식 제공

### 2.2 기존 수정 파일
- `Assets/02.Scripts/Player/Player.cs`
  - 피격 및 회피 모션에 대한 ActionMotionData 직렬화 필드 추가
  - 상태 머신에서 각 모션 데이터에 접근할 수 있는 프로퍼티 제공
- `Assets/02.Scripts/Player/State/PlayerHitState.cs`
  - 고정 타이머 대신 ActionMotionData의 TotalDuration을 기준으로 경직 지속 시간 결정
  - 물리 X축 고정을 유지하며 모션 지속 시간 종료 시 자동 복귀
- `Assets/02.Scripts/Player/State/PlayerDodgeState.cs`
  - 회피 이동 지속 시간 및 물리 곡선 계산 기준을 ActionMotionData의 TotalDuration으로 일원화
  - CancelRatio 도달 시 조기 행동 전환 허용 로직 적용

---

## 3. 핵심 클래스 세부 구현

### 3.1 ActionMotionData.cs
```csharp
using UnityEngine;

[System.Serializable]
public class ActionMotionData
{
    [SerializeField] private AnimationClip _clip;
    [SerializeField, Min(0.01f)] private float _animatorSpeed = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float _cancelRatio = 0.8f;
    [SerializeField, Min(0.01f)] private float _fallbackDuration = 0.6f;

    public AnimationClip Clip => _clip;
    public float AnimatorSpeed => _animatorSpeed;
    public float CancelRatio => _cancelRatio;

    public float TotalDuration => (_clip != null && _animatorSpeed > 0f)
        ? _clip.length / _animatorSpeed
        : _fallbackDuration;

    public float CancelStartTime => TotalDuration * _cancelRatio;

    public bool CanCancel(float elapsedTime) => elapsedTime >= CancelStartTime;
}
```

---

## 4. 상태별 적용 방안

### 4.1 피격 상태 (PlayerHitState)
1. 진입 시점:
   - Rigidbody2D의 X축 이동을 물리적으로 고정
   - 애니메이터 hit 트리거 발동
2. 실행 시점:
   - 경과 시간 누적
   - 경과 시간이 HitMotion.TotalDuration에 도달하면 물리 고정을 해제하고 Idle 또는 Move로 복귀

### 4.2 회피 상태 (PlayerDodgeState)
1. 진입 시점:
   - 무적 상태 활성화 및 dodge 트리거 발동
   - 시작 위치 및 회피 방향 기록
2. 물리 연산 시점:
   - 정규화 진행도 = Mathf.Clamp01(경과시간 / DodgeMotion.TotalDuration)
   - 속도 곡선 평가 후 위치 갱신
3. 실행 시점:
   - 경과 시간이 DodgeMotion.CancelStartTime에 도달하면 선입력된 이동 또는 공격 허용
   - 경과 시간이 DodgeMotion.TotalDuration에 도달하면 상태 머신 완전 복귀 및 무적 해제

---

## 5. 검증 시나리오
1. 피격 검증:
   - 적 공격 충돌 시 피격 모션이 진행되는 동안 적이 밀더라도 플레이어 위치가 고정되는지 확인
   - 피격 모션 종료와 동시에 정확히 조작이 복구되는지 확인
2. 회피 검증:
   - 회피 실행 시 이동 궤적과 회피 애니메이션이 동시에 끝나 미끄러짐이 없는지 확인
   - 회피 후반부 캔슬 구간에서 부드럽게 다음 동작으로 연계되는지 확인

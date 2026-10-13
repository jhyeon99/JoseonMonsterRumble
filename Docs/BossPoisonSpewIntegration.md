# 독분사 패턴 인계

## 준비 상태

실제 PF_Boss, Boss_AC, BossPattern 및 원본 보스 스테이지는 이 준비 작업에서 변경하지 않았다.
패턴을 실제 보스에 등록하지 않았다. 준비 패턴의 Weight는 0으로 저장했다.

- 패턴: Assets/03.Prefabs/Enemy/Boss/PF_BossPoisonSpewPattern.prefab
- VFX: Assets/03.Prefabs/VFX/PF_BossPoisonBeam.prefab
- 기존 BossPattern 상속: BossPoisonSpewPattern
- 검수된 입 키 원본: Assets/05.Animations/VFX/Anim_BossPoisonSpewPreview.anim
- 확인용 씬: Assets/01.Scenes/Test_BossPoisonSpew.unity

## 팀원 작업 완료 후 연결

1. PF_BossPoisonSpewPattern을 보스 SpriteRenderer 오브젝트의 자식으로 배치한다. 패턴의 로컬 위치·회전은 0, 크기는 1로 둔다. 해당 SpriteRenderer는 실제 보스 Animator 계층 안에 있어야 한다.
2. 패턴의 Boss Renderer에 실제 보스 SpriteRenderer를 연결한다. Mouth와 Vfx Prefab은 준비 프리팹에 연결돼 있다. 기존 보스의 Mouth는 수정하지 않는다.
3. 씬의 패턴 오브젝트를 선택하고 Tools > Poison Spew > Prepare Clip For Selected Pattern을 실행한다. 검수된 입 키와 스프라이트 키를 현재 계층 경로로 복사한 새 클립이 생성되고 패턴의 Animation Clip에 연결된다. 원본 키와 Controller는 수정하지 않는다.
4. 생성된 클립을 실제 Animator의 독분사 상태에 넣는다. 준비 패턴의 Animation Trigger 기본값은 PoisonSpew다. 이 Trigger를 Controller에 추가해 독분사 상태로 진입시키고, 완료 시 기존 대기 상태로 복귀시키는 전이를 팀의 기존 방식대로 설정한다. 다른 이름을 사용할 경우 패턴의 Trigger도 맞춘다.
5. 실제 Animator 상태 Speed와 패턴의 Animation Speed를 일치시킨다. 연결 도구가 클립의 실제 길이와 프레임 속도로 Hit Start Ratio와 Hit End Ratio를 계산한다. 원본 시작 프레임은 10, 종료 프레임은 38이다. 클립을 편집하면 비율을 다시 맞춘다.
6. 팀에서 결정한 Phase, Floor, Cooldown, Weight를 설정한다. Weight는 실제 등록을 승인한 마지막 단계에서만 0보다 크게 바꾼다. 보스 패턴 수집 전에 씬/프리팹에 붙어 있어야 한다. 실행 중 컴포넌트 추가 방식은 지원하지 않는다.

## 실행 및 정리 규약

- BossPattern의 기존 트리거·클립 대기·진행도·Cleanup을 사용한다. 별도의 Animator 재생이나 패턴 선택기를 만들지 않는다.
- VFX는 보스 초기화 때 한 번만 생성하고 이후 재사용한다. 비활성 VFX 프리팹에 실제 입·SpriteRenderer·공격자를 연결한 뒤 활성화한다.
- 정상 종료: 입에 연결된 채로 감압 → 전체 줄기 동시 분해 → 크기가 다른 독가루가 감속·중력 낙하 → 소멸.
- 정상 종료 파편이 소멸할 때까지 패턴 Execute가 기다린다. 이후 BossPattern의 기존 애니메이션 완료 대기가 적용된다.
- 그로기·페이즈 전환·보스 비활성화: Cleanup에서 StopFire로 판정과 모든 파티클을 즉시 정리한다.
- 바닥 잔류 장판은 없다. 피해 판정은 분사 중에만 활성화되며 종료 파편에는 피해가 없다.

## 조절 위치

물줄기·피해·종료 독가루 수치는 PF_BossPoisonBeam의 PoisonStream > BossPoisonBeam에서 조절한다.
크기: Residual Droplet Size Range. 양: Residual Droplets Per Fragment.
낙하 감속: Residual Drag. 잔류 시간: Residual Particle Lifetime.
입 방향을 그대로 따르게 하려면 Enable Follow Through를 끈다. 별도 회전 연출을 유지하면 해당 회전 설정을 검수한다.
프리뷰 씬의 저장된 Beam 수치를 준비 VFX에 복사했다. 이후 프리뷰만 수정했다면 준비 VFX에도 같은 수치를 반영해야 한다.

## 접목 후 필수 확인

- 실제 보스 양방향에서 입 위치와 분사각이 맞는지 확인.
- 애니메이션 진입과 분사 시작·종료 시점 확인.
- 벽·지형 충돌 및 플레이어 다단 피해 간격 확인.
- 정상 종료 중 입 연결·전체 동시 분해·독가루 낙하 확인.
- 그로기·페이즈 전환·사망·비활성화 중 판정이나 파티클이 남지 않는지 확인.
- Profiler에서 독분사 중과 종료 파편의 CPU·GC·투명 렌더링 비용 확인.

실제 보스 실행 검증과 Profiler 측정은 미완료다. 준비 파일의 컴파일 검사는 실행 결과 보고를 따른다.

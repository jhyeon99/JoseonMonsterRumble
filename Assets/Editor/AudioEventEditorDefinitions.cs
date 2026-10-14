using System;
using System.Collections.Generic;

internal static class AudioEventEditorDefinitions
{
    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<string>> Categories = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    static AudioEventEditorDefinitions()
    {
        const string definitions = @"
Player.Attack1|해당 1타 공격 시작
Player.Attack2|해당 2타 공격 시작
Player.Attack3|해당 3타 공격 시작
Player.MeleeImpact|실제 피해가 적용된 근접 명중
Player.Footstep|접지·이동 중 발 디딤 마커
Player.Jump|점프 실행 성공
Player.Land|실제 공중 → 접지 전환
Player.Dodge|백스텝 실행 시작
Player.Hurt|방어되지 않은 비치명 피해 적용
Player.Death|사망 확정
Player.Block|실제 가드 방어 성공
Player.Parry|실제 패링 성공
Player.GuardBreak|가드 브레이크 확정
Player.Finisher|마무리 타격 실행당 1회
Player.ChargeStep|부적 슬롯 활성화 성공
Player.TalismanFire|각 부적 발사부터 명중 또는 소멸까지
Player.TalismanExplosion|유효 명중으로 부적 폭발 시작
Player.PotionUse|물약 회복·소모 성공
Player.GuardEnter|가드 자세 진입 성공
Player.ChargeLoop|부적 충전 시작
Player.ChargeFull|최대 충전 단계 최초 도달
Player.Unavailable|자원 부족 등으로 사용 실패
Ground.Hurt|비치명 피해 적용
Ground.Death|사망 확정
Ground.Footstep|접지·이동 중 발 디딤 마커
Ground.MeleeAttack|근접 공격 실행 시작
Ground.RangedFire|투사체 실제 생성·발사
Ground.Groggy|패링 후 그로기 진입
Ground.AttackReady|공격 준비 시작
Ground.Detect|대기·순찰에서 추적으로 전환
Ground.FinisherLaunch|마무리 공격에 의한 날려 보내기 시작
Air.Hurt|비치명 피해 적용
Air.Death|사망 확정
Air.MeleeDash|U자 돌진 실행 시작
Air.RangedFire|투사체 실제 생성·발사
Air.Flight|비행 이동 시작
Air.Groggy|패링 후 그로기 진입
Air.AttackReady|공격 준비 시작
Air.Ambience|대기·순찰 상태 시작
Boss.Intro|등장 연출 시작
Boss.Hurt|직접 공격 피해 적용
Boss.Footstep|기본 이동·후퇴 점프의 실제 착지마다 1회
Boss.FrogAmbience|보스전 중 울음이 끝난 뒤 랜덤 휴식 간격으로 1회씩
Boss.Tongue|각 높이의 혀 뻗기 시작
Boss.TongueImpact|보스 혀가 플레이어에게 실제 피해를 준 순간
Boss.Stomp|각 찍기의 실제 착지
Boss.NeedleFire|좌우 바늘 발사 묶음 생성
Boss.Spit|각 침뱉기 발사 묶음 생성
Boss.SpitImpact|보스 침이 플레이어에게 직접 피해를 준 순간
Boss.ChargeStart|실제 돌진 이동·판정 시작
Boss.PhaseHowl|1 → 2페이즈 전환 연출 시작
Boss.SummonStart|독 방울 소환 패턴 진입
Boss.BubbleHurt|개구리알에 실제 피해가 들어간 타격마다
Boss.BubblePop|공격에 의한 방울 파열 성공
Boss.BabyLaunch|3타로 올챙이 날려 보내기 성공
Boss.BabyHitBoss|올챙이 기믹 피해 적용 성공
Boss.Groggy|찍기 패링 후 짧은 그로기 진입
Boss.FinalGroggy|2페이즈 체력 소진 후 마무리 대기 진입
Boss.Defeat|마무리에 의한 최종 처치 확정
Boss.AttackReady|패턴 예고 시작
Boss.Jump|각 찍기의 도약 시작
Boss.TongueRetract|패링 후 혀 회수 시작
Boss.ChargeLoop|실제 돌진 시작
Boss.ChargeEnd|돌진 정상 종료
Boss.BubbleSpawn|독 방울 웨이브 생성 성공
Boss.BabySpawn|방울 파열에 의한 올챙이 생성 성공
Boss.BabyHurt|올챙이 비치명 피해 적용
Boss.BabyDeath|올챙이 일반 사망 확정
Boss.GroggyRecover|짧은 그로기 정상 회복
Music.Title|타이틀 씬 진입
Music.Field|EP1 씬 진입
Music.Boss|실제 보스 전투 시작
Music.BossPhase2|2페이즈 진입 확정
Music.Prologue|EP0 문을 열고 나온 직후
Music.Victory|보스 최종 처치 확정
Music.Death|사망 화면 진입
Item.SoulPickup|소울 수집 성공
Item.PotionPickup|물약 수집 성공
Environment.Checkpoint|새 체크포인트 저장 성공
Environment.FallingWarning|발판 파괴 예고 애니메이션 시작
Environment.FallingBreak|발판 실제 낙하 시작
Environment.PlatformStart|발판 이동 실행 시작
Environment.PlatformLoop|발판 이동 실행 시작
Environment.PlatformStop|목적지 도착으로 정상 정지
Environment.Switch|발판 이동 요청 성공
UI.Click|버튼 클릭 처리
UI.Hover|버튼 포인터 진입
UI.PanelOpen|패널 열림
UI.PanelClose|패널 닫힘
UI.Slider|사용자 조작으로 슬라이더 값 변경
Prologue.Scream|지정 비명 연출 시작
Prologue.Break|지정 지게 파손 연출 시작
Prologue.Door|지정 문 연출 시작
Buff.Apply|버프 상태 진입
Buff.Loop|버프 유지 시작
Buff.Enrage|광폭화 상태 진입
Buff.Explosion|폭발 연출·판정 발생
";
        foreach (string line in definitions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|');
            Descriptions.Add(parts[0], parts[1]);
            string prefix = parts[0].Substring(0, parts[0].IndexOf('.'));
            if (!Categories.TryGetValue(prefix, out List<string> ids)) Categories[prefix] = ids = new List<string>();
            ids.Add(parts[0]);
        }
    }

    public static string[] Ids(string prefix) => Categories.TryGetValue(prefix, out List<string> ids) ? ids.ToArray() : Array.Empty<string>();
    public static string Description(string id) => id != null && Descriptions.TryGetValue(id, out string description)
        ? description : "기획서에 등록되지 않은 이벤트입니다.";
}

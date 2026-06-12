using System;
using System.Collections.Generic;

namespace New_Tradegy.Alerting
{
    // =========================
    // 1) Alert 타입 (10개 고정)
    // =========================
    public enum AlertType
    {
        NextChanged,         // Next 교체(자주) - S1
        NextLocked,          // Next가 임계치 돌파로 "주목" - S2
        ActivePromoted,      // Active 비어있을 때 Next->Active 승격 - S1
        ActiveReplaced,      // (옵션) Active 교체 - S2 (보통은 금지 추천)

        ImpulseStart,        // 가속 시작(싹) - S1 or OFF
        ImpulseStrong,       // 강함(한 번 볼만) - S2
        EruptionExtreme,     // 극강 eruption (행동급 후보) - S3
        EruptionFailSign,    // 꺾임/이탈(손절 후보) - S2 (Alarm 금지)

        OrderFilled,         // 체결/부분체결 업데이트 - 기본 OFF (원하면 S1)
        NoRequest            // RQ 부족/차단 - S1 (쿨다운 길게)
    }

    public enum Severity
    {
        S1_Tick = 1,
        S2_Ping = 2,
        S3_Alarm = 3
    }

    [Flags]
    public enum HudTarget
    {
        None = 0,
        Mouse = 1,
        Area = 2,
        Center = 4
    }

    // 사운드 id는 네 SoundUtils.Sound("일반","no request") 같은 체계에 맞춰 string로 둠
    public sealed class AlertRule
    {
        public AlertType Type { get; set; }
        public Severity Severity { get; set; }
        public HudTarget Hud { get; set; }
        public string SoundId { get; set; } // null이면 소리 OFF (런타임에서 null 체크)
        public int GlobalCooldownMs { get; set; }
        public int PerSymbolCooldownMs { get; set; }
        public int SuppressLowerAfterMs { get; set; }
    }

    public sealed class Alert
    {
        public AlertType Type { get; set; }
        public string Symbol { get; set; } = "";    // 종목코드/심볼(섹터면 "SECTOR:xxx")
        public string Title { get; set; } = "";     // CenterHud용 짧은 제목
        public string Message { get; set; } = "";   // 1~2줄 요약
        public int SlotIndex { get; set; } = -1;    // AreaHud에서 어느 칸 강조할지
        public DateTime TsUtc { get; set; } = DateTime.UtcNow;
    }

    // =====================================
    // 2) 기본 룰셋 (네가 말한 철학에 맞춤)
    // =====================================
    public static class DefaultAlertRules
    {
        // SoundId 예시: "tick", "ping", "alarm", "no_rq"
        // 네 프로젝트에 맞춰 SoundUtils.Sound(category, id) 형태로 변환해서 쓰면 됨
        public static readonly Dictionary<AlertType, AlertRule> Rules = new Dictionary<AlertType, AlertRule>()
        {
            [AlertType.NextChanged] = new AlertRule
            {
                Type = AlertType.NextChanged,
                Severity = Severity.S1_Tick,
                Hud = HudTarget.Area,
                SoundId = "tick",
                GlobalCooldownMs = 1000,
                PerSymbolCooldownMs = 8000,
                SuppressLowerAfterMs = 0
            },
            [AlertType.NextLocked] = new AlertRule
            {
                Type = AlertType.NextLocked,
                Severity = Severity.S2_Ping,
                Hud = HudTarget.Center | HudTarget.Area,
                SoundId = "ping",
                GlobalCooldownMs = 5000,
                PerSymbolCooldownMs = 15000,
                SuppressLowerAfterMs = 2500
            },
            [AlertType.ActivePromoted] = new AlertRule
            {
                Type = AlertType.ActivePromoted,
                Severity = Severity.S1_Tick,
                Hud = HudTarget.Area,
                SoundId = "tick",
                GlobalCooldownMs = 1200,
                PerSymbolCooldownMs = 12000,
                SuppressLowerAfterMs = 0
            },
            [AlertType.ActiveReplaced] = new AlertRule
            {
                Type = AlertType.ActiveReplaced,
                Severity = Severity.S2_Ping,
                Hud = HudTarget.Center | HudTarget.Area,
                SoundId = "ping",
                GlobalCooldownMs = 5000,
                PerSymbolCooldownMs = 20000,
                SuppressLowerAfterMs = 2500
            },

            [AlertType.ImpulseStart] = new AlertRule
            {
                Type = AlertType.ImpulseStart,
                Severity = Severity.S1_Tick,
                Hud = HudTarget.Mouse | HudTarget.Area,
                SoundId = null, // 기본은 OFF 추천(너무 자주 발생 가능)
                GlobalCooldownMs = 1200,
                PerSymbolCooldownMs = 8000,
                SuppressLowerAfterMs = 0
            },
            [AlertType.ImpulseStrong] = new AlertRule
            {
                Type = AlertType.ImpulseStrong,
                Severity = Severity.S2_Ping,
                Hud = HudTarget.Center | HudTarget.Area,
                SoundId = "ping",
                GlobalCooldownMs = 5000,
                PerSymbolCooldownMs = 12000,
                SuppressLowerAfterMs = 2500
            },
            [AlertType.EruptionExtreme] = new AlertRule
            {
                Type = AlertType.EruptionExtreme,
                Severity = Severity.S3_Alarm,
                Hud = HudTarget.Center | HudTarget.Area,
                SoundId = "alarm",
                GlobalCooldownMs = 25000,
                PerSymbolCooldownMs = 30000,
                SuppressLowerAfterMs = 3000
            },
            [AlertType.EruptionFailSign] = new AlertRule
            {
                Type = AlertType.EruptionFailSign,
                Severity = Severity.S2_Ping,
                Hud = HudTarget.Mouse | HudTarget.Center,
                SoundId = "ping",
                GlobalCooldownMs = 7000,
                PerSymbolCooldownMs = 15000,
                SuppressLowerAfterMs = 2500
            },

            [AlertType.OrderFilled] = new AlertRule
            {
                Type = AlertType.OrderFilled,
                Severity = Severity.S1_Tick,
                Hud = HudTarget.Mouse,
                SoundId = null, // 체결은 기본 OFF (원하면 "tick"으로)
                GlobalCooldownMs = 1000,
                PerSymbolCooldownMs = 10000,
                SuppressLowerAfterMs = 0
            },
            [AlertType.NoRequest] = new AlertRule
            {
                Type = AlertType.NoRequest,
                Severity = Severity.S1_Tick,
                Hud = HudTarget.Center | HudTarget.Mouse,
                SoundId = "no_rq",
                GlobalCooldownMs = 5000,     // 네가 이미 쓰는 5초 제한과 맞춤
                PerSymbolCooldownMs = 0,
                SuppressLowerAfterMs = 0
            },
        };
    }

    // =====================================
    // 3) 스팸차단/우선순위/쿨다운 매니저
    // =====================================
    public sealed class AlertManager
    {
        private readonly Dictionary<AlertType, int> _lastTypeTick = new Dictionary<AlertType, int>();
        private readonly Dictionary<(AlertType, string), int> _lastTypeSymbolTick = new Dictionary<(AlertType, string), int>();

        // "S3 울린 직후 낮은 레벨 억제"를 위한 전역 게이트
        private int _suppressBelowSeverityUntilTick = 0;
        private Severity _suppressBelowSeverity = Severity.S1_Tick;

        // "한 틱 1사운드"를 위한 현재 틱 처리용
        private int _lastSoundTick = 0;

        public bool TryAccept(in Alert alert, out AlertRule rule)
        {
            rule = DefaultAlertRules.Rules[alert.Type];

            int now = Environment.TickCount;

            // 1) S3/S2 이후 낮은 레벨 억제
            if (now < _suppressBelowSeverityUntilTick)
            {
                if (rule.Severity < _suppressBelowSeverity)
                    return false;
            }

            // 2) 종류별 쿨다운
            if (rule.GlobalCooldownMs > 0 &&
                _lastTypeTick.TryGetValue(alert.Type, out int lastType) &&
                now - lastType < rule.GlobalCooldownMs)
                return false;

            // 3) 종목별 쿨다운
            if (rule.PerSymbolCooldownMs > 0 && !string.IsNullOrEmpty(alert.Symbol))
            {
                var key = (alert.Type, alert.Symbol);
                if (_lastTypeSymbolTick.TryGetValue(key, out int lastSym) &&
                    now - lastSym < rule.PerSymbolCooldownMs)
                    return false;
            }

            // 통과 → 기록
            _lastTypeTick[alert.Type] = now;
            if (rule.PerSymbolCooldownMs > 0 && !string.IsNullOrEmpty(alert.Symbol))
                _lastTypeSymbolTick[(alert.Type, alert.Symbol)] = now;

            // 억제창 갱신(높은 레벨만 의미)
            if (rule.SuppressLowerAfterMs > 0)
            {
                _suppressBelowSeverity = rule.Severity; // 이 severity 미만을 억제
                _suppressBelowSeverityUntilTick = now + rule.SuppressLowerAfterMs;
            }

            return true;
        }

        public bool TryGetSound(AlertRule rule, out string soundId)
        {
            soundId = rule.SoundId;

            if (soundId == null)
                return false;

            int now = Environment.TickCount;

            if (now == _lastSoundTick)
                return false;   // 한 틱 1사운드

            _lastSoundTick = now;
            return true;
        }


        // =====================================
        // 4) 사용 예시 (너가 꽂을 위치)
        // =====================================
        /*
            // Detector/Runner에서:
            var alert = new Alert
            {
                Type = AlertType.EruptionExtreme,
                Symbol = stock,
                Title = $"{stock} EXTREME",
                Message = $"zPro {zPro:F1} / dom {dom:F2} / 10s {delta10:F0}%",
                SlotIndex = slot
            };

            if (_alertManager.TryAccept(alert, out var rule))
            {
                // HUD 반영
                if (rule.Hud.HasFlag(HudTarget.Area))   AreaHud.Post(alert);
                if (rule.Hud.HasFlag(HudTarget.Mouse))  MouseHud.Post(alert);
                if (rule.Hud.HasFlag(HudTarget.Center)) CenterHud.Post(alert);

                // 소리
                if (_alertManager.TryGetSound(rule, out var sid))
                    SoundUtils.Sound("일반", sid); // 너 방식대로
            }
        */
    }
}
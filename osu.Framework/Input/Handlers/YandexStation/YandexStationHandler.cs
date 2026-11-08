// Copyright (c) SanyaPilot 2026

using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Input.StateChanges;
using osu.Framework.IO.Network;
using osu.Framework.Platform;
using osu.Framework.Statistics;

namespace osu.Framework.Input.Handlers.YandexStation
{
    public class YandexStationHandler : InputHandler
    {
        private static readonly GlobalStatistic<ulong> statistic_total_events = GlobalStatistics.Get<ulong>(StatisticGroupFor<YandexStationHandler>(), "Total events");

        public override string Description => "Yandex Station Max";

        public override bool IsActive => true;

        public Bindable<string> Hostname { get; } =
            new Bindable<string>("Yandex-Station-Max");

        public BindableInt Port { get; } = new BindableInt(1337)
        {
            MinValue = 1,
            MaxValue = 65535,
        };

        [JsonIgnore]
        public YandexStationWorker Worker { get; private set; } = null!;

        public override bool Initialize(GameHost host)
        {
            if (!base.Initialize(host))
                return false;

            Worker = new(Hostname.Value, Port.Value);
            Worker.Connect();

            Enabled.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    Worker.PacketReceived += handleEvent;
                }
                else
                {
                    Worker.PacketReceived -= handleEvent;
                }
            }, true);

            Hostname.BindValueChanged(e => Worker.Hostname = e.NewValue, false);
            Port.BindValueChanged(e => Worker.Port = e.NewValue, false);

            return true;
        }

        private void handleEvent(object? sender, string input)
        {
            switch (input)
            {
                case "ring:cw":
                    enqueueJoystickButtonDown(JoystickButton.Axis64Positive);
                    enqueueJoystickButtonUp(JoystickButton.Axis64Positive);
                    break;

                case "ring:ccw":
                    enqueueJoystickButtonDown(JoystickButton.Axis64Negative);
                    enqueueJoystickButtonUp(JoystickButton.Axis64Negative);
                    break;

                case "btn:dn":
                    enqueueJoystickButtonDown(JoystickButton.Button128);
                    break;

                case "btn:up":
                    enqueueJoystickButtonUp(JoystickButton.Button128);
                    break;
            }
        }

        private void enqueueJoystickEvent(IInput evt)
        {
            PendingInputs.Enqueue(evt);
            FrameStatistics.Increment(StatisticsCounterType.JoystickEvents);
            statistic_total_events.Value++;
        }

        private void enqueueJoystickButtonDown(JoystickButton button) => enqueueJoystickEvent(new JoystickButtonInput(button, true));

        private void enqueueJoystickButtonUp(JoystickButton button) => enqueueJoystickEvent(new JoystickButtonInput(button, false));
    }
}

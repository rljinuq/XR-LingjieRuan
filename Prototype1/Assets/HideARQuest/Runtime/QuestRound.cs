using System;

namespace HideAR.Quest
{
    public enum QuestPhase { Intro, Placing, Saving, Review, Handoff, Countdown, Seeking, Found, Error }

    // Input must be released after each transition before it can affect the next stage.
    public sealed class QuestRound
    {
        public QuestPhase Phase { get; private set; } = QuestPhase.Intro;
        public bool InputArmed { get; private set; }
        public bool AnchorPending { get; private set; }
        public float SearchSeconds { get; private set; }
        public float CountdownRemaining { get; private set; }
        float releaseSeconds;

        public void Reset()
        {
            AnchorPending = false;
            SearchSeconds = 0;
            CountdownRemaining = 0;
            Enter(QuestPhase.Intro);
        }

        public bool StartHiding()
        {
            if (Phase != QuestPhase.Intro && Phase != QuestPhase.Found) return false;
            SearchSeconds = 0;
            Enter(QuestPhase.Placing);
            return true;
        }

        public void UpdateRelease(bool anyTriggerHeld, float delta)
        {
            // Stay armed for the next press, regardless of Input System callback order.
            if (InputArmed) return;
            if (anyTriggerHeld) { releaseSeconds = 0; return; }
            releaseSeconds += delta;
            if (releaseSeconds >= 0.35f) InputArmed = true;
        }

        public void Disarm() { InputArmed = false; releaseSeconds = 0; }

        public bool RequestPlacement()
        {
            if (Phase != QuestPhase.Placing || !InputArmed || AnchorPending) return false;
            AnchorPending = true;
            Enter(QuestPhase.Saving);
            return true;
        }

        public bool AcceptAnchor()
        {
            if (Phase != QuestPhase.Saving || !AnchorPending) return false;
            AnchorPending = false;
            Enter(QuestPhase.Review);
            return true;
        }

        public bool Move()
        {
            if (Phase != QuestPhase.Review) return false;
            Enter(QuestPhase.Placing);
            return true;
        }

        public bool Confirm()
        {
            if (Phase != QuestPhase.Review) return false;
            Enter(QuestPhase.Handoff);
            return true;
        }

        public bool Ready()
        {
            if (Phase != QuestPhase.Handoff) return false;
            CountdownRemaining = 3;
            Enter(QuestPhase.Countdown);
            return true;
        }

        public void Tick(float delta, bool trackingAndPresent)
        {
            if (!trackingAndPresent) { Disarm(); return; }
            if (Phase == QuestPhase.Countdown)
            {
                CountdownRemaining = Math.Max(0, CountdownRemaining - delta);
                if (CountdownRemaining <= 0) Enter(QuestPhase.Seeking);
            }
            else if (Phase == QuestPhase.Seeking) SearchSeconds += delta;
        }

        public bool Found()
        {
            if (Phase != QuestPhase.Seeking || !InputArmed) return false;
            Enter(QuestPhase.Found);
            return true;
        }

        public void Fail() { Enter(QuestPhase.Error); }
        void Enter(QuestPhase value) { Phase = value; Disarm(); }
    }
}

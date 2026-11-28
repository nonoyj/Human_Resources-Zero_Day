// HR//0D -- feeds the shift clock into RoomLighting once per frame.
// Assembly: HRZeroDay.Game
//
// Kept separate from RoomLighting so the lighting can be built and tested without a shift, and so a
// missing ShiftController degrades to a static "day" room instead of throwing during scene build.
using HRZeroDay.Core.Data;
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game
{
    public class DeskLightDriver : MonoBehaviour
    {
        public RoomLighting lighting;
        public ShiftController shift;
        /// <summary>Seconds left on the CURRENT applicant; MonitorScreenView owns that timer.</summary>
        public MonitorScreenView screen;
        /// <summary>The surface baker that actually holds the lit pixels (v130.5). The lamp's switch has to
        /// reach the thing that owns the room's lit surfaces, or it only changes the light's formula.</summary>
        public ScreenLight surfaces;
        /// <summary>Seconds left on the current applicant. Owned by whatever drives the shift clock;
        /// the room light and the HUD must read the same number or the sky lies about the time.</summary>
        public double currentApplicantRemaining = ShiftClock.SecondsPerApplicant;
        /// <summary>The wall clock's hands (v130). Fed the SAME progress the room light gets.</summary>
        public RoomClockView clock;
        /// <summary>The lamp's level (v130): 0 when it is off, 1 when it is on, ramped over a second.</summary>
        public LampView lamp;
        public bool lampOn = true;

        /// <summary>
        /// Shift progress 0..1. The day is a fixed number of applicants (ShiftController.Today), and each
        /// applicant is worth ShiftClock.SecondsPerApplicant, so progress is applicants finished plus the
        /// fraction of the current one. The HUD clock and the room light read the same two numbers.
        /// </summary>
        public static double ProgressOf(ShiftController controller, double currentApplicantRemaining)
        {
            if (controller == null) return 0.0;
            var today = controller.Today;
            int count = today == null ? 0 : today.Count;
            if (count <= 0) return 0.0;
            double per = ShiftClock.SecondsPerApplicant;
            double elapsed = controller.CurrentIndex * per + (per - Mathf.Clamp((float)currentApplicantRemaining, 0f, (float)per));
            return Daylight.Progress(elapsed, count * per);
        }

        void Update()
        {
            double progress = ProgressOf(shift, currentApplicantRemaining);
            if (lighting != null) lighting.Apply(progress);
            // ONE progress, three readers: the room's tint, the lamp's level and the wall clock's hands.
            if (clock != null) clock.progress = (float)progress;
            if (lamp != null)
            {
                lamp.SetOn(lampOn);
                // THE FIXTURE AND THE ROOM MUST AGREE (v130.5, BUG-040). The lamp's level is ramped over a
                // second, so the room crossfades with it instead of snapping: the wood under the shade gains
                // its step as the shade brightens, and loses it as the shade goes dull. This one line is what
                // was missing between the switch and the light.
                if (surfaces != null) surfaces.lampOn = lampOn;
            }
        }
    }
}

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
        /// <summary>Seconds left on the current applicant. Owned by whatever drives the shift clock;
        /// the room light and the HUD must read the same number or the sky lies about the time.</summary>
        public double currentApplicantRemaining = ShiftClock.SecondsPerApplicant;

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
            if (lighting == null) return;
            lighting.Apply(ProgressOf(shift, currentApplicantRemaining));
        }
    }
}

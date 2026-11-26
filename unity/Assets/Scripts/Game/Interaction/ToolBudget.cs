// HR//0D -- imperfect verification tools: 2 / 2 / 1 uses per day (ADR-006).
// Pure C#: the budget only counts uses. It never decides whether a record is genuine,
// because a tool MUST be able to say VALID for an AI.
// Owner: Agent (interaction layer).
using System.Collections.Generic;

namespace HRZeroDay.Game.Interaction
{
    public sealed class ToolBudget
    {
        private readonly Dictionary<ToolKind, int> _used = new Dictionary<ToolKind, int>();
        private int _day = 1;

        public int Day { get { return _day; } }

        public int Used(ToolKind tool)
        {
            int n;
            return _used.TryGetValue(tool, out n) ? n : 0;
        }

        public int Remaining(ToolKind tool)
        {
            return EvidenceMap.MaxUsesPerDay(tool) - Used(tool);
        }

        public bool TryUse(ToolKind tool, int day)
        {
            AdvanceToDay(day);
            if (Remaining(tool) <= 0) return false;
            _used[tool] = Used(tool) + 1;
            return true;
        }

        public void AdvanceToDay(int day)
        {
            if (day == _day) return;
            _day = day;
            _used.Clear();
        }
    }
}

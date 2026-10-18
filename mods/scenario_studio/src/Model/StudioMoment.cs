namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;

    /// <summary>
    /// What the novice mode shows instead of a listing: the line on screen now
    /// (<see cref="Say"/>), the lines said right before it, and — for a
    /// question — every answer with the place it leads to. Service lines
    /// (lights, labels, <c>end</c>) never appear; an answer's
    /// <c>-&gt; :label</c> is followed to the first meaningful line after the
    /// label (through <c>goto</c> hops), so <c>option → :exit → switch_scene</c>
    /// reads as "goes to the scene". An answer without a target or with a
    /// missing label is <see cref="StudioOutcomeKind.Broken"/>. When no dialog
    /// runs, the moment is the scenario's first line or question.
    /// </summary>
    public sealed class StudioMoment
    {
        public StudioCodeLine Say { get; private set; }
        public List<StudioCodeLine> Before { get; } = new();
        public List<StudioBranch> Branches { get; } = new();

        public bool IsQuestion => Say != null && Say.Kind == StudioLineKind.Choice;

        public static StudioMoment Build(StudioSession session)
        {
            var moment = new StudioMoment();
            var lines = session.Lines;
            var index = FindAnchor(lines, session.CurrentIndex);
            if (index < 0)
                return moment;

            moment.Say = lines[index];

            for (var i = index - 1; i >= 0 && lines[i].Kind == StudioLineKind.Line; i--)
                moment.Before.Insert(0, lines[i]);

            if (!moment.IsQuestion)
                return moment;

            for (var next = index + 1; next < lines.Count && lines[next].Kind == StudioLineKind.Option; next++)
                moment.Branches.Add(Resolve(lines, lines[next]));
            return moment;
        }

        private static int FindAnchor(List<StudioCodeLine> lines, int current)
        {
            if (current < 0)
                current = 0;
            if (lines.Count == 0)
                return -1;

            if (lines[current].Kind == StudioLineKind.Option)
            {
                for (var i = current; i >= 0; i--)
                {
                    if (lines[i].Kind == StudioLineKind.Choice)
                        return i;
                }
                return -1;
            }

            for (var i = current; i < lines.Count; i++)
            {
                if (lines[i].Kind == StudioLineKind.Line || lines[i].Kind == StudioLineKind.Choice)
                    return i;
            }
            return -1;
        }

        private static StudioBranch Resolve(List<StudioCodeLine> lines, StudioCodeLine option)
        {
            var branch = new StudioBranch { Line = option, Kind = StudioOutcomeKind.Broken };
            var label = string.IsNullOrEmpty(option.Target) ? -1 : FindLabel(lines, option.Target);
            if (label < 0)
                return branch;

            var hops = 0;
            for (var i = label + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                switch (line.Kind)
                {
                    case StudioLineKind.Goto:
                        i = string.IsNullOrEmpty(line.Target) ? -1 : FindLabel(lines, line.Target);
                        if (i < 0 || ++hops > 8)
                            return branch;
                        continue;
                    case StudioLineKind.Jump:
                        branch.Kind = StudioOutcomeKind.Scene;
                        branch.Target = line.Target;
                        return branch;
                    case StudioLineKind.Action:
                        branch.Kind = StudioOutcomeKind.Action;
                        branch.Target = line.Target;
                        return branch;
                    case StudioLineKind.End:
                        branch.Kind = StudioOutcomeKind.End;
                        return branch;
                    case StudioLineKind.Line:
                    case StudioLineKind.Choice:
                        branch.Kind = StudioOutcomeKind.Continue;
                        return branch;
                }
            }

            branch.Kind = StudioOutcomeKind.End;
            return branch;
        }

        private static int FindLabel(List<StudioCodeLine> lines, string name)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Kind == StudioLineKind.Label && lines[i].Target == name)
                    return i;
            }
            return -1;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists.Impls;

namespace Cthangover.Core.Skills
{
    public partial class SkillWidget : ColumnCellListWidget<SkillCardBehaviour, SkillInfo>
    {
        public override ICollection<SkillInfo> CreateModels()
        {
            return GameData.Instance.Runtime.SkillData.Skills.Select(x => SkillFactory.Instance.Get(x)).ToList();
        }
    }
}

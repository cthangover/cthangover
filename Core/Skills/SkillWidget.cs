using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists.Impls;

namespace Cthangover.Core.Skills
{
    /// <summary>
    /// UI widget that displays all skills currently owned by the player in a
    /// column-based card grid. Extends
    /// <see cref="ColumnCellListWidget{TItem, TModel}"/> to reuse the generic
    /// list-rendering pipeline. When the widget is populated it reads the set
    /// of owned skill IDs from <see cref="SkillData.Skills"/> in the current
    /// <see cref="GameData"/> runtime, resolves each ID to a
    /// <see cref="SkillInfo"/> via <see cref="SkillFactory.Instance"/>, and
    /// hands the resulting models to the list for rendering as
    /// <see cref="SkillCardBehaviour"/> items.
    /// </summary>
    public partial class SkillWidget : ColumnCellListWidget<SkillCardBehaviour, SkillInfo>
    {
        /// <summary>
        /// Produces the collection of <see cref="SkillInfo"/> models that the
        /// list widget will render. Each ownedskill ID from
        /// <see cref="GameData.Instance.Runtime.SkillData"/> is resolved through
        /// <see cref="SkillFactory.Instance.Get"/> and collected into a
        /// <see cref="List{T}"/>.
        /// </summary>
        /// <returns>
        /// A snapshot of all currently owned skills, each as a fully-populated
        /// <see cref="SkillInfo"/> instance.
        /// </returns>
        public override ICollection<SkillInfo> CreateModels()
        {
            return GameData.Instance.Runtime.SkillData.Skills.Select(x => SkillFactory.Instance.Get(x)).ToList();
        }
    }
}
